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
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class FirmMasterSyncTaskHandler(
    MikroDbContext mikroDbContext,
    AxataSynchronizationOutboxWriter outboxWriter,
    AxataSynchronizationLiveTransportService liveTransportService)
    : IAxataSynchronizationTaskHandler
{
    public string Code => "firm-master-sync";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var query = CreateQuery();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Take(take).ToArrayAsync(cancellationToken);

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            null,
            totalCount,
            items.Length,
            DateTime.UtcNow,
            items.Select(item => new AxataSynchronizationPreviewItemDto(
                    item.CustomerCode,
                    item.DisplayName,
                    JsonSerializer.Serialize(item, AxataSynchronizationJson.Options)))
                .ToArray(),
            ["Canli Mikro cari hesaplari okunarak preview olusturuldu."]);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var items = await CreateQuery().ToArrayAsync(cancellationToken);

        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
        {
            var succeededCount = 0;
            foreach (var item in items)
            {
                var dispatch = await liveTransportService.DispatchFirmMasterAsync(
                    context,
                    item,
                    cancellationToken);
                if (!dispatch.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"Firma AXATA'ya gonderilemedi. Cari={item.CustomerCode}, State={dispatch.ServiceState}, Message={dispatch.ServiceMessage}");
                }

                succeededCount++;
            }

            return new AxataSynchronizationTaskExecutionResult(
                succeededCount,
                $"{succeededCount} firma master/adres kaydi AXATA'ya canli gonderildi.",
                Array.Empty<AxataSynchronizationJobArtifactDto>());
        }

        var payload = new
        {
            task = context.Definition.Code,
            taskName = context.Definition.Name,
            generatedAtUtc = context.RequestedAtUtc,
            recordCount = items.Length,
            records = items
        };

        var artifacts = await WriteArtifactsIfNeededAsync(context, payload, cancellationToken);

        return new AxataSynchronizationTaskExecutionResult(
            items.Length,
            $"{items.Length} firma master kaydi hazirlandi.",
            artifacts);
    }

    private IQueryable<FirmMasterPayloadItem> CreateQuery() =>
        mikroDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .Where(customer =>
                customer.cari_iptal != true &&
                customer.cari_kod != null &&
                customer.cari_unvan1 != null)
            .GroupJoin(
                mikroDbContext.CARI_HESAP_ADRESLERIs.AsNoTracking(),
                customer => new
                {
                    CustomerCode = customer.cari_kod,
                    AddressNo = customer.cari_sevk_adres_no ?? customer.cari_fatura_adres_no ?? 1
                },
                address => new
                {
                    CustomerCode = address.adr_cari_kod,
                    AddressNo = address.adr_adres_no ?? 0
                },
                (customer, addresses) => new { customer, addresses })
            .SelectMany(
                item => item.addresses.DefaultIfEmpty(),
                (item, address) => new { item.customer, address })
            .OrderBy(item => item.customer.cari_kod)
            .Select(item => new FirmMasterPayloadItem(
                item.customer.cari_kod ?? string.Empty,
                item.customer.cari_unvan1 ?? string.Empty,
                item.customer.cari_unvan2 ?? string.Empty,
                ((item.customer.cari_unvan1 ?? string.Empty) + " " + (item.customer.cari_unvan2 ?? string.Empty)).Trim(),
                item.customer.cari_VergiKimlikNo ?? string.Empty,
                item.customer.cari_vdaire_no ?? string.Empty,
                item.customer.cari_EMail ?? string.Empty,
                item.customer.cari_CepTel ?? string.Empty,
                ((item.address!.adr_cadde ?? string.Empty) + " " + (item.address.adr_sokak ?? string.Empty)).Trim(),
                ((item.address.adr_ilce ?? string.Empty) + " " + (item.address.adr_il ?? string.Empty)).Trim(),
                item.customer.cari_fatura_adres_no,
                item.customer.cari_sevk_adres_no,
                item.customer.cari_efatura_fl ?? false,
                item.customer.cari_eirsaliye_fl ?? false));

    private async Task<IReadOnlyCollection<AxataSynchronizationJobArtifactDto>> WriteArtifactsIfNeededAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Outbox)
        {
            return Array.Empty<AxataSynchronizationJobArtifactDto>();
        }

        return [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
    }

}

internal sealed class ProductMasterSyncTaskHandler(
    MikroDbContext mikroDbContext,
    AxataSynchronizationOutboxWriter outboxWriter,
    IAxataProductSynchronizationService productSynchronizationService)
    : IAxataSynchronizationTaskHandler
{
    public string Code => "product-master-sync";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var query = CreateQuery();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Take(take).ToArrayAsync(cancellationToken);

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            null,
            totalCount,
            items.Length,
            DateTime.UtcNow,
            items.Select(item => new AxataSynchronizationPreviewItemDto(
                    item.ProductCode,
                    $"{item.ProductName} / {item.Barcode}",
                    JsonSerializer.Serialize(item, AxataSynchronizationJson.Options)))
                .ToArray(),
            ["Canli Mikro stok ve barkod kayitlari okunarak preview olusturuldu."]);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
        {
            var dispatch = await productSynchronizationService.DispatchAsync(
                new AxataProductSynchronizationDispatchRequest(
                    Array.Empty<string>(),
                    100000,
                    true),
                cancellationToken);
            if (dispatch.FailedProductCount > 0)
            {
                throw new InvalidOperationException(
                    $"{dispatch.FailedProductCount} urun AXATA'ya gonderilemedi. Basarili: {dispatch.SucceededProductCount}.");
            }

            return new AxataSynchronizationTaskExecutionResult(
                dispatch.SucceededProductCount,
                $"{dispatch.SucceededProductCount} urun AXATA addSKUMaster operasyonuyla canli gonderildi.",
                Array.Empty<AxataSynchronizationJobArtifactDto>());
        }

        var items = await CreateQuery().ToArrayAsync(cancellationToken);
        var payload = new
        {
            task = context.Definition.Code,
            taskName = context.Definition.Name,
            generatedAtUtc = context.RequestedAtUtc,
            recordCount = items.Length,
            records = items
        };

        var artifacts = await WriteArtifactsIfNeededAsync(context, payload, cancellationToken);

        return new AxataSynchronizationTaskExecutionResult(
            items.Length,
            $"{items.Length} urun master kaydi hazirlandi.",
            artifacts);
    }

    private IQueryable<ProductMasterPayloadItem> CreateQuery() =>
        from stock in mikroDbContext.STOKLARs.AsNoTracking()
        where !(stock.sto_pasif_fl ?? false) &&
              stock.sto_kod != null
        let barcodeRow = mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(item => item.bar_stokkodu == stock.sto_kod && item.bar_kodu != null)
            .OrderByDescending(item => item.bar_master ?? false)
            .ThenBy(item => item.bar_birimpntr ?? 0)
            .ThenByDescending(item => item.bar_create_date)
            .Select(item => new
            {
                item.bar_kodu,
                item.bar_icerigi
            })
            .FirstOrDefault()
        where barcodeRow != null && barcodeRow.bar_kodu != null
        orderby stock.sto_kod
        select new ProductMasterPayloadItem(
            stock.sto_kod,
            stock.sto_isim ?? string.Empty,
            barcodeRow!.bar_kodu ?? string.Empty,
            stock.sto_birim1_ad ?? string.Empty,
            stock.sto_plu_no,
            (byte)(stock.sto_perakende_vergi ?? 0),
            stock.sto_kasa_tarti_fl ?? false,
            (barcodeRow.bar_icerigi ?? 0) == 1,
            (stock.sto_satis_dursun ?? 0) != 0,
            (stock.sto_siparis_dursun ?? 0) != 0,
            (stock.sto_malkabul_dursun ?? 0) != 0);

    private async Task<IReadOnlyCollection<AxataSynchronizationJobArtifactDto>> WriteArtifactsIfNeededAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Outbox)
        {
            return Array.Empty<AxataSynchronizationJobArtifactDto>();
        }

        return [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
    }
}

internal sealed class IssuedWarehouseOrderSyncTaskHandler(
    WarehouseOrderListQueryExecutor listQueryExecutor,
    WarehouseOrderDetailQueryExecutor detailQueryExecutor,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    AxataSynchronizationOutboxWriter outboxWriter,
    AxataSynchronizationLiveTransportService liveTransportService)
    : IAxataSynchronizationTaskHandler
{
    private const WarehouseOrderListDirection AxataWarehouseOrderDirection = WarehouseOrderListDirection.Received;

    public string Code => "issued-warehouse-order-sync";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateWarehouseOrderListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            AxataWarehouseOrderDirection,
            cancellationToken);

        var selected = documents.Take(take).ToArray();
        var previewItems = new List<AxataSynchronizationPreviewItemDto>(selected.Length);

        foreach (var document in selected)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new WarehouseOrderDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                AxataWarehouseOrderDirection,
                cancellationToken);

            var payload = AxataSynchronizationPayloadFactory.BuildWarehouseOrderDocument(detail);
            previewItems.Add(new AxataSynchronizationPreviewItemDto(
                $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                $"{document.LineCount} satir / {document.TotalQuantity:0.##} miktar",
                JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options)));
        }

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            warehouseNo,
            documents.Count,
            previewItems.Count,
            DateTime.UtcNow,
            previewItems,
            [$"Son {Math.Max(1, options.CurrentValue.DefaultLookbackDays)} gun icindeki AXATA kaynak/cikis depo siparisleri tarandi."]);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateWarehouseOrderListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            AxataWarehouseOrderDirection,
            cancellationToken);

        var payloadDocuments = new List<object>(documents.Count);

        foreach (var document in documents)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new WarehouseOrderDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                AxataWarehouseOrderDirection,
                cancellationToken);

            if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
            {
                var dispatch = await liveTransportService.DispatchWarehouseOrderAsync(
                    context,
                    detail,
                    cancellationToken);
                if (!dispatch.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"AXATA C01 cikis siparisi gonderilemedi. Belge={document.DocumentSerie}.{document.DocumentOrderNo}, State={dispatch.ServiceState}, Message={dispatch.ServiceMessage}");
                }

                payloadDocuments.Add(new
                {
                    document = $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                    dispatch.OperationName,
                    dispatch.ServiceState,
                    dispatch.ServiceMessage
                });
                continue;
            }

            payloadDocuments.Add(AxataSynchronizationPayloadFactory.BuildWarehouseOrderDocument(detail));
        }

        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
        {
            return new AxataSynchronizationTaskExecutionResult(
                payloadDocuments.Count,
                $"{payloadDocuments.Count} verilen depo siparisi AXATA C01 outbound order olarak canli gonderildi.",
                Array.Empty<AxataSynchronizationJobArtifactDto>());
        }

        var payload = new
        {
            task = context.Definition.Code,
            taskName = context.Definition.Name,
            generatedAtUtc = context.RequestedAtUtc,
            warehouseNo,
            documentCount = payloadDocuments.Count,
            documents = payloadDocuments
        };

        var artifacts = await WriteArtifactsIfNeededAsync(context, payload, cancellationToken);

        return new AxataSynchronizationTaskExecutionResult(
            payloadDocuments.Count,
            $"{payloadDocuments.Count} verilen depo siparisi payload'a donusturuldu.",
            artifacts);
    }

    private static WarehouseOrderListRequest CreateWarehouseOrderListRequest(int lookbackDays, int warehouseNo)
    {
        var normalizedLookbackDays = Math.Max(1, lookbackDays);
        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-(normalizedLookbackDays - 1));

        return new WarehouseOrderListRequest(warehouseNo, startDate, endDate);
    }

    private static int GetRequiredWarehouseNo(AxataSynchronizationTaskExecutionContext context) =>
        context.WarehouseNo is > 0
            ? context.WarehouseNo.Value
            : throw new ArgumentException("Warehouse number is required for this synchronization task.");

    private async Task<IReadOnlyCollection<AxataSynchronizationJobArtifactDto>> WriteArtifactsIfNeededAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Outbox)
        {
            return Array.Empty<AxataSynchronizationJobArtifactDto>();
        }

        return [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
    }
}

internal sealed class ReceivedCompanyOrderSyncTaskHandler(
    CompanyOrderListQueryExecutor listQueryExecutor,
    CompanyOrderDetailQueryExecutor detailQueryExecutor,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    AxataSynchronizationOutboxWriter outboxWriter,
    AxataSynchronizationLiveTransportService liveTransportService)
    : IAxataSynchronizationTaskHandler
{
    private const CompanyOrderListDirection AxataCompanyOrderDirection = CompanyOrderListDirection.Received;

    public string Code => "received-company-order-sync";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateCompanyOrderListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            AxataCompanyOrderDirection,
            cancellationToken);

        var selected = documents.Take(take).ToArray();
        var previewItems = new List<AxataSynchronizationPreviewItemDto>(selected.Length);

        foreach (var document in selected)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new CompanyOrderDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                AxataCompanyOrderDirection,
                cancellationToken);

            var payload = AxataSynchronizationPayloadFactory.BuildCompanyOrderDocument(detail);
            previewItems.Add(new AxataSynchronizationPreviewItemDto(
                $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                $"{document.CustomerDisplayName} / {document.LineCount} satir / {document.TotalRemainingQuantity:0.##} kalan miktar",
                JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options)));
        }

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            warehouseNo,
            documents.Count,
            previewItems.Count,
            DateTime.UtcNow,
            previewItems,
            [$"Son {Math.Max(1, options.CurrentValue.DefaultLookbackDays)} gun icindeki acik alinan firma siparisleri AXATA C02 icin tarandi."]);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateCompanyOrderListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            AxataCompanyOrderDirection,
            cancellationToken);
        var payloadDocuments = new List<object>(documents.Count);

        foreach (var document in documents)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new CompanyOrderDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                AxataCompanyOrderDirection,
                cancellationToken);

            if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
            {
                var dispatch = await liveTransportService.DispatchCustomerOutboundOrderAsync(
                    context,
                    detail,
                    cancellationToken);
                if (!dispatch.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"AXATA C02 cikis siparisi gonderilemedi. Belge={document.DocumentSerie}.{document.DocumentOrderNo}, State={dispatch.ServiceState}, Message={dispatch.ServiceMessage}");
                }

                payloadDocuments.Add(new
                {
                    document = $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                    dispatch.OperationName,
                    dispatch.ServiceState,
                    dispatch.ServiceMessage
                });
                continue;
            }

            payloadDocuments.Add(AxataSynchronizationPayloadFactory.BuildCompanyOrderDocument(detail));
        }

        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
        {
            return new AxataSynchronizationTaskExecutionResult(
                payloadDocuments.Count,
                $"{payloadDocuments.Count} alinan firma siparisi AXATA C02 outbound order olarak canli gonderildi.",
                Array.Empty<AxataSynchronizationJobArtifactDto>());
        }

        var payload = new
        {
            task = context.Definition.Code,
            taskName = context.Definition.Name,
            generatedAtUtc = context.RequestedAtUtc,
            warehouseNo,
            documentCount = payloadDocuments.Count,
            documents = payloadDocuments
        };

        var artifacts = await WriteArtifactsIfNeededAsync(context, payload, cancellationToken);

        return new AxataSynchronizationTaskExecutionResult(
            payloadDocuments.Count,
            $"{payloadDocuments.Count} alinan firma siparisi payload'a donusturuldu.",
            artifacts);
    }

    private static CompanyOrderListRequest CreateCompanyOrderListRequest(int lookbackDays, int warehouseNo)
    {
        var normalizedLookbackDays = Math.Max(1, lookbackDays);
        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-(normalizedLookbackDays - 1));

        return new CompanyOrderListRequest(warehouseNo, startDate, endDate, null, true);
    }

    private static int GetRequiredWarehouseNo(AxataSynchronizationTaskExecutionContext context) =>
        context.WarehouseNo is > 0
            ? context.WarehouseNo.Value
            : throw new ArgumentException("Warehouse number is required for this synchronization task.");

    private async Task<IReadOnlyCollection<AxataSynchronizationJobArtifactDto>> WriteArtifactsIfNeededAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Outbox)
        {
            return Array.Empty<AxataSynchronizationJobArtifactDto>();
        }

        return [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
    }
}

internal sealed class WarehouseInboundOrderSyncTaskHandler(
    WarehouseOrderListQueryExecutor listQueryExecutor,
    WarehouseOrderDetailQueryExecutor detailQueryExecutor,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    AxataSynchronizationOutboxWriter outboxWriter,
    AxataSynchronizationLiveTransportService liveTransportService)
    : IAxataSynchronizationTaskHandler
{
    private const WarehouseOrderListDirection AxataWarehouseOrderDirection = WarehouseOrderListDirection.Issued;

    public string Code => "warehouse-inbound-order-sync";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateWarehouseOrderListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            AxataWarehouseOrderDirection,
            cancellationToken);

        var selected = documents.Take(take).ToArray();
        var previewItems = new List<AxataSynchronizationPreviewItemDto>(selected.Length);

        foreach (var document in selected)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new WarehouseOrderDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                AxataWarehouseOrderDirection,
                cancellationToken);

            var payload = AxataSynchronizationPayloadFactory.BuildWarehouseOrderDocument(detail);
            previewItems.Add(new AxataSynchronizationPreviewItemDto(
                $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                $"{document.LineCount} satir / {document.TotalQuantity:0.##} miktar / kaynak depo {document.RelatedWarehouseName}",
                JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options)));
        }

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            warehouseNo,
            documents.Count,
            previewItems.Count,
            DateTime.UtcNow,
            previewItems,
            [$"Son {Math.Max(1, options.CurrentValue.DefaultLookbackDays)} gun icindeki AXATA hedef/giris depo siparisleri tarandi."]);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateWarehouseOrderListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            AxataWarehouseOrderDirection,
            cancellationToken);

        var payloadDocuments = new List<object>(documents.Count);

        foreach (var document in documents)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new WarehouseOrderDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                AxataWarehouseOrderDirection,
                cancellationToken);

            if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
            {
                var dispatch = await liveTransportService.DispatchWarehouseInboundOrderAsync(
                    context,
                    detail,
                    cancellationToken);
                if (!dispatch.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"AXATA G02 giris siparisi gonderilemedi. Belge={document.DocumentSerie}.{document.DocumentOrderNo}, State={dispatch.ServiceState}, Message={dispatch.ServiceMessage}");
                }

                payloadDocuments.Add(new
                {
                    document = $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                    dispatch.OperationName,
                    dispatch.ServiceState,
                    dispatch.ServiceMessage
                });
                continue;
            }

            payloadDocuments.Add(AxataSynchronizationPayloadFactory.BuildWarehouseOrderDocument(detail));
        }

        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
        {
            return new AxataSynchronizationTaskExecutionResult(
                payloadDocuments.Count,
                $"{payloadDocuments.Count} merkez depo giris siparisi AXATA G02 inbound order olarak canli gonderildi.",
                Array.Empty<AxataSynchronizationJobArtifactDto>());
        }

        var payload = new
        {
            task = context.Definition.Code,
            taskName = context.Definition.Name,
            generatedAtUtc = context.RequestedAtUtc,
            warehouseNo,
            documentCount = payloadDocuments.Count,
            documents = payloadDocuments
        };

        var artifacts = await WriteArtifactsIfNeededAsync(context, payload, cancellationToken);

        return new AxataSynchronizationTaskExecutionResult(
            payloadDocuments.Count,
            $"{payloadDocuments.Count} merkez depo giris siparisi payload'a donusturuldu.",
            artifacts);
    }

    private static WarehouseOrderListRequest CreateWarehouseOrderListRequest(int lookbackDays, int warehouseNo)
    {
        var normalizedLookbackDays = Math.Max(1, lookbackDays);
        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-(normalizedLookbackDays - 1));

        return new WarehouseOrderListRequest(warehouseNo, startDate, endDate);
    }

    private static int GetRequiredWarehouseNo(AxataSynchronizationTaskExecutionContext context) =>
        context.WarehouseNo is > 0
            ? context.WarehouseNo.Value
            : throw new ArgumentException("Warehouse number is required for this synchronization task.");

    private async Task<IReadOnlyCollection<AxataSynchronizationJobArtifactDto>> WriteArtifactsIfNeededAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Outbox)
        {
            return Array.Empty<AxataSynchronizationJobArtifactDto>();
        }

        return [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
    }
}

internal sealed class CompanyReceivingSyncTaskHandler(
    CompanyMovementListQueryExecutor listQueryExecutor,
    CompanyMovementDetailQueryExecutor detailQueryExecutor,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    AxataSynchronizationOutboxWriter outboxWriter,
    AxataSynchronizationLiveTransportService liveTransportService)
    : IAxataSynchronizationTaskHandler
{
    public string Code => "company-receiving-sync";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateCompanyMovementListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            CompanyMovementKind.IncomingShipment,
            cancellationToken);

        var selected = documents.Take(take).ToArray();
        var previewItems = new List<AxataSynchronizationPreviewItemDto>(selected.Length);

        foreach (var document in selected)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new CompanyMovementDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                CompanyMovementKind.IncomingShipment,
                cancellationToken);

            var payload = AxataSynchronizationPayloadFactory.BuildCompanyReceivingDocument(detail);
            previewItems.Add(new AxataSynchronizationPreviewItemDto(
                $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                $"{document.CustomerDisplayName} / {document.TotalQuantity:0.##} miktar",
                JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options)));
        }

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            warehouseNo,
            documents.Count,
            previewItems.Count,
            DateTime.UtcNow,
            previewItems,
            [$"Son {Math.Max(1, options.CurrentValue.DefaultLookbackDays)} gun icindeki firma mal kabul belgeleri tarandi."]);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateCompanyMovementListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(
            request,
            CompanyMovementKind.IncomingShipment,
            cancellationToken);

        var payloadDocuments = new List<object>(documents.Count);

        foreach (var document in documents)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new CompanyMovementDetailRequest(warehouseNo, document.DocumentSerie, document.DocumentOrderNo),
                CompanyMovementKind.IncomingShipment,
                cancellationToken);

            if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
            {
                var dispatch = await liveTransportService.DispatchCompanyReceivingAsync(
                    context,
                    detail,
                    cancellationToken);
                if (!dispatch.IsSuccess)
                {
                    throw new InvalidOperationException(
                        $"AXATA G01 giris siparisi gonderilemedi. Belge={document.DocumentSerie}.{document.DocumentOrderNo}, State={dispatch.ServiceState}, Message={dispatch.ServiceMessage}");
                }

                payloadDocuments.Add(new
                {
                    document = $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                    dispatch.OperationName,
                    dispatch.ServiceState,
                    dispatch.ServiceMessage
                });
                continue;
            }

            payloadDocuments.Add(AxataSynchronizationPayloadFactory.BuildCompanyReceivingDocument(detail));
        }

        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
        {
            return new AxataSynchronizationTaskExecutionResult(
                payloadDocuments.Count,
                $"{payloadDocuments.Count} firma mal kabul belgesi AXATA G01 inbound order olarak canli gonderildi.",
                Array.Empty<AxataSynchronizationJobArtifactDto>());
        }

        var payload = new
        {
            task = context.Definition.Code,
            taskName = context.Definition.Name,
            generatedAtUtc = context.RequestedAtUtc,
            warehouseNo,
            documentCount = payloadDocuments.Count,
            documents = payloadDocuments
        };

        var artifacts = await WriteArtifactsIfNeededAsync(context, payload, cancellationToken);

        return new AxataSynchronizationTaskExecutionResult(
            payloadDocuments.Count,
            $"{payloadDocuments.Count} firma mal kabul belgesi payload'a donusturuldu.",
            artifacts);
    }

    private static CompanyMovementListRequest CreateCompanyMovementListRequest(int lookbackDays, int warehouseNo)
    {
        var normalizedLookbackDays = Math.Max(1, lookbackDays);
        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-(normalizedLookbackDays - 1));

        return new CompanyMovementListRequest(warehouseNo, startDate, endDate);
    }

    private static int GetRequiredWarehouseNo(AxataSynchronizationTaskExecutionContext context) =>
        context.WarehouseNo is > 0
            ? context.WarehouseNo.Value
            : throw new ArgumentException("Warehouse number is required for this synchronization task.");

    private async Task<IReadOnlyCollection<AxataSynchronizationJobArtifactDto>> WriteArtifactsIfNeededAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Outbox)
        {
            return Array.Empty<AxataSynchronizationJobArtifactDto>();
        }

        return [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
    }
}

internal sealed class InventoryCountSyncTaskHandler(
    InventoryCountListQueryExecutor listQueryExecutor,
    InventoryCountDetailQueryExecutor detailQueryExecutor,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    AxataSynchronizationOutboxWriter outboxWriter,
    IAxataDynamicCensusImportService dynamicCensusImportService)
    : IAxataSynchronizationTaskHandler
{
    public string Code => "inventory-count-sync";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateInventoryCountListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(request, cancellationToken);

        var selected = documents.Take(take).ToArray();
        var previewItems = new List<AxataSynchronizationPreviewItemDto>(selected.Length);

        foreach (var document in selected)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new InventoryCountDetailRequest(warehouseNo, document.DocumentNo, document.DocumentDate ?? DateTime.Today),
                cancellationToken);

            var payload = AxataSynchronizationPayloadFactory.BuildInventoryCountDocument(detail);
            previewItems.Add(new AxataSynchronizationPreviewItemDto(
                document.DocumentNo.ToString(),
                $"{document.LineCount} satir / {document.TotalQuantity:0.##} miktar",
                JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options)));
        }

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            warehouseNo,
            documents.Count,
            previewItems.Count,
            DateTime.UtcNow,
            previewItems,
            [$"Son {Math.Max(1, options.CurrentValue.DefaultLookbackDays)} gun icindeki sayim belgeleri tarandi."]);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live)
        {
            var result = await dynamicCensusImportService.ExecuteAsync(
                new AxataDynamicCensusExecuteRequest(
                    500,
                    ContinueOnError: true,
                    Acknowledge: true),
                context.RequestedByUserId,
                cancellationToken);

            return new AxataSynchronizationTaskExecutionResult(
                result.SucceededLineCount,
                $"{result.SucceededLineCount} AXATA dynamic census satiri Mikro'ya islendi. Created={result.CreatedMovementLineCount}, Failed={result.FailedLineCount}, Skipped={result.SkippedLineCount}.",
                Array.Empty<AxataSynchronizationJobArtifactDto>());
        }

        var warehouseNo = GetRequiredWarehouseNo(context);
        var request = CreateInventoryCountListRequest(options.CurrentValue.DefaultLookbackDays, warehouseNo);
        var documents = await listQueryExecutor.ExecuteAsync(request, cancellationToken);

        var payloadDocuments = new List<object>(documents.Count);

        foreach (var document in documents)
        {
            var detail = await detailQueryExecutor.ExecuteAsync(
                new InventoryCountDetailRequest(warehouseNo, document.DocumentNo, document.DocumentDate ?? DateTime.Today),
                cancellationToken);

            payloadDocuments.Add(AxataSynchronizationPayloadFactory.BuildInventoryCountDocument(detail));
        }

        var payload = new
        {
            task = context.Definition.Code,
            taskName = context.Definition.Name,
            generatedAtUtc = context.RequestedAtUtc,
            warehouseNo,
            documentCount = payloadDocuments.Count,
            documents = payloadDocuments
        };

        var artifacts = await WriteArtifactsIfNeededAsync(context, payload, cancellationToken);

        return new AxataSynchronizationTaskExecutionResult(
            payloadDocuments.Count,
            $"{payloadDocuments.Count} sayim sonucu belgesi payload'a donusturuldu.",
            artifacts);
    }

    private static InventoryCountListRequest CreateInventoryCountListRequest(int lookbackDays, int warehouseNo)
    {
        var normalizedLookbackDays = Math.Max(1, lookbackDays);
        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-(normalizedLookbackDays - 1));

        return new InventoryCountListRequest(warehouseNo, startDate, endDate);
    }

    private static int GetRequiredWarehouseNo(AxataSynchronizationTaskExecutionContext context) =>
        context.WarehouseNo is > 0
            ? context.WarehouseNo.Value
            : throw new ArgumentException("Warehouse number is required for this synchronization task.");

    private async Task<IReadOnlyCollection<AxataSynchronizationJobArtifactDto>> WriteArtifactsIfNeededAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Outbox)
        {
            return Array.Empty<AxataSynchronizationJobArtifactDto>();
        }

        return [await outboxWriter.WritePayloadAsync(context, payload, cancellationToken)];
    }
}

internal sealed record FirmMasterPayloadItem(
    string CustomerCode,
    string Title1,
    string Title2,
    string DisplayName,
    string TaxNumber,
    string TaxOfficeNo,
    string Email,
    string MobilePhone,
    string AddressLine1,
    string AddressLine2,
    int? DefaultInvoiceAddressNo,
    int? DefaultShipmentAddressNo,
    bool EInvoiceEnabled,
    bool EDespatchEnabled);

internal sealed record ProductMasterPayloadItem(
    string ProductCode,
    string ProductName,
    string Barcode,
    string UnitName,
    int PluNo,
    byte RetailTaxRate,
    bool ScaleProduct,
    bool BarcodeContent,
    bool SaleBlocked,
    bool OrderBlocked,
    bool GoodsAcceptanceBlocked);
