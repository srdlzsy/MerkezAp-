using System.Text.Json;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationManualDocumentService(
    WarehouseOrderListQueryExecutor warehouseOrderListQueryExecutor,
    WarehouseOrderDetailQueryExecutor warehouseOrderDetailQueryExecutor,
    CompanyMovementListQueryExecutor companyMovementListQueryExecutor,
    CompanyMovementDetailQueryExecutor companyMovementDetailQueryExecutor,
    InventoryCountListQueryExecutor inventoryCountListQueryExecutor,
    InventoryCountDetailQueryExecutor inventoryCountDetailQueryExecutor,
    AxataSynchronizationOutboxWriter outboxWriter,
    AxataSynchronizationLiveTransportService liveTransportService,
    MikroWriteDbContext mikroWriteDbContext)
{
    private const WarehouseOrderListDirection AxataWarehouseOrderDirection = WarehouseOrderListDirection.Received;
    private const short MikroUserNo = 39;
    private const string CompletedStatus = "1";

    public Task<AxataSynchronizationManualDocumentCandidatesDto> ListCandidatesAsync(
        AxataSynchronizationTaskExecutionContext context,
        AxataSynchronizationManualDocumentCandidateCriteria criteria,
        CancellationToken cancellationToken) =>
        ListCandidatesCoreAsync(context, criteria, cancellationToken);

    public Task<AxataSynchronizationManualDocumentDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        AxataSynchronizationManualDocumentInput input,
        CancellationToken cancellationToken) =>
        ExecuteCoreAsync(context, input, cancellationToken);

    public Task<AxataSynchronizationManualDocumentDto> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        AxataSynchronizationManualDocumentInput input,
        CancellationToken cancellationToken) =>
        ExecuteCoreAsync(context, input, cancellationToken);

    public Task<AxataSynchronizationManualDocumentBatchDto> PreviewBatchAsync(
        AxataSynchronizationTaskExecutionContext context,
        IReadOnlyCollection<AxataSynchronizationManualDocumentInput> inputs,
        bool continueOnError,
        CancellationToken cancellationToken) =>
        ExecuteBatchCoreAsync(context, inputs, continueOnError, cancellationToken);

    public Task<AxataSynchronizationManualDocumentBatchDto> ExecuteBatchAsync(
        AxataSynchronizationTaskExecutionContext context,
        IReadOnlyCollection<AxataSynchronizationManualDocumentInput> inputs,
        bool continueOnError,
        CancellationToken cancellationToken) =>
        ExecuteBatchCoreAsync(context, inputs, continueOnError, cancellationToken);

    public Task<AxataSynchronizationManualDispatchDto> DispatchLiveAsync(
        AxataSynchronizationTaskExecutionContext context,
        AxataSynchronizationManualDocumentInput input,
        CancellationToken cancellationToken) =>
        DispatchLiveCoreAsync(context, input, cancellationToken);

    public Task<AxataSynchronizationManualDispatchBatchDto> DispatchBatchLiveAsync(
        AxataSynchronizationTaskExecutionContext context,
        IReadOnlyCollection<AxataSynchronizationManualDocumentInput> inputs,
        bool continueOnError,
        CancellationToken cancellationToken) =>
        DispatchBatchLiveCoreAsync(context, inputs, continueOnError, cancellationToken);

    private async Task<AxataSynchronizationManualDocumentCandidatesDto> ListCandidatesCoreAsync(
        AxataSynchronizationTaskExecutionContext context,
        AxataSynchronizationManualDocumentCandidateCriteria criteria,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var notes = new List<string>
        {
            "Bu endpoint manuel kurtarma icin uygun evraklari listeler; payload uretmez."
        };

        IReadOnlyCollection<AxataSynchronizationManualDocumentCandidateItemDto> items;
        int totalRecordCount;

        switch (context.Definition.Code)
        {
            case "issued-warehouse-order-sync":
            {
                var documents = await warehouseOrderListQueryExecutor.ExecuteAsync(
                    new WarehouseOrderListRequest(warehouseNo, criteria.StartDate, criteria.EndDate),
                    AxataWarehouseOrderDirection,
                    cancellationToken);
                totalRecordCount = documents.Count;
                items = documents
                    .Skip(criteria.Skip)
                    .Take(criteria.Take)
                    .Select(document => new AxataSynchronizationManualDocumentCandidateItemDto(
                        $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                        $"{document.LineCount} satir / {document.TotalQuantity:0.##} miktar / hedef depo {document.RelatedWarehouseName}",
                        document.DocumentSerie,
                        document.DocumentOrderNo,
                        null,
                        document.DocumentDate,
                        document.DocumentNumber,
                        document.LineCount,
                        document.TotalQuantity))
                    .ToArray();
                notes.Add("AXATA C01 kaynak/cikis depo siparisleri Mikro listesinden okundu.");
                break;
            }
            case "company-receiving-sync":
            {
                var documents = await companyMovementListQueryExecutor.ExecuteAsync(
                    new CompanyMovementListRequest(warehouseNo, criteria.StartDate, criteria.EndDate),
                    CompanyMovementKind.IncomingShipment,
                    cancellationToken);
                totalRecordCount = documents.Count;
                items = documents
                    .Skip(criteria.Skip)
                    .Take(criteria.Take)
                    .Select(document => new AxataSynchronizationManualDocumentCandidateItemDto(
                        $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                        $"{document.CustomerDisplayName} / {document.LineCount} satir / {document.TotalQuantity:0.##} miktar",
                        document.DocumentSerie,
                        document.DocumentOrderNo,
                        null,
                        document.DocumentDate,
                        document.DocumentNo,
                        document.LineCount,
                        document.TotalQuantity))
                    .ToArray();
                notes.Add("Firma mal kabul belgeleri Mikro listesinden okundu.");
                break;
            }
            case "inventory-count-sync":
            {
                var documents = await inventoryCountListQueryExecutor.ExecuteAsync(
                    new InventoryCountListRequest(warehouseNo, criteria.StartDate, criteria.EndDate),
                    cancellationToken);
                totalRecordCount = documents.Count;
                items = documents
                    .Skip(criteria.Skip)
                    .Take(criteria.Take)
                    .Select(document => new AxataSynchronizationManualDocumentCandidateItemDto(
                        $"{document.DocumentNo} / {(document.DocumentDate ?? document.CreatedAt):yyyy-MM-dd}",
                        $"{document.Name} / {document.LineCount} satir / {document.TotalQuantity:0.##} miktar",
                        null,
                        null,
                        document.DocumentNo,
                        document.DocumentDate ?? document.CreatedAt.Date,
                        document.DocumentNo.ToString(),
                        document.LineCount,
                        document.TotalQuantity))
                    .ToArray();
                notes.Add("Sayim sonucu belgeleri Mikro listesinden okundu.");
                break;
            }
            default:
                throw new ArgumentException(
                    $"Task '{context.Definition.Code}' evrak bazli manuel listeleme endpoint'ini desteklemiyor.",
                    nameof(context.Definition.Code));
        }

        notes.Add("Donen alanlar tekil veya toplu preview/execute request body'sine dogrudan tasinabilir.");

        return new AxataSynchronizationManualDocumentCandidatesDto(
            context.Definition.Code,
            context.Definition.Name,
            context.Definition.Flow,
            warehouseNo,
            criteria.StartDate,
            criteria.EndDate,
            totalRecordCount,
            Math.Min(criteria.Skip, totalRecordCount),
            items.Count,
            DateTime.UtcNow,
            items,
            notes);
    }

    private async Task<AxataSynchronizationManualDocumentBatchDto> ExecuteBatchCoreAsync(
        AxataSynchronizationTaskExecutionContext context,
        IReadOnlyCollection<AxataSynchronizationManualDocumentInput> inputs,
        bool continueOnError,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            throw new ArgumentException("At least one document must be supplied for batch manual synchronization.");
        }

        var documents = new List<AxataSynchronizationManualDocumentDto>(inputs.Count);
        var failures = new List<AxataSynchronizationManualDocumentBatchFailureDto>();

        foreach (var input in inputs)
        {
            try
            {
                documents.Add(await ExecuteCoreAsync(context, input, cancellationToken));
            }
            catch (Exception exception)
            {
                if (!continueOnError)
                {
                    throw;
                }

                failures.Add(new AxataSynchronizationManualDocumentBatchFailureDto(
                    BuildDocumentReference(input),
                    exception.Message));
            }
        }

        var notes = new List<string>
        {
            continueOnError
                ? "Batch isleminde hatali evraklar kaydedildi ve kalan evraklarla devam edildi."
                : "Batch isleminde ilk hatada islem durdurulur."
        };

        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Outbox)
        {
            notes.Add("Her basarili evrak icin ayri outbox artifact'i yazilabilir.");
        }
        else
        {
            notes.Add("DryRun modunda batch sonucunda dosya yazilmaz.");
        }

        return new AxataSynchronizationManualDocumentBatchDto(
            context.Definition.Code,
            context.Definition.Name,
            context.Definition.Flow,
            context.ExecutionMode.ToExternalValue(),
            GetRequiredWarehouseNo(context),
            context.RequestedAtUtc,
            inputs.Count,
            documents.Count,
            failures.Count,
            documents,
            failures,
            notes);
    }

    private async Task<AxataSynchronizationManualDispatchBatchDto> DispatchBatchLiveCoreAsync(
        AxataSynchronizationTaskExecutionContext context,
        IReadOnlyCollection<AxataSynchronizationManualDocumentInput> inputs,
        bool continueOnError,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            throw new ArgumentException("At least one document must be supplied for batch live dispatch.");
        }

        var documents = new List<AxataSynchronizationManualDispatchDto>(inputs.Count);
        var failures = new List<AxataSynchronizationManualDocumentBatchFailureDto>();

        foreach (var input in inputs)
        {
            try
            {
                var result = await DispatchLiveCoreAsync(context, input, cancellationToken);
                documents.Add(result);

                if (!result.IsSuccess)
                {
                    failures.Add(new AxataSynchronizationManualDocumentBatchFailureDto(
                        result.DocumentReference,
                        result.ServiceMessage));

                    if (!continueOnError)
                    {
                        throw new InvalidOperationException(
                            $"AXATA live dispatch failed for '{result.DocumentReference}': {result.ServiceMessage}");
                    }
                }
            }
            catch (Exception exception)
            {
                if (!continueOnError)
                {
                    throw;
                }

                failures.Add(new AxataSynchronizationManualDocumentBatchFailureDto(
                    BuildDocumentReference(input),
                    exception.Message));
            }
        }

        var succeededCount = documents.Count(document => document.IsSuccess);
        var notes = new List<string>
        {
            "Bu endpoint payload'i outbox'a yazmak yerine eski AXATA worker kontratina gore canli SOAP dispatch yapar.",
            continueOnError
                ? "Batch dispatch'te hatali evraklar kaydedildi ve kalan evraklarla devam edildi."
                : "Batch dispatch'te ilk hata veya red cevabinda islem durdurulur."
        };

        return new AxataSynchronizationManualDispatchBatchDto(
            context.Definition.Code,
            context.Definition.Name,
            context.Definition.Flow,
            GetRequiredWarehouseNo(context),
            context.RequestedAtUtc,
            inputs.Count,
            succeededCount,
            failures.Count,
            documents,
            failures,
            notes);
    }

    private async Task<AxataSynchronizationManualDocumentDto> ExecuteCoreAsync(
        AxataSynchronizationTaskExecutionContext context,
        AxataSynchronizationManualDocumentInput input,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var notes = new List<string>
        {
            "Bu endpoint worker kuyruguna girmeden tek evrak bazli manuel kontrol icin calisir."
        };

        object payload;
        string documentReference;

        switch (context.Definition.Code)
        {
            case "issued-warehouse-order-sync":
            {
                var detailRequest = CreateWarehouseOrderRequest(input, warehouseNo);
                var detail = await warehouseOrderDetailQueryExecutor.ExecuteAsync(
                    detailRequest,
                    AxataWarehouseOrderDirection,
                    cancellationToken);

                payload = AxataSynchronizationPayloadFactory.BuildWarehouseOrderDocument(detail);
                documentReference = $"{detailRequest.DocumentSerie}.{detailRequest.DocumentOrderNo}";
                notes.Add("AXATA C01 kaynak/cikis depo siparisi Mikro'dan okunup payload formatina hazirlandi.");
                break;
            }
            case "company-receiving-sync":
            {
                var detailRequest = CreateCompanyMovementRequest(input, warehouseNo);
                var detail = await companyMovementDetailQueryExecutor.ExecuteAsync(
                    detailRequest,
                    CompanyMovementKind.IncomingShipment,
                    cancellationToken);

                payload = AxataSynchronizationPayloadFactory.BuildCompanyReceivingDocument(detail);
                documentReference = $"{detailRequest.DocumentSerie}.{detailRequest.DocumentOrderNo}";
                notes.Add("Firma mal kabul belgesi Mikro'dan okunup AXATA payload formatina hazirlandi.");
                break;
            }
            case "inventory-count-sync":
            {
                var detailRequest = CreateInventoryCountRequest(input, warehouseNo);
                var detail = await inventoryCountDetailQueryExecutor.ExecuteAsync(detailRequest, cancellationToken);

                payload = AxataSynchronizationPayloadFactory.BuildInventoryCountDocument(detail);
                documentReference = $"{detailRequest.DocumentNo} / {detailRequest.DocumentDate:yyyy-MM-dd}";
                notes.Add("Sayim sonucu belgesi Mikro'dan okunup AXATA payload formatina hazirlandi.");
                break;
            }
            default:
                throw new ArgumentException(
                    $"Task '{context.Definition.Code}' evrak bazli manuel kontrol endpoint'ini desteklemiyor.",
                    nameof(context.Definition.Code));
        }

        var artifacts = Array.Empty<AxataSynchronizationJobArtifactDto>();

        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Outbox)
        {
            artifacts = [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
            notes.Add("Payload outbox klasorune JSON artifact olarak yazildi.");
        }
        else
        {
            notes.Add("DryRun modunda sadece payload uretildi, dosya yazilmadi.");
        }

        return new AxataSynchronizationManualDocumentDto(
            context.Definition.Code,
            context.Definition.Name,
            context.Definition.Flow,
            context.ExecutionMode.ToExternalValue(),
            warehouseNo,
            documentReference,
            context.RequestedAtUtc,
            1,
            JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options),
            notes,
            artifacts);
    }

    private async Task<AxataSynchronizationManualDispatchDto> DispatchLiveCoreAsync(
        AxataSynchronizationTaskExecutionContext context,
        AxataSynchronizationManualDocumentInput input,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var notes = new List<string>
        {
            "Bu endpoint worker kuyruguna girmeden secili evraki canli AXATA SOAP servisine gonderir."
        };

        AxataLiveDispatchResult dispatchResult;
        string documentReference;

        switch (context.Definition.Code)
        {
            case "issued-warehouse-order-sync":
            {
                var detailRequest = CreateWarehouseOrderRequest(input, warehouseNo);
                var detail = await warehouseOrderDetailQueryExecutor.ExecuteAsync(
                    detailRequest,
                    AxataWarehouseOrderDirection,
                    cancellationToken);

                documentReference = $"{detailRequest.DocumentSerie}.{detailRequest.DocumentOrderNo}";
                dispatchResult = await liveTransportService.DispatchWarehouseOrderAsync(
                    context,
                    detail,
                    cancellationToken);
                notes.Add("AXATA C01 kaynak/cikis depo siparisi task bazli canli dispatch akisi ile gonderildi.");

                if (dispatchResult.IsSuccess)
                {
                    var markedLineCount = await MarkWarehouseOrderAsSentAsync(detailRequest, cancellationToken);
                    notes.Add(markedLineCount > 0
                        ? $"{markedLineCount} Mikro siparis satirinda ssip_special1=1 olarak isaretlendi."
                        : "UYARI: AXATA Success dondu ancak Mikro ssip_special1 bayragi icin eslesen satir bulunamadi.");
                }

                break;
            }
            case "company-receiving-sync":
            {
                var detailRequest = CreateCompanyMovementRequest(input, warehouseNo);
                var detail = await companyMovementDetailQueryExecutor.ExecuteAsync(
                    detailRequest,
                    CompanyMovementKind.IncomingShipment,
                    cancellationToken);

                documentReference = $"{detailRequest.DocumentSerie}.{detailRequest.DocumentOrderNo}";
                dispatchResult = await liveTransportService.DispatchCompanyReceivingAsync(
                    context,
                    detail,
                    cancellationToken);
                notes.Add("Firma mal kabul belgesi task bazli AXATA canli dispatch akisi ile gonderildi.");
                break;
            }
            case "inventory-count-sync":
                throw new NotSupportedException(
                    "Inventory count icin eski worker tarafinda canli Mikro -> AXATA push kontrati bulunmuyor; sadece AXATA -> Mikro duzeltme akisi tanimli.");
            default:
                throw new NotSupportedException(
                    $"Task '{context.Definition.Code}' icin canli AXATA dispatch akisi tanimli degil.");
        }

        notes.AddRange(dispatchResult.Notes);

        return new AxataSynchronizationManualDispatchDto(
            context.Definition.Code,
            context.Definition.Name,
            context.Definition.Flow,
            warehouseNo,
            documentReference,
            dispatchResult.OperationName,
            dispatchResult.EndpointUrl,
            context.RequestedAtUtc,
            dispatchResult.IsSuccess,
            dispatchResult.ServiceState,
            dispatchResult.ServiceMessage,
            dispatchResult.PayloadJson,
            dispatchResult.RequestXml,
            dispatchResult.ResponseXml,
            notes);
    }

    private async Task<int> MarkWarehouseOrderAsSentAsync(
        WarehouseOrderDetailRequest request,
        CancellationToken cancellationToken)
    {
        var documentSerie = request.DocumentSerie.Trim();
        var now = DateTime.Now;

        return await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order =>
                order.ssip_iptal != true &&
                order.ssip_evrakno_seri == documentSerie &&
                order.ssip_evrakno_sira == request.DocumentOrderNo &&
                order.ssip_cikdepo == request.WarehouseNo)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.ssip_special1, CompletedStatus)
                .SetProperty(order => order.ssip_lastup_user, MikroUserNo)
                .SetProperty(order => order.ssip_lastup_date, now),
                cancellationToken);
    }

    private static string BuildDocumentReference(AxataSynchronizationManualDocumentInput input)
    {
        if (!string.IsNullOrWhiteSpace(input.DocumentSerie) && input.DocumentOrderNo.HasValue)
        {
            return $"{input.DocumentSerie.Trim()}.{input.DocumentOrderNo.Value}";
        }

        if (input.DocumentNo.HasValue && input.DocumentDate.HasValue)
        {
            return $"{input.DocumentNo.Value} / {input.DocumentDate.Value:yyyy-MM-dd}";
        }

        if (input.DocumentNo.HasValue)
        {
            return input.DocumentNo.Value.ToString();
        }

        return "unknown-document";
    }

    private static WarehouseOrderDetailRequest CreateWarehouseOrderRequest(
        AxataSynchronizationManualDocumentInput input,
        int warehouseNo)
    {
        if (string.IsNullOrWhiteSpace(input.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required for issued warehouse order sync.", nameof(input));
        }

        if (!input.DocumentOrderNo.HasValue || input.DocumentOrderNo.Value < 0)
        {
            throw new ArgumentException("Document order no must be zero or greater for issued warehouse order sync.", nameof(input));
        }

        return new WarehouseOrderDetailRequest(
            warehouseNo,
            input.DocumentSerie.Trim(),
            input.DocumentOrderNo.Value);
    }

    private static CompanyMovementDetailRequest CreateCompanyMovementRequest(
        AxataSynchronizationManualDocumentInput input,
        int warehouseNo)
    {
        if (string.IsNullOrWhiteSpace(input.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required for company receiving sync.", nameof(input));
        }

        if (!input.DocumentOrderNo.HasValue || input.DocumentOrderNo.Value < 0)
        {
            throw new ArgumentException("Document order no must be zero or greater for company receiving sync.", nameof(input));
        }

        return new CompanyMovementDetailRequest(
            warehouseNo,
            input.DocumentSerie.Trim(),
            input.DocumentOrderNo.Value);
    }

    private static InventoryCountDetailRequest CreateInventoryCountRequest(
        AxataSynchronizationManualDocumentInput input,
        int warehouseNo)
    {
        if (!input.DocumentNo.HasValue || input.DocumentNo.Value < 0)
        {
            throw new ArgumentException("Document no must be zero or greater for inventory count sync.", nameof(input));
        }

        if (!input.DocumentDate.HasValue)
        {
            throw new ArgumentException("Document date is required for inventory count sync.", nameof(input));
        }

        return new InventoryCountDetailRequest(
            warehouseNo,
            input.DocumentNo.Value,
            input.DocumentDate.Value);
    }

    private static int GetRequiredWarehouseNo(AxataSynchronizationTaskExecutionContext context) =>
        context.WarehouseNo is > 0
            ? context.WarehouseNo.Value
            : throw new ArgumentException("Warehouse number is required for manual AXATA synchronization.");
}

internal sealed record AxataSynchronizationManualDocumentInput(
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? DocumentNo,
    DateTime? DocumentDate);

internal sealed record AxataSynchronizationManualDocumentCandidateCriteria(
    DateTime StartDate,
    DateTime EndDate,
    int Skip,
    int Take);
