using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataOutboundDeliveryImportService(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    MikroWriteDbContext mikroWriteDbContext,
    ICreateInterWarehouseShipmentUseCase createInterWarehouseShipmentUseCase)
    : IAxataOutboundDeliveryImportService,
        IAxataIntegrationAuditService
{
    private const string PendingStatus = "0";
    private const string CompletedStatus = "1";
    private const string C01MovementType = "C01";
    private const string C02MovementType = "C02";
    private const string C03MovementType = "C03";
    private const string C04LegacyMovementType = "C4";
    private const string CompanyCode = "01";
    private const string WarehouseCode = "01";
    private const int TransitWarehouseNo = 60;
    private const int DefaultTake = 20;
    private const int MaxTake = 200;
    private const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string ServiceNamespace = "http://tempuri.org/";
    private const string AxataWmsNamespace = "http://axatawms";
    private const string MainContractName = "IAxataServicePool";
    private const string ExtContractName = "IAxataServicePoolEXT";
    private const string FetchOperationName = "getOutBoundDeliveryList";
    private const string AckOperationName = "updIntegrationTable";
    private const int DefaultWarehouseOrderSourceWarehouseNo = 50;
    private const double QuantityTolerance = 0.000001d;

    private static readonly IReadOnlyCollection<string> AuditMovementTypes =
    [
        C01MovementType,
        C02MovementType,
        C03MovementType,
        C04LegacyMovementType
    ];

    public async Task<AxataOutboundDeliveryQueuePreviewDto> PreviewOutboundDeliveriesAsync(
        AxataOutboundDeliveryQueuePreviewRequest request,
        CancellationToken cancellationToken)
    {
        var movementType = ResolveOutboundDeliveryMovementType(request.MovementType);
        var take = NormalizeTake(request.Take);
        var documents = await FetchPendingOutboundDeliveriesAsync(movementType, cancellationToken);
        var selectedDocuments = documents
            .Take(take)
            .ToArray();
        var hasLiveImport = movementType.Equals(C01MovementType, StringComparison.OrdinalIgnoreCase);

        return new AxataOutboundDeliveryQueuePreviewDto(
            movementType,
            PendingStatus,
            DateTime.UtcNow,
            documents.Count,
            selectedDocuments.Length,
            selectedDocuments.Sum(document => document.Lines.Count),
            selectedDocuments.Sum(document => document.Lines.Sum(line => line.Quantity)),
            selectedDocuments.Select(document => ToQueuePreviewDocumentDto(document, hasLiveImport)).ToArray(),
            hasLiveImport
                ? [
                    "AXATA outbound delivery kuyrugu canli servisten okundu.",
                    "C01 icin detayli Mikro eslesme ve import kontrolu live/axata/outbound-deliveries/c01/preview endpoint'inde yapilir."
                ]
                : [
                    "AXATA outbound delivery kuyrugu canli servisten okundu.",
                    "Bu hareket tipi icin su an sadece kuyruk preview vardir; Mikro'ya yazma ve AXATA ack akisi acik degildir."
                ]);
    }

    public async Task<AxataIntegrationAuditDto> GetOverviewAsync(
        AxataIntegrationAuditRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDate) = ResolveDateRange(request.StartDate, request.EndDate);
        var take = NormalizeTake(request.Take);
        var warehouseOrderSourceWarehouseNos = ResolveWarehouseOrderSourceWarehouseNos(request.WarehouseNo);
        var warehouseAudit = await GetWarehouseOrderAuditAsync(
            startDate,
            endDate,
            warehouseOrderSourceWarehouseNos,
            take,
            cancellationToken);

        var pendingDocuments = new List<AxataOutboundDeliveryDocument>();
        var movementDocuments = new Dictionary<string, IReadOnlyCollection<AxataOutboundDeliveryDocument>>(
            StringComparer.OrdinalIgnoreCase);
        var movementFetchErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var movementType in AuditMovementTypes)
        {
            var fetchResult = await TryFetchPendingOutboundDeliveriesForAuditAsync(movementType, cancellationToken);
            if (!fetchResult.IsSuccess)
            {
                movementDocuments[movementType] = Array.Empty<AxataOutboundDeliveryDocument>();
                movementFetchErrors[movementType] = fetchResult.ErrorMessage
                    ?? "AXATA fetch failed.";
                continue;
            }

            var documents = fetchResult.Documents;
            var filteredDocuments = documents
                .Where(document => IsInsideDateRange(document.AxataDate, startDate, endDate))
                .ToArray();

            movementDocuments[movementType] = filteredDocuments;
            pendingDocuments.AddRange(filteredDocuments);
        }

        var c01Analyses = await AnalyzeC01DocumentsAsync(
            movementDocuments.TryGetValue(C01MovementType, out var c01Documents)
                ? c01Documents
                : Array.Empty<AxataOutboundDeliveryDocument>(),
            cancellationToken);

        var c01PendingDocuments = c01Analyses
            .Select(ToPendingOutboundDeliveryDto)
            .ToArray();

        var pendingDeliveryDtos = new List<AxataPendingOutboundDeliveryDto>(pendingDocuments.Count);
        pendingDeliveryDtos.AddRange(c01PendingDocuments);

        foreach (var movementGroup in movementDocuments.Where(item => item.Key != C01MovementType))
        {
            pendingDeliveryDtos.AddRange(movementGroup.Value.Select(document => ToQueueOnlyPendingDto(document)));
        }

        var movementSummaries = AuditMovementTypes
            .Select(movementType =>
            {
                var movementPending = pendingDeliveryDtos
                    .Where(document => document.MovementType.Equals(movementType, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var hasFetchError = movementFetchErrors.TryGetValue(movementType, out var fetchError);

                return new AxataOutboundDeliveryMovementSummaryDto(
                    movementType,
                    hasFetchError ? "FetchFailed" : PendingStatus,
                    movementPending.Length,
                    movementPending.Sum(document => document.LineCount),
                    movementPending.Sum(document => document.Quantity),
                    movementPending.Count(document => document.ExistingLinkedMovementLineCount == 0),
                    movementPending.Count(document => document.MikroCheckState == "MikroShipmentExistsPendingAck"),
                    hasFetchError
                        ? $"AXATA fetch failed: {fetchError}"
                        : movementType == C01MovementType
                        ? "C01 order and shipment link check"
                        : "AXATA pending queue only");
            })
            .ToArray();

        var c01MissingInMikroDocumentCount = c01PendingDocuments.Count(document =>
            document.ExistingLinkedMovementLineCount == 0);
        var c01MikroExistsPendingAckDocumentCount = c01PendingDocuments.Count(document =>
            document.MikroCheckState == "MikroShipmentExistsPendingAck");
        var summary = new AxataIntegrationAuditSummaryDto(
            warehouseAudit.DocumentCount,
            warehouseAudit.SentDocumentCount,
            warehouseAudit.PartiallySentDocumentCount,
            warehouseAudit.UnsentDocumentCount,
            warehouseAudit.SentMissingMikroShipmentDocumentCount,
            warehouseAudit.SentMissingMikroShipmentLineCount,
            warehouseAudit.SentMissingMikroShipmentQuantity,
            pendingDeliveryDtos.Count,
            pendingDeliveryDtos.Sum(document => document.LineCount),
            pendingDeliveryDtos.Sum(document => document.Quantity),
            c01PendingDocuments.Length,
            c01MissingInMikroDocumentCount,
            c01MikroExistsPendingAckDocumentCount);

        var interventionCandidates = pendingDeliveryDtos
            .Where(document => document.CanIntervene)
            .OrderBy(document => document.AxataSequenceNo)
            .Take(take)
            .ToArray();
        var returnedPendingDocuments = pendingDeliveryDtos
            .OrderBy(document => document.MovementType)
            .ThenBy(document => document.AxataSequenceNo)
            .Take(take)
            .ToArray();
        var isInSync = summary.UnsentWarehouseOrderDocumentCount == 0 &&
                       summary.PartiallySentWarehouseOrderDocumentCount == 0 &&
                       summary.SentWarehouseOrderMissingMikroShipmentDocumentCount == 0 &&
                       summary.PendingOutboundDeliveryDocumentCount == 0;
        var operations = BuildAuditOperations(summary);
        var notes = new List<string>
        {
            $"Siparis kontrolu sadece AXATA kaynak/cikis depo(lar)i ({FormatWarehouseNos(warehouseOrderSourceWarehouseNos)}) icin ssip_cikdepo uzerinden yapilir.",
            "Mikro depolar arasi siparislerdeki ssip_special1 worker basari bayragi kontrol edilir.",
            "ssip_special1=1 olan depolar arasi siparislerde STOK_HAREKETLERI_EK.sth_subesip_uid sevk linki yoksa AXATA'dan Mikro'ya donus sevki eksik kabul edilir.",
            "Sevk kontrolu AXATA getOutBoundDeliveryListAsync status 0 kuyrugundan okunur.",
            "C01 icin Mikro siparis satiri ve STOK_HAREKETLERI_EK linki kontrol edilir; diger hareket tipleri bu raporda kuyruk seviyesinde izlenir.",
            "AXATA servisi tarih filtresi almadigi icin tarih parse edilemeyen pending sevkler rapora dahil edilir."
        };
        notes.AddRange(movementFetchErrors.Select(error =>
            $"AXATA {error.Key} pending sevk kuyrugu okunamadi: {error.Value}"));

        return new AxataIntegrationAuditDto(
            isInSync,
            DateTime.UtcNow,
            startDate,
            endDate,
            request.WarehouseNo,
            summary,
            movementSummaries,
            warehouseAudit.UnsyncedDocuments,
            warehouseAudit.SentMissingMikroShipmentDocuments,
            returnedPendingDocuments,
            interventionCandidates,
            operations,
            notes);
    }

    public async Task<AxataOutboundDeliveryImportPreviewDto> PreviewC01Async(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var documents = await FetchPendingOutboundDeliveriesAsync(C01MovementType, cancellationToken);
        var selectedDocuments = documents.Take(take).ToArray();
        var analyses = await AnalyzeC01DocumentsAsync(selectedDocuments, cancellationToken);

        return new AxataOutboundDeliveryImportPreviewDto(
            C01MovementType,
            PendingStatus,
            DateTime.UtcNow,
            documents.Count,
            analyses.Count,
            selectedDocuments.Sum(document => document.Lines.Count),
            selectedDocuments.Sum(document => document.Lines.Sum(line => line.Quantity)),
            analyses.Select(analysis => analysis.ImportDto).ToArray(),
            [
                "AXATA C01 teslimatlar getOutBoundDeliveryListAsync ile Status=0 olarak okunur.",
                "CanImport=true olan belgeler Mikro siparis satirina birebir eslesmistir ve henuz fis linki yoktur.",
                "Mikro fis linki zaten varsa tekrar fis uretilmez; execute sirasinda uygun durumda sadece AXATA ack yapilabilir."
            ]);
    }

    public async Task<AxataOutboundDeliveryImportExecuteDto> ExecuteC01Async(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var documents = await FetchPendingOutboundDeliveriesAsync(C01MovementType, cancellationToken);
        var selectedDocuments = documents.Take(take).ToArray();
        var analyses = await AnalyzeC01DocumentsAsync(selectedDocuments, cancellationToken);
        var results = new List<AxataOutboundDeliveryImportResultDto>(analyses.Count);
        var failures = new List<AxataOutboundDeliveryImportFailureDto>();
        var skippedDocumentCount = 0;

        foreach (var analysis in analyses)
        {
            try
            {
                if (analysis.ImportDto.CanImport)
                {
                    var shipmentResponse = await createInterWarehouseShipmentUseCase.ExecuteAsync(
                        BuildCreateShipmentRequest(analysis),
                        cancellationToken);
                    var acknowledged = false;

                    if (request.Acknowledge)
                    {
                        await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                        acknowledged = true;
                    }

                    results.Add(new AxataOutboundDeliveryImportResultDto(
                        analysis.Document.AxataSequenceNo,
                        analysis.Document.AxataDeliveryNo,
                        analysis.Document.DocumentSerie,
                        analysis.Document.DocumentOrderNo ?? 0,
                        shipmentResponse.DocumentSerie,
                        shipmentResponse.DocumentOrderNo,
                        shipmentResponse.LineCount,
                        shipmentResponse.TotalQuantity,
                        acknowledged,
                        acknowledged
                            ? "Mikro sevk fisi olusturuldu ve AXATA ENT006.S06STAT=1 yapildi."
                            : "Mikro sevk fisi olusturuldu; AXATA ack istenmedigi icin status degistirilmedi."));

                    continue;
                }

                if (request.Acknowledge && CanAcknowledgeExistingMikroShipment(analysis))
                {
                    await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                    skippedDocumentCount++;
                    results.Add(new AxataOutboundDeliveryImportResultDto(
                        analysis.Document.AxataSequenceNo,
                        analysis.Document.AxataDeliveryNo,
                        analysis.Document.DocumentSerie,
                        analysis.Document.DocumentOrderNo ?? 0,
                        string.Empty,
                        0,
                        0,
                        0d,
                        true,
                        "Mikro sevk linki zaten vardi; duplicate fis olusturulmadan AXATA ENT006.S06STAT=1 yapildi."));

                    continue;
                }

                skippedDocumentCount++;
                failures.Add(new AxataOutboundDeliveryImportFailureDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.ImportDto.Warning ?? "C01 delivery can not be imported safely."));

                if (!request.ContinueOnError)
                {
                    break;
                }
            }
            catch (Exception exception)
            {
                failures.Add(new AxataOutboundDeliveryImportFailureDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    exception.Message));

                if (!request.ContinueOnError)
                {
                    break;
                }
            }
        }

        return new AxataOutboundDeliveryImportExecuteDto(
            C01MovementType,
            PendingStatus,
            DateTime.UtcNow,
            analyses.Count,
            results.Count(result => result.CreatedMovementLineCount > 0 || result.Acknowledged),
            failures.Count,
            skippedDocumentCount,
            results.Sum(result => result.CreatedMovementLineCount),
            results.Sum(result => result.CreatedMovementQuantity),
            results,
            failures,
            [
                "AXATA ack islemi Mikro sevk fisi basariyla olustuktan sonra yapilir.",
                "Mikro fis linki mevcutsa duplicate fis olusturulmaz.",
                $"Talep eden kullanici: {requestedByUserId}"
            ]);
    }

    public async Task<AxataOutboundDeliveryImportPreviewDto> PreviewC01DocumentAsync(
        AxataOutboundDeliveryDocumentImportPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var analysis = await GetC01DocumentAnalysisAsync(
            request.DocumentSerie,
            request.DocumentOrderNo,
            request.Status,
            cancellationToken);

        return new AxataOutboundDeliveryImportPreviewDto(
            C01MovementType,
            analysis.Document.Status,
            DateTime.UtcNow,
            1,
            1,
            analysis.Document.Lines.Count,
            analysis.Document.Lines.Sum(line => line.Quantity),
            [analysis.ImportDto],
            [
                $"AXATA C01 teslimat belge bazinda arandi: {FormatAxataDocumentNo(request.DocumentSerie, request.DocumentOrderNo)}.",
                string.IsNullOrWhiteSpace(request.Status)
                    ? "Status belirtilmedigi icin once 0, sonra 1 denendi."
                    : $"AXATA status {request.Status.Trim()} ile arandi.",
                "CanImport=true ise Mikro sevk fisi linki eksiktir ve satir/miktar eslesmesi uygundur."
            ]);
    }

    public async Task<AxataOutboundDeliveryImportExecuteDto> ExecuteC01DocumentAsync(
        AxataOutboundDeliveryDocumentImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var analysis = await GetC01DocumentAnalysisAsync(
            request.DocumentSerie,
            request.DocumentOrderNo,
            request.Status,
            cancellationToken);
        var results = new List<AxataOutboundDeliveryImportResultDto>();
        var failures = new List<AxataOutboundDeliveryImportFailureDto>();
        var skippedDocumentCount = 0;

        try
        {
            if (analysis.ImportDto.CanImport)
            {
                var shipmentResponse = await createInterWarehouseShipmentUseCase.ExecuteAsync(
                    BuildCreateShipmentRequest(analysis),
                    cancellationToken);
                var acknowledged = false;

                if (request.Acknowledge)
                {
                    await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                    acknowledged = true;
                }

                results.Add(new AxataOutboundDeliveryImportResultDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.Document.DocumentSerie,
                    analysis.Document.DocumentOrderNo ?? 0,
                    shipmentResponse.DocumentSerie,
                    shipmentResponse.DocumentOrderNo,
                    shipmentResponse.LineCount,
                    shipmentResponse.TotalQuantity,
                    acknowledged,
                    acknowledged
                        ? "Belge bazli Mikro sevk fisi olusturuldu ve AXATA ENT006.S06STAT=1 yapildi."
                        : "Belge bazli Mikro sevk fisi olusturuldu; AXATA status degistirilmedi."));
            }
            else if (request.Acknowledge && CanAcknowledgeExistingMikroShipment(analysis))
            {
                await AcknowledgeOutboundDeliveryAsync(analysis.Document.AxataSequenceNo, cancellationToken);
                skippedDocumentCount++;
                results.Add(new AxataOutboundDeliveryImportResultDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.Document.DocumentSerie,
                    analysis.Document.DocumentOrderNo ?? 0,
                    string.Empty,
                    0,
                    0,
                    0d,
                    true,
                    "Mikro sevk linki zaten vardi; duplicate fis olusturulmadan AXATA ENT006.S06STAT=1 yapildi."));
            }
            else
            {
                skippedDocumentCount++;
                failures.Add(new AxataOutboundDeliveryImportFailureDto(
                    analysis.Document.AxataSequenceNo,
                    analysis.Document.AxataDeliveryNo,
                    analysis.ImportDto.Warning ?? "C01 delivery can not be imported safely."));
            }
        }
        catch (Exception exception)
        {
            failures.Add(new AxataOutboundDeliveryImportFailureDto(
                analysis.Document.AxataSequenceNo,
                analysis.Document.AxataDeliveryNo,
                exception.Message));
        }

        return new AxataOutboundDeliveryImportExecuteDto(
            C01MovementType,
            analysis.Document.Status,
            DateTime.UtcNow,
            1,
            results.Count(result => result.CreatedMovementLineCount > 0 || result.Acknowledged),
            failures.Count,
            skippedDocumentCount,
            results.Sum(result => result.CreatedMovementLineCount),
            results.Sum(result => result.CreatedMovementQuantity),
            results,
            failures,
            [
                $"Belge bazli rescue: {FormatAxataDocumentNo(request.DocumentSerie, request.DocumentOrderNo)}.",
                "Mikro'ya yazim sadece AXATA teslimat detaylari Mikro siparis satirlariyla birebir eslesirse yapilir.",
                $"Talep eden kullanici: {requestedByUserId}"
            ]);
    }

    private int[] ResolveWarehouseOrderSourceWarehouseNos(int? requestedWarehouseNo)
    {
        var configuredWarehouseNos = options.CurrentValue.WarehouseOrderAutomation.WarehouseNos
            .Where(warehouseNo => warehouseNo > 0)
            .Distinct()
            .OrderBy(warehouseNo => warehouseNo)
            .ToArray();

        if (configuredWarehouseNos.Length == 0)
        {
            configuredWarehouseNos = [DefaultWarehouseOrderSourceWarehouseNo];
        }

        if (requestedWarehouseNo is > 0)
        {
            return configuredWarehouseNos.Contains(requestedWarehouseNo.Value)
                ? [requestedWarehouseNo.Value]
                : Array.Empty<int>();
        }

        return configuredWarehouseNos;
    }

    private async Task<WarehouseOrderAuditResult> GetWarehouseOrderAuditAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<int> sourceWarehouseNos,
        int take,
        CancellationToken cancellationToken)
    {
        var endDateExclusive = endDate.Date.AddDays(1);
        var sourceWarehouseNoArray = sourceWarehouseNos
            .Where(warehouseNo => warehouseNo > 0)
            .Distinct()
            .ToArray();

        if (sourceWarehouseNoArray.Length == 0)
        {
            return new WarehouseOrderAuditResult(
                0,
                0,
                0,
                0,
                0,
                0,
                0d,
                Array.Empty<AxataSentWarehouseOrderMissingShipmentDto>(),
                Array.Empty<AxataUnsyncedWarehouseOrderDto>());
        }

        var query = mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_iptal != true &&
                order.ssip_tarih.HasValue &&
                order.ssip_tarih.Value >= startDate.Date &&
                order.ssip_tarih.Value < endDateExclusive &&
                order.ssip_evrakno_seri != null &&
                order.ssip_evrakno_sira.HasValue &&
                sourceWarehouseNoArray.Contains(order.ssip_cikdepo ?? 0));

        var rows = await query
            .Select(order => new WarehouseOrderAuditRow(
                order.ssip_evrakno_seri ?? string.Empty,
                order.ssip_evrakno_sira ?? 0,
                order.ssip_tarih ?? DateTime.MinValue,
                order.ssip_girdepo ?? 0,
                order.ssip_cikdepo ?? 0,
                order.ssip_satirno ?? 0,
                order.ssip_Guid,
                order.ssip_miktar ?? 0d,
                order.ssip_teslim_miktar ?? 0d,
                order.ssip_special1 ?? string.Empty,
                order.ssip_lastup_date))
            .ToListAsync(cancellationToken);

        var movementLinkCounts = await GetWarehouseOrderMovementLinkCountsAsync(
            rows.Select(row => row.OrderLineGuid).Distinct().ToArray(),
            cancellationToken);

        var documents = rows
            .GroupBy(row => new
            {
                row.DocumentSerie,
                row.DocumentOrderNo,
                DocumentDate = row.DocumentDate.Date,
                row.InWarehouseNo,
                row.OutWarehouseNo
            })
            .Select(group =>
            {
                var sentLines = group
                    .Where(row => IsAxataSentFlag(row.Special1))
                    .ToArray();
                var lineCount = group.Count();
                var sentLineCount = sentLines.Length;
                var state = sentLineCount == lineCount
                    ? "Sent"
                    : sentLineCount > 0
                        ? "PartiallySent"
                        : "NotSent";

                return new AxataUnsyncedWarehouseOrderDto(
                    group.Key.DocumentSerie,
                    group.Key.DocumentOrderNo,
                    group.Key.DocumentDate,
                    group.Key.InWarehouseNo,
                    group.Key.OutWarehouseNo,
                    lineCount,
                    sentLineCount,
                    lineCount - sentLineCount,
                    group.Sum(row => row.Quantity),
                    sentLines.Sum(row => row.Quantity),
                    group.Where(row => !IsAxataSentFlag(row.Special1)).Sum(row => row.Quantity),
                    state,
                    group.Max(row => row.LastUpdateDate),
                    state == "Sent"
                        ? string.Empty
                        : "Mikro ssip_special1 worker basari bayragi tum satirlarda 1 degil.");
            })
            .OrderBy(document => document.DocumentDate)
            .ThenBy(document => document.DocumentSerie)
            .ThenBy(document => document.DocumentOrderNo)
            .ToArray();

        var sentMissingMikroShipmentDocuments = rows
            .GroupBy(row => new
            {
                row.DocumentSerie,
                row.DocumentOrderNo,
                DocumentDate = row.DocumentDate.Date,
                row.InWarehouseNo,
                row.OutWarehouseNo
            })
            .Select(group =>
            {
                var sentRows = group
                    .Where(row => IsAxataSentFlag(row.Special1))
                    .ToArray();
                var missingRows = sentRows
                    .Where(row => movementLinkCounts.GetValueOrDefault(row.OrderLineGuid) == 0)
                    .ToArray();
                var linkedMovementLineCount = group
                    .Sum(row => movementLinkCounts.GetValueOrDefault(row.OrderLineGuid));

                return new AxataSentWarehouseOrderMissingShipmentDto(
                    group.Key.DocumentSerie,
                    group.Key.DocumentOrderNo,
                    group.Key.DocumentDate,
                    group.Key.InWarehouseNo,
                    group.Key.OutWarehouseNo,
                    group.Count(),
                    sentRows.Length,
                    missingRows.Length,
                    group.Sum(row => row.Quantity),
                    sentRows.Sum(row => row.Quantity),
                    missingRows.Sum(row => row.Quantity),
                    group.Sum(row => row.DeliveredQuantity),
                    linkedMovementLineCount,
                    "SentToAxataMissingMikroShipment",
                    group.Max(row => row.LastUpdateDate),
                    "Siparis AXATA'ya gonderildi olarak isaretli, ancak Mikro sevk fisi linki STOK_HAREKETLERI_EK.sth_subesip_uid uzerinden bulunamadi.");
            })
            .Where(document => document.MissingMovementLinkLineCount > 0)
            .OrderBy(document => document.DocumentDate)
            .ThenBy(document => document.DocumentSerie)
            .ThenBy(document => document.DocumentOrderNo)
            .ToArray();

        var unsyncedDocuments = documents
            .Where(document => document.State != "Sent")
            .Take(take)
            .ToArray();

        return new WarehouseOrderAuditResult(
            documents.Length,
            documents.Count(document => document.State == "Sent"),
            documents.Count(document => document.State == "PartiallySent"),
            documents.Count(document => document.State == "NotSent"),
            sentMissingMikroShipmentDocuments.Length,
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkLineCount),
            sentMissingMikroShipmentDocuments.Sum(document => document.MissingMovementLinkQuantity),
            sentMissingMikroShipmentDocuments.Take(take).ToArray(),
            unsyncedDocuments);
    }

    private static string FormatWarehouseNos(IReadOnlyCollection<int> warehouseNos) =>
        warehouseNos.Count == 0
            ? "AXATA kaynak/cikis depo yok"
            : string.Join(", ", warehouseNos.OrderBy(warehouseNo => warehouseNo));

    private static IReadOnlyCollection<AxataIntegrationAuditOperationDto> BuildAuditOperations(
        AxataIntegrationAuditSummaryDto summary)
    {
        var unsentDocumentCount = summary.UnsentWarehouseOrderDocumentCount +
                                  summary.PartiallySentWarehouseOrderDocumentCount;
        var pendingOutboundDocumentCount = summary.PendingOutboundDeliveryDocumentCount;
        var missingMikroShipmentDocumentCount = summary.SentWarehouseOrderMissingMikroShipmentDocumentCount;

        return
        [
            new AxataIntegrationAuditOperationDto(
                "warehouse-orders-not-sent-to-axata",
                "Mikro siparis AXATA gonderim kontrolu",
                unsentDocumentCount == 0 ? "Ok" : "ActionRequired",
                unsentDocumentCount == 0 ? "Success" : "Warning",
                unsentDocumentCount,
                0,
                0d,
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/candidates",
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/preview-batch",
                "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/dispatch-batch",
                true,
                true,
                "ssip_special1 basari bayragi eksik olan depolar arasi siparisler AXATA'ya manuel tekrar gonderilebilir."),
            new AxataIntegrationAuditOperationDto(
                "axata-pending-outbound-deliveries",
                "AXATA bekleyen sevk kuyrugu",
                pendingOutboundDocumentCount == 0 ? "Ok" : "ActionRequired",
                pendingOutboundDocumentCount == 0 ? "Success" : "Warning",
                pendingOutboundDocumentCount,
                summary.PendingOutboundDeliveryLineCount,
                summary.PendingOutboundDeliveryQuantity,
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/preview?movementType={C01|C02|C03|C4}",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/preview",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/import",
                pendingOutboundDocumentCount > 0,
                true,
                "AXATA Status=0 sevk kuyrugu izlenir; canli Mikro import/ack su an C01 icin aciktir."),
            new AxataIntegrationAuditOperationDto(
                "sent-to-axata-missing-mikro-shipment",
                "AXATA sevk kesilmis Mikro donus eksik",
                missingMikroShipmentDocumentCount == 0 ? "Ok" : "ActionRequired",
                missingMikroShipmentDocumentCount == 0 ? "Success" : "Critical",
                missingMikroShipmentDocumentCount,
                summary.SentWarehouseOrderMissingMikroShipmentLineCount,
                summary.SentWarehouseOrderMissingMikroShipmentQuantity,
                "/api/integrations/axata-sync/live/audit/overview#sentWarehouseOrdersMissingMikroShipments",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo}/preview",
                "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo}/import",
                true,
                true,
                "ssip_special1=1 olan ama STOK_HAREKETLERI_EK sevk linki bulunmayan belgeler listelenir; belge bazli C01 rescue AXATA teslimat detayini bulursa Mikro sevk fisini olusturur.")
        ];
    }

    private async Task<C01DeliveryAnalysis> GetC01DocumentAnalysisAsync(
        string documentSerie,
        int documentOrderNo,
        string? status,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentSerie = NormalizeRequiredDocumentSerie(documentSerie);
        if (documentOrderNo <= 0)
        {
            throw new ArgumentException("Document order no must be greater than zero.", nameof(documentOrderNo));
        }

        var axataDocumentNo = FormatAxataDocumentNo(normalizedDocumentSerie, documentOrderNo);
        var statuses = ResolveOutboundDeliveryStatuses(status);
        var fetchedDocuments = new List<AxataOutboundDeliveryDocument>();

        foreach (var candidateStatus in statuses)
        {
            var documents = await FetchOutboundDeliveriesAsync(
                C01MovementType,
                candidateStatus,
                axataDocumentNo,
                cancellationToken);
            var matches = documents
                .Where(document => MatchesAxataDocument(document, normalizedDocumentSerie, documentOrderNo))
                .ToArray();

            fetchedDocuments.AddRange(matches);

            if (matches.Length > 0)
            {
                break;
            }
        }

        var selectedDocument = fetchedDocuments
            .OrderBy(document => document.Status == PendingStatus ? 0 : 1)
            .ThenBy(document => document.AxataSequenceNo)
            .FirstOrDefault();

        if (selectedDocument is null)
        {
            throw new KeyNotFoundException(
                $"AXATA C01 delivery was not found for document {axataDocumentNo} with status {FormatStatuses(statuses)}.");
        }

        var analyses = await AnalyzeC01DocumentsAsync([selectedDocument], cancellationToken);
        return analyses.Single();
    }

    private async Task<IReadOnlyCollection<C01DeliveryAnalysis>> AnalyzeC01DocumentsAsync(
        IReadOnlyCollection<AxataOutboundDeliveryDocument> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
        {
            return Array.Empty<C01DeliveryAnalysis>();
        }

        var parsedDocuments = documents
            .Where(document => document.DocumentOrderNo.HasValue && !string.IsNullOrWhiteSpace(document.DocumentSerie))
            .ToArray();
        var orderLines = Array.Empty<DEPOLAR_ARASI_SIPARISLER>();

        if (parsedDocuments.Length > 0)
        {
            var documentSeries = parsedDocuments
                .Select(document => document.DocumentSerie)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var documentOrderNos = parsedDocuments
                .Select(document => document.DocumentOrderNo!.Value)
                .Distinct()
                .ToArray();

            orderLines = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
                .AsNoTracking()
                .Where(order =>
                    order.ssip_iptal != true &&
                    order.ssip_evrakno_seri != null &&
                    documentSeries.Contains(order.ssip_evrakno_seri) &&
                    order.ssip_evrakno_sira.HasValue &&
                    documentOrderNos.Contains(order.ssip_evrakno_sira.Value))
                .ToArrayAsync(cancellationToken);
        }

        var movementLinkCounts = await GetMovementLinkCountsAsync(orderLines, cancellationToken);
        var orderLinesByDocument = orderLines
            .GroupBy(order => new MikroDocumentKey(order.ssip_evrakno_seri ?? string.Empty, order.ssip_evrakno_sira ?? 0))
            .ToDictionary(
                group => group.Key,
                group => group.ToArray());

        return documents
            .Select(document =>
            {
                var documentOrderLines = document.DocumentOrderNo.HasValue
                    ? orderLinesByDocument.GetValueOrDefault(new MikroDocumentKey(document.DocumentSerie, document.DocumentOrderNo.Value))
                      ?? Array.Empty<DEPOLAR_ARASI_SIPARISLER>()
                    : Array.Empty<DEPOLAR_ARASI_SIPARISLER>();
                var positiveLines = document.Lines
                    .Where(line => line.Quantity > 0d)
                    .ToArray();
                var matchedLines = MatchC01Lines(document, documentOrderLines, positiveLines);
                var existingLinkedMovementLineCount = documentOrderLines
                    .Sum(order => movementLinkCounts.GetValueOrDefault(order.ssip_Guid));
                var warning = BuildC01Warning(
                    document,
                    documentOrderLines,
                    positiveLines,
                    matchedLines,
                    existingLinkedMovementLineCount);
                var canImport = string.IsNullOrWhiteSpace(warning);
                var mikroCheckState = ResolveC01MikroCheckState(
                    documentOrderLines,
                    positiveLines,
                    matchedLines,
                    existingLinkedMovementLineCount,
                    canImport);

                return new C01DeliveryAnalysis(
                    document,
                    new AxataOutboundDeliveryImportDocumentDto(
                        document.AxataSequenceNo,
                        document.AxataDeliveryNo,
                        document.DocumentSerie,
                        document.DocumentOrderNo ?? 0,
                        document.MovementType,
                        document.Status,
                        document.SourceWarehouseNo,
                        document.TargetWarehouseNo,
                        document.AxataDate,
                        document.Lines.Count,
                        document.Lines.Sum(line => line.Quantity),
                        documentOrderLines.Length,
                        documentOrderLines.Sum(order => order.ssip_miktar ?? 0d),
                        documentOrderLines.Sum(order => order.ssip_teslim_miktar ?? 0d),
                        existingLinkedMovementLineCount,
                        canImport,
                        warning),
                    matchedLines,
                    mikroCheckState);
            })
            .ToArray();
    }

    private async Task<Dictionary<Guid, int>> GetWarehouseOrderMovementLinkCountsAsync(
        IReadOnlyCollection<Guid> orderLineGuids,
        CancellationToken cancellationToken)
    {
        var distinctOrderLineGuids = orderLineGuids
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctOrderLineGuids.Length == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var linkedOrderLineGuids = await mikroWriteDbContext.STOK_HAREKETLERI_EKs
            .AsNoTracking()
            .Where(extra =>
                extra.sth_subesip_uid.HasValue &&
                distinctOrderLineGuids.Contains(extra.sth_subesip_uid.Value))
            .Select(extra => extra.sth_subesip_uid!.Value)
            .ToListAsync(cancellationToken);

        return linkedOrderLineGuids
            .GroupBy(guid => guid)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private async Task<Dictionary<Guid, int>> GetMovementLinkCountsAsync(
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        CancellationToken cancellationToken)
    {
        var orderLineGuids = orderLines
            .Select(order => order.ssip_Guid)
            .Distinct()
            .ToArray();

        if (orderLineGuids.Length == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var linkedOrderLineGuids = await mikroWriteDbContext.STOK_HAREKETLERI_EKs
            .AsNoTracking()
            .Where(extra =>
                extra.sth_subesip_uid.HasValue &&
                orderLineGuids.Contains(extra.sth_subesip_uid.Value))
            .Select(extra => extra.sth_subesip_uid!.Value)
            .ToListAsync(cancellationToken);

        return linkedOrderLineGuids
            .GroupBy(guid => guid)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static IReadOnlyCollection<MatchedC01Line> MatchC01Lines(
        AxataOutboundDeliveryDocument document,
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        IReadOnlyCollection<AxataOutboundDeliveryLine> positiveLines)
    {
        var result = new List<MatchedC01Line>(positiveLines.Count);

        foreach (var axataLine in positiveLines)
        {
            var orderLine = orderLines.FirstOrDefault(order =>
                (order.ssip_satirno ?? -1) == axataLine.LineNo &&
                string.Equals(
                    NormalizeCode(order.ssip_stok_kod),
                    NormalizeCode(axataLine.StockCode),
                    StringComparison.OrdinalIgnoreCase));

            if (orderLine is not null)
            {
                result.Add(new MatchedC01Line(document, axataLine, orderLine));
            }
        }

        return result;
    }

    private static string? BuildC01Warning(
        AxataOutboundDeliveryDocument document,
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        IReadOnlyCollection<AxataOutboundDeliveryLine> positiveLines,
        IReadOnlyCollection<MatchedC01Line> matchedLines,
        int existingLinkedMovementLineCount)
    {
        if (document.AxataSequenceNo <= 0)
        {
            return "AXATA S06SIRA bulunamadi; ENT006 ack guvenli degil.";
        }

        if (!document.DocumentOrderNo.HasValue || string.IsNullOrWhiteSpace(document.DocumentSerie))
        {
            return "AXATA S06TESL seri.sira formatinda degil.";
        }

        if (document.SourceWarehouseNo <= 0 || document.TargetWarehouseNo <= 0)
        {
            return "AXATA kaynak/hedef depo bilgisi okunamadi.";
        }

        if (positiveLines.Count == 0)
        {
            return "AXATA teslimat satiri yok veya miktarlar sifir.";
        }

        if (orderLines.Count == 0)
        {
            return "Mikro depolar arasi siparis bulunamadi.";
        }

        if (matchedLines.Count != positiveLines.Count)
        {
            return "AXATA satirlari Mikro siparis satirlariyla birebir eslesmedi.";
        }

        if (existingLinkedMovementLineCount > 0)
        {
            return "Mikro sevk linki zaten mevcut; duplicate fis uretilmez.";
        }

        if (matchedLines.Any(line =>
                line.OrderLine.ssip_girdepo != document.TargetWarehouseNo ||
                line.OrderLine.ssip_cikdepo != document.SourceWarehouseNo))
        {
            return "Mikro siparis depo bilgisi AXATA teslimat deposuyla uyusmuyor.";
        }

        if (matchedLines.Any(line => line.OrderLine.ssip_kapat_fl == true))
        {
            return "Mikro siparis satiri kapali.";
        }

        if (matchedLines.Any(line =>
            line.AxataLine.Quantity >
            ((line.OrderLine.ssip_miktar ?? 0d) - (line.OrderLine.ssip_teslim_miktar ?? 0d)) + QuantityTolerance))
        {
            return "AXATA teslim miktari Mikro siparis kalan miktarindan buyuk.";
        }

        return null;
    }

    private static string ResolveC01MikroCheckState(
        IReadOnlyCollection<DEPOLAR_ARASI_SIPARISLER> orderLines,
        IReadOnlyCollection<AxataOutboundDeliveryLine> positiveLines,
        IReadOnlyCollection<MatchedC01Line> matchedLines,
        int existingLinkedMovementLineCount,
        bool canImport)
    {
        if (existingLinkedMovementLineCount > 0)
        {
            return "MikroShipmentExistsPendingAck";
        }

        if (canImport)
        {
            return "ReadyForImport";
        }

        if (orderLines.Count == 0)
        {
            return "OrderNotFound";
        }

        if (matchedLines.Count != positiveLines.Count)
        {
            return "OrderLineMismatch";
        }

        return "Blocked";
    }

    private static bool CanAcknowledgeExistingMikroShipment(C01DeliveryAnalysis analysis) =>
        analysis.ImportDto.ExistingLinkedMovementLineCount > 0 &&
        analysis.ImportDto.MikroDeliveredQuantity + QuantityTolerance >= analysis.ImportDto.AxataQuantity;

    private static AxataPendingOutboundDeliveryDto ToPendingOutboundDeliveryDto(C01DeliveryAnalysis analysis) =>
        new(
            analysis.Document.MovementType,
            analysis.Document.Status,
            analysis.Document.AxataSequenceNo,
            analysis.Document.AxataDeliveryNo,
            analysis.Document.DocumentSerie,
            analysis.Document.DocumentOrderNo,
            analysis.Document.SourceWarehouseNo,
            analysis.Document.TargetWarehouseNo,
            analysis.Document.AxataDate,
            analysis.Document.Lines.Count,
            analysis.Document.Lines.Sum(line => line.Quantity),
            analysis.ImportDto.MikroOrderLineCount,
            analysis.ImportDto.MikroOrderQuantity,
            analysis.ImportDto.MikroDeliveredQuantity,
            analysis.ImportDto.ExistingLinkedMovementLineCount,
            analysis.MikroCheckState,
            analysis.ImportDto.CanImport || CanAcknowledgeExistingMikroShipment(analysis),
            analysis.ImportDto.Warning);

    private static AxataPendingOutboundDeliveryDto ToQueueOnlyPendingDto(AxataOutboundDeliveryDocument document) =>
        new(
            document.MovementType,
            document.Status,
            document.AxataSequenceNo,
            document.AxataDeliveryNo,
            document.DocumentSerie,
            document.DocumentOrderNo,
            document.SourceWarehouseNo,
            document.TargetWarehouseNo,
            document.AxataDate,
            document.Lines.Count,
            document.Lines.Sum(line => line.Quantity),
            0,
            0d,
            0d,
            0,
            "PendingInAxataQueue",
            false,
            "Bu hareket tipi icin Mikro fis eslesmesi bu endpointte kontrol edilmiyor; AXATA status 0 oldugu icin worker tamamlamamis kabul edilir.");

    private static AxataOutboundDeliveryQueueDocumentDto ToQueuePreviewDocumentDto(
        AxataOutboundDeliveryDocument document,
        bool hasLiveImport) =>
        new(
            document.AxataSequenceNo,
            document.AxataDeliveryNo,
            document.DocumentSerie,
            document.DocumentOrderNo,
            document.MovementType,
            document.Status,
            document.SourceWarehouseNo,
            document.TargetWarehouseNo,
            document.AxataDate,
            document.Lines.Count,
            document.Lines.Sum(line => line.Quantity),
            hasLiveImport,
            hasLiveImport
                ? "LiveImportAvailableViaC01Endpoint"
                : "LiveQueuePreviewOnly",
            hasLiveImport
                ? "Detayli import uygunlugu icin C01 preview endpoint'i kullanilmalidir."
                : "Bu hareket tipi icin Mikro import/ack endpoint'i henuz acik degildir.");

    private static CreateInterWarehouseShipmentRequest BuildCreateShipmentRequest(C01DeliveryAnalysis analysis) =>
        new(
            analysis.Document.SourceWarehouseNo,
            analysis.Document.TargetWarehouseNo,
            TransitWarehouseNo,
            analysis.Document.AxataDate ?? DateTime.Today,
            analysis.Document.AxataDate ?? DateTime.Today,
            analysis.Document.AxataDeliveryNo,
            analysis.Document.AxataDeliveryNo,
            analysis.MatchedLines
                .OrderBy(line => line.AxataLine.LineNo)
                .Select(line => new CreateInterWarehouseShipmentLineRequest(
                    line.AxataLine.StockCode,
                    line.AxataLine.Quantity,
                    line.OrderLine.ssip_Guid,
                    line.OrderLine.ssip_b_fiyat ?? 0d,
                    line.OrderLine.ssip_birim_pntr ?? 1,
                    analysis.Document.AxataDeliveryNo,
                    null,
                    0,
                    line.OrderLine.ssip_projekodu,
                    null,
                    line.OrderLine.ssip_sormerkezi))
                .ToArray(),
            true);

    private async Task<IReadOnlyCollection<AxataOutboundDeliveryDocument>> FetchPendingOutboundDeliveriesAsync(
        string movementType,
        CancellationToken cancellationToken) =>
        await FetchOutboundDeliveriesAsync(movementType, PendingStatus, null, cancellationToken);

    private async Task<IReadOnlyCollection<AxataOutboundDeliveryDocument>> FetchOutboundDeliveriesAsync(
        string movementType,
        string status,
        string? orderNumber,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration(requireExtendedEndpoint: false);
        var envelope = BuildFetchEnvelope(
            configuration,
            movementType,
            status,
            orderNumber);
        var responseXml = await SendSoapAsync(
            envelope,
            configuration.MainEndpointUrl,
            MainContractName,
            FetchOperationName,
            cancellationToken);
        var serviceResponse = ParseSoapServiceResponse(responseXml);

        if (!serviceResponse.IsSuccess)
        {
            throw new InvalidOperationException(
                $"AXATA {FetchOperationName} failed: {serviceResponse.Message}");
        }

        return ParseOutboundDeliveryDocuments(responseXml, movementType);
    }

    private async Task<OutboundDeliveryAuditFetchResult> TryFetchPendingOutboundDeliveriesForAuditAsync(
        string movementType,
        CancellationToken cancellationToken)
    {
        try
        {
            return new OutboundDeliveryAuditFetchResult(
                true,
                await FetchPendingOutboundDeliveriesAsync(movementType, cancellationToken),
                null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new OutboundDeliveryAuditFetchResult(
                false,
                Array.Empty<AxataOutboundDeliveryDocument>(),
                exception.Message);
        }
    }

    private async Task AcknowledgeOutboundDeliveryAsync(
        long axataSequenceNo,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration(requireExtendedEndpoint: true);
        var envelope = BuildAckEnvelope(configuration, axataSequenceNo);
        var responseXml = await SendSoapAsync(
            envelope,
            configuration.ExtendedEndpointUrl,
            ExtContractName,
            AckOperationName,
            cancellationToken);
        var serviceResponse = ParseSoapServiceResponse(responseXml);

        if (!serviceResponse.IsSuccess)
        {
            throw new InvalidOperationException(
                $"AXATA {AckOperationName} failed: {serviceResponse.Message}");
        }
    }

    private AxataOutboundDeliveryConfiguration GetRequiredConfiguration(bool requireExtendedEndpoint)
    {
        var currentOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(currentOptions.MainEndpointUrl))
        {
            throw new InvalidOperationException("AXATA main endpoint url is not configured.");
        }

        if (requireExtendedEndpoint && string.IsNullOrWhiteSpace(currentOptions.ExtendedEndpointUrl))
        {
            throw new InvalidOperationException("AXATA extended endpoint url is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Username))
        {
            throw new InvalidOperationException("AXATA username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Password))
        {
            throw new InvalidOperationException("AXATA password is not configured.");
        }

        return new AxataOutboundDeliveryConfiguration(
            currentOptions.MainEndpointUrl,
            currentOptions.ExtendedEndpointUrl,
            currentOptions.Username,
            currentOptions.Password);
    }

    private static string BuildFetchEnvelope(
        AxataOutboundDeliveryConfiguration configuration,
        string movementType,
        string status,
        string? orderNumber)
    {
        var soap = XNamespace.Get(SoapEnvelopeNamespace);
        var service = XNamespace.Get(ServiceNamespace);
        var document = new XDocument(
            new XElement(
                soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soap),
                new XAttribute(XNamespace.Xmlns + "tem", service),
                new XElement(
                    soap + "Body",
                    new XElement(service + "username", configuration.Username),
                    new XElement(service + "password", configuration.Password),
                    new XElement(
                        "OutboundDeliveryQuery",
                        new XElement("CompanyCode", CompanyCode),
                        new XElement("WarehouseCode", WarehouseCode),
                        new XElement("OrderNumber", NormalizeQueryValue(orderNumber)),
                        new XElement("Firma", string.Empty),
                        new XElement("MovementType", movementType),
                        new XElement("Status", status),
                        new XElement("YuklemeNo", string.Empty),
                        new XElement("Type", string.Empty)))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static string BuildAckEnvelope(
        AxataOutboundDeliveryConfiguration configuration,
        long axataSequenceNo)
    {
        var soap = XNamespace.Get(SoapEnvelopeNamespace);
        var service = XNamespace.Get(ServiceNamespace);
        var axataWms = XNamespace.Get(AxataWmsNamespace);
        var document = new XDocument(
            new XElement(
                soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soap),
                new XAttribute(XNamespace.Xmlns + "tem", service),
                new XElement(
                    soap + "Body",
                    new XElement(service + "username", configuration.Username),
                    new XElement(service + "password", configuration.Password),
                    new XElement(
                        axataWms + "Table",
                        new XElement("TableName", "ENT006"),
                        new XElement("UpdateField", "S06STAT"),
                        new XElement("UpdateValue", CompletedStatus),
                        new XElement("IDField", "S06SIRA"),
                        new XElement(
                            "IDValues",
                            new XElement("IDValue", axataSequenceNo.ToString(CultureInfo.InvariantCulture)))))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private async Task<string> SendSoapAsync(
        string envelope,
        string endpointUrl,
        string contractName,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };

        requestMessage.Headers.TryAddWithoutValidation(
            "SOAPAction",
            $"{ServiceNamespace}{contractName}/{operationName}");

        using var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(requestMessage, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                AppendResponsePreview(
                    ExtractSoapFaultOrDefault(
                    responseContent,
                        $"AXATA service returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}."),
                    responseContent));
        }

        return responseContent;
    }

    private static IReadOnlyCollection<AxataOutboundDeliveryDocument> ParseOutboundDeliveryDocuments(
        string responseXml,
        string requestedMovementType)
    {
        var document = XDocument.Parse(responseXml);
        var documentElements = document.Descendants()
            .Where(element => Child(element, "ENT006") is not null)
            .ToArray();
        var result = new List<AxataOutboundDeliveryDocument>(documentElements.Length);

        foreach (var documentElement in documentElements)
        {
            var header = Child(documentElement, "ENT006");
            if (header is null)
            {
                continue;
            }

            var axataDeliveryNo = Field(header, "S06TESL");
            var (documentSerie, documentOrderNo) = ParseAxataDeliveryNo(axataDeliveryNo);
            var movementType = requestedMovementType;
            var sourceWarehouseNo = ParseInt(FirstNonEmpty(Field(header, "S06FIRM"), Field(header, "S06SMUS"))) ?? 0;
            var targetWarehouseNo = ParseInt(FirstNonEmpty(Field(header, "S06TFIR"), Field(header, "S06TMUS"))) ?? 0;
            var status = FirstNonEmpty(Field(header, "S06STAT"), PendingStatus);
            var axataDate = ParseDate(FirstNonEmpty(
                Field(header, "S06ITAR"),
                Field(header, "S06TARI"),
                Field(header, "S06SDAT"),
                Field(header, "S06CTAR")));
            var lines = ParseOutboundDeliveryLines(documentElement);

            result.Add(new AxataOutboundDeliveryDocument(
                ParseLong(Field(header, "S06SIRA")) ?? 0,
                axataDeliveryNo,
                documentSerie,
                documentOrderNo,
                movementType,
                status,
                sourceWarehouseNo,
                targetWarehouseNo,
                axataDate,
                lines));
        }

        return result
            .OrderBy(item => item.AxataSequenceNo)
            .ToArray();
    }

    private static IReadOnlyCollection<AxataOutboundDeliveryLine> ParseOutboundDeliveryLines(XElement documentElement)
    {
        var lineRoot = Child(documentElement, "ENT007_List") ?? documentElement;

        return lineRoot.Descendants()
            .Where(element =>
                Child(element, "S07KALN") is not null &&
                Child(element, "S07SKOD") is not null)
            .Select(element => new AxataOutboundDeliveryLine(
                ParseInt(Field(element, "S07KALN")) ?? 0,
                Field(element, "S07SKOD"),
                ParseDouble(Field(element, "S07MIKT")) ?? 0d))
            .ToArray();
    }

    private static AxataSoapServiceResponse ParseSoapServiceResponse(string responseXml)
    {
        var faultMessage = ExtractSoapFaultOrDefault(responseXml, null);
        if (!string.IsNullOrWhiteSpace(faultMessage))
        {
            return new AxataSoapServiceResponse(false, null, faultMessage);
        }

        var document = XDocument.Parse(responseXml);
        var stateText = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("state", StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim();
        var message = document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("message", StringComparison.OrdinalIgnoreCase))
            ?.Value
            .Trim();
        var state = ParseInt(stateText ?? string.Empty);
        var isSuccess = !state.HasValue || state.Value == 0;

        return new AxataSoapServiceResponse(
            isSuccess,
            state,
            string.IsNullOrWhiteSpace(message) ? "AXATA response received." : message);
    }

    private static string ExtractSoapFaultOrDefault(string responseContent, string? fallbackMessage)
    {
        try
        {
            var document = XDocument.Parse(responseContent);
            var fault = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals("Fault", StringComparison.OrdinalIgnoreCase));

            if (fault is null)
            {
                return fallbackMessage ?? string.Empty;
            }

            var faultString = fault.Descendants()
                .FirstOrDefault(element => element.Name.LocalName.Equals("faultstring", StringComparison.OrdinalIgnoreCase))
                ?.Value
                .Trim();

            return string.IsNullOrWhiteSpace(faultString)
                ? fallbackMessage ?? "AXATA SOAP fault was returned."
                : faultString;
        }
        catch
        {
            return fallbackMessage ?? string.Empty;
        }
    }

    private static string AppendResponsePreview(string message, string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return message;
        }

        var preview = responseContent
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        if (preview.Length > 500)
        {
            preview = preview[..500] + "...";
        }

        return $"{message} Response body: {preview}";
    }

    private static (DateTime StartDate, DateTime EndDate) ResolveDateRange(DateTime? startDate, DateTime? endDate)
    {
        var normalizedEndDate = (endDate ?? DateTime.Today).Date;
        var normalizedStartDate = (startDate ?? normalizedEndDate).Date;

        if (normalizedEndDate < normalizedStartDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.");
        }

        return (normalizedStartDate, normalizedEndDate);
    }

    private static int NormalizeTake(int? take) =>
        take is > 0 ? Math.Min(take.Value, MaxTake) : DefaultTake;

    private static string ResolveOutboundDeliveryMovementType(string? movementType)
    {
        var normalized = string.IsNullOrWhiteSpace(movementType)
            ? C01MovementType
            : movementType.Trim().ToUpperInvariant();

        if (normalized == "C04")
        {
            normalized = C04LegacyMovementType;
        }

        return AuditMovementTypes.Any(item => item.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ? normalized
            : throw new ArgumentException(
                "Movement type must be one of C01, C02, C03, C4 or C04.",
                nameof(movementType));
    }

    private static IReadOnlyCollection<string> ResolveOutboundDeliveryStatuses(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return [PendingStatus, CompletedStatus];
        }

        var normalized = status.Trim();
        return normalized is PendingStatus or CompletedStatus
            ? [normalized]
            : throw new ArgumentException("Status must be 0 or 1.", nameof(status));
    }

    private static string NormalizeRequiredDocumentSerie(string documentSerie)
    {
        if (string.IsNullOrWhiteSpace(documentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(documentSerie));
        }

        return documentSerie.Trim();
    }

    private static string FormatAxataDocumentNo(string documentSerie, int documentOrderNo) =>
        $"{NormalizeRequiredDocumentSerie(documentSerie)}.{documentOrderNo.ToString(CultureInfo.InvariantCulture)}";

    private static string FormatStatuses(IReadOnlyCollection<string> statuses) =>
        string.Join(", ", statuses.Select(status => status.Trim()));

    private static bool MatchesAxataDocument(
        AxataOutboundDeliveryDocument document,
        string documentSerie,
        int documentOrderNo) =>
        (document.DocumentOrderNo == documentOrderNo &&
         string.Equals(document.DocumentSerie, documentSerie, StringComparison.OrdinalIgnoreCase)) ||
        string.Equals(
            document.AxataDeliveryNo,
            FormatAxataDocumentNo(documentSerie, documentOrderNo),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeQueryValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static bool IsInsideDateRange(DateTime? value, DateTime startDate, DateTime endDate) =>
        !value.HasValue || (value.Value.Date >= startDate.Date && value.Value.Date <= endDate.Date);

    private static bool IsAxataSentFlag(string? value) =>
        string.Equals(value?.Trim(), CompletedStatus, StringComparison.OrdinalIgnoreCase);

    private static (string Serie, int? OrderNo) ParseAxataDeliveryNo(string deliveryNo)
    {
        var normalized = deliveryNo.Trim();
        var separatorIndex = normalized.LastIndexOf('.');

        if (separatorIndex <= 0 || separatorIndex >= normalized.Length - 1)
        {
            return (normalized, null);
        }

        var serie = normalized[..separatorIndex].Trim();
        var orderNoText = normalized[(separatorIndex + 1)..].Trim();

        return int.TryParse(orderNoText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNo)
            ? (serie, orderNo)
            : (serie, null);
    }

    private static string Field(XElement element, string localName) =>
        Child(element, localName)?.Value.Trim() ?? string.Empty;

    private static XElement? Child(XElement element, string localName) =>
        element.Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeCode(string? value) =>
        value?.Trim() ?? string.Empty;

    private static int? ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static long? ParseLong(string value) =>
        long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static double? ParseDouble(string value)
    {
        var trimmed = value.Trim();
        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var trValue))
        {
            return trValue;
        }

        var normalized = trimmed.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var normalizedValue)
            ? normalizedValue
            : null;
    }

    private static DateTime? ParseDate(string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        string[] formats =
        [
            "yyyyMMdd",
            "yyyyMMddHHmmss",
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss",
            "dd.MM.yyyy",
            "dd.MM.yyyy HH:mm:ss",
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss"
        ];

        if (DateTime.TryParseExact(
                trimmed,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var exactValue))
        {
            return exactValue;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.AssumeLocal, out var trValue))
        {
            return trValue;
        }

        return DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedValue)
            ? parsedValue
            : null;
    }
}

internal sealed record AxataOutboundDeliveryConfiguration(
    string MainEndpointUrl,
    string ExtendedEndpointUrl,
    string Username,
    string Password);

internal sealed record AxataSoapServiceResponse(
    bool IsSuccess,
    int? State,
    string Message);

internal sealed record AxataOutboundDeliveryDocument(
    long AxataSequenceNo,
    string AxataDeliveryNo,
    string DocumentSerie,
    int? DocumentOrderNo,
    string MovementType,
    string Status,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    DateTime? AxataDate,
    IReadOnlyCollection<AxataOutboundDeliveryLine> Lines);

internal sealed record AxataOutboundDeliveryLine(
    int LineNo,
    string StockCode,
    double Quantity);

internal sealed record MatchedC01Line(
    AxataOutboundDeliveryDocument Document,
    AxataOutboundDeliveryLine AxataLine,
    DEPOLAR_ARASI_SIPARISLER OrderLine);

internal sealed record OutboundDeliveryAuditFetchResult(
    bool IsSuccess,
    IReadOnlyCollection<AxataOutboundDeliveryDocument> Documents,
    string? ErrorMessage);

internal sealed record C01DeliveryAnalysis(
    AxataOutboundDeliveryDocument Document,
    AxataOutboundDeliveryImportDocumentDto ImportDto,
    IReadOnlyCollection<MatchedC01Line> MatchedLines,
    string MikroCheckState);

internal sealed record MikroDocumentKey(
    string DocumentSerie,
    int DocumentOrderNo);

internal sealed record WarehouseOrderAuditRow(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime DocumentDate,
    int InWarehouseNo,
    int OutWarehouseNo,
    int LineNo,
    Guid OrderLineGuid,
    double Quantity,
    double DeliveredQuantity,
    string Special1,
    DateTime? LastUpdateDate);

internal sealed record WarehouseOrderAuditResult(
    int DocumentCount,
    int SentDocumentCount,
    int PartiallySentDocumentCount,
    int UnsentDocumentCount,
    int SentMissingMikroShipmentDocumentCount,
    int SentMissingMikroShipmentLineCount,
    double SentMissingMikroShipmentQuantity,
    IReadOnlyCollection<AxataSentWarehouseOrderMissingShipmentDto> SentMissingMikroShipmentDocuments,
    IReadOnlyCollection<AxataUnsyncedWarehouseOrderDto> UnsyncedDocuments);
