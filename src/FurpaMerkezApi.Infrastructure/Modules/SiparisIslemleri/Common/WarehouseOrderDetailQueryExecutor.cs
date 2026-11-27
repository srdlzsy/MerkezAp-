using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

public sealed class WarehouseOrderDetailQueryExecutor(
    MikroDbContext mikroDbContext,
    AuthDbContext authDbContext,
    ILogger<WarehouseOrderDetailQueryExecutor> logger,
    IOptionsMonitor<GreenGrocerProductCaseOptions>? greenGrocerOptions = null,
    IGreenGrocerProductCaseService? greenGrocerProductCaseService = null)
{
    private const double QuantityTolerance = 0.000001d;

    internal async Task<WarehouseOrderDetailDto> ExecuteAsync(
        WarehouseOrderDetailRequest request,
        WarehouseOrderListDirection direction,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        var isIssued = direction == WarehouseOrderListDirection.Issued;
        var documentSerie = request.DocumentSerie.Trim();
        var rawDocumentSerie = request.DocumentSerie;

        var baseQuery = mikroDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                (order.ssip_evrakno_seri == documentSerie ||
                 order.ssip_evrakno_seri == rawDocumentSerie ||
                 (order.ssip_evrakno_seri != null && order.ssip_evrakno_seri.Trim() == documentSerie)) &&
                order.ssip_evrakno_sira == request.DocumentOrderNo);

        baseQuery = isIssued
            ? baseQuery.Where(order => order.ssip_girdepo == request.WarehouseNo)
            : baseQuery.Where(order => order.ssip_cikdepo == request.WarehouseNo);

        var rows = await (
            from order in baseQuery
            join inWarehouse in mikroDbContext.DEPOLARs.AsNoTracking() on order.ssip_girdepo equals inWarehouse.dep_no into inWarehouseGroup
            from inWarehouse in inWarehouseGroup.DefaultIfEmpty()
            join outWarehouse in mikroDbContext.DEPOLARs.AsNoTracking() on order.ssip_cikdepo equals outWarehouse.dep_no into outWarehouseGroup
            from outWarehouse in outWarehouseGroup.DefaultIfEmpty()
            join stock in mikroDbContext.STOKLARs.AsNoTracking() on order.ssip_stok_kod equals stock.sto_kod into stockGroup
            from stock in stockGroup.DefaultIfEmpty()
            orderby order.ssip_satirno, order.ssip_stok_kod
            select new
            {
                order.ssip_tarih,
                order.ssip_teslim_tarih,
                order.ssip_evrakno_seri,
                order.ssip_evrakno_sira,
                order.ssip_belgeno,
                order.ssip_Guid,
                order.ssip_satirno,
                order.ssip_stok_kod,
                StockName = stock.sto_isim,
                ModelCode = stock.sto_model_kodu,
                stock.sto_birim1_ad,
                stock.sto_birim2_ad,
                stock.sto_birim3_ad,
                stock.sto_birim4_ad,
                order.ssip_birim_pntr,
                order.ssip_miktar,
                order.ssip_teslim_miktar,
                order.ssip_b_fiyat,
                order.ssip_tutar,
                order.ssip_kapat_fl,
                order.ssip_aciklama,
                order.ssip_paket_kod,
                order.ssip_projekodu,
                order.ssip_girdepo,
                InWarehouseName = inWarehouse.dep_adi,
                order.ssip_cikdepo,
                OutWarehouseName = outWarehouse.dep_adi
            }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException("Warehouse order detail was not found.");
        }

        var headerCount = rows
            .Select(row => new
            {
                DocumentDate = row.ssip_tarih?.Date,
                row.ssip_belgeno,
                row.ssip_girdepo,
                row.ssip_cikdepo
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one warehouse order matched the requested serie and order number for the selected warehouse.");
        }

        var firstRow = rows[0];
        var inWarehouseNo = firstRow.ssip_girdepo ?? 0;
        var inWarehouseName = firstRow.InWarehouseName ?? string.Empty;
        var outWarehouseNo = firstRow.ssip_cikdepo ?? 0;
        var outWarehouseName = firstRow.OutWarehouseName ?? string.Empty;
        var warehouseNo = isIssued ? inWarehouseNo : outWarehouseNo;
        var warehouseName = isIssued ? inWarehouseName : outWarehouseName;
        var relatedWarehouseNo = isIssued ? outWarehouseNo : inWarehouseNo;
        var relatedWarehouseName = isIssued ? outWarehouseName : inWarehouseName;
        var normalizedDocumentSerie = firstRow.ssip_evrakno_seri ?? documentSerie;
        var normalizedDocumentOrderNo = firstRow.ssip_evrakno_sira ?? request.DocumentOrderNo;

        var greenGrocerCaseByLineGuid = await GetGreenGrocerCaseByLineGuidAsync(
            rows.Select(row => row.ssip_Guid).ToArray(),
            cancellationToken);
        await AddLiveGreenGrocerCaseEstimatesAsync(
            rows.Select(row => new LiveGreenGrocerOrderLine(
                row.ssip_Guid,
                row.ssip_stok_kod ?? string.Empty,
                row.ModelCode ?? string.Empty,
                row.ssip_miktar ?? 0d,
                row.ssip_teslim_miktar ?? 0d)).ToArray(),
            greenGrocerCaseByLineGuid,
            outWarehouseNo,
            inWarehouseNo,
            firstRow.ssip_tarih,
            cancellationToken);
        var stockCodes = rows
            .Select(row => row.ssip_stok_kod)
            .Where(stockCode => !string.IsNullOrWhiteSpace(stockCode))
            .Select(stockCode => stockCode!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var barcodeRows = await mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(barcode =>
                barcode.bar_stokkodu != null &&
                stockCodes.Contains(barcode.bar_stokkodu) &&
                barcode.bar_kodu != null &&
                barcode.bar_kodu != string.Empty)
            .Select(barcode => new { barcode.bar_stokkodu, barcode.bar_kodu })
            .ToListAsync(cancellationToken);
        var barcodeByStockCode = barcodeRows
            .GroupBy(row => row.bar_stokkodu!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.bar_kodu!.Trim()).OrderBy(value => value).FirstOrDefault() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        var items = rows
            .Select(row =>
            {
                var unitPointer = NormalizeUnitPointer(row.ssip_birim_pntr);
                var quantity = row.ssip_miktar ?? 0d;
                var deliveredQuantity = row.ssip_teslim_miktar ?? 0d;
                var remainingQuantity = quantity - deliveredQuantity;
                var isClosed = row.ssip_kapat_fl == true ||
                               deliveredQuantity + QuantityTolerance >= quantity;

                return new WarehouseOrderLineItemDto(
                    row.ssip_Guid,
                    row.ssip_satirno ?? 0,
                    row.ssip_stok_kod ?? string.Empty,
                    row.StockName ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
                    quantity,
                    deliveredQuantity,
                    remainingQuantity,
                    row.ssip_b_fiyat ?? 0d,
                    row.ssip_tutar ?? 0d,
                    isClosed,
                    row.ssip_aciklama ?? string.Empty,
                    row.ssip_paket_kod ?? string.Empty,
                    row.ssip_projekodu ?? string.Empty,
                    greenGrocerCaseByLineGuid.GetValueOrDefault(row.ssip_Guid),
                    row.ModelCode ?? string.Empty,
                    barcodeByStockCode.GetValueOrDefault(row.ssip_stok_kod?.Trim() ?? string.Empty, string.Empty));
            })
            .ToArray();

        var header = new WarehouseOrderHeaderDto(
            WarehouseOrderDocumentKey.Create(warehouseNo, normalizedDocumentSerie, normalizedDocumentOrderNo),
            firstRow.ssip_tarih ?? DateTime.MinValue,
            rows.Max(row => row.ssip_teslim_tarih),
            normalizedDocumentSerie,
            normalizedDocumentOrderNo,
            firstRow.ssip_belgeno ?? string.Empty,
            warehouseNo,
            warehouseName,
            relatedWarehouseNo,
            relatedWarehouseName,
            inWarehouseNo,
            inWarehouseName,
            outWarehouseNo,
            outWarehouseName,
            items.Length,
            items.Sum(item => item.Quantity),
            items.Sum(item => item.DeliveredQuantity),
            items.Sum(item => item.RemainingQuantity),
            items.Sum(item => item.LineAmount),
            items.All(item => item.IsClosed));

        return new WarehouseOrderDetailDto(header, items);
    }

    private async Task<Dictionary<Guid, WarehouseOrderLineGreenGrocerCaseDto>> GetGreenGrocerCaseByLineGuidAsync(
        IReadOnlyCollection<Guid> lineGuids,
        CancellationToken cancellationToken)
    {
        if (greenGrocerOptions?.CurrentValue.AuthDbTrackingEnabled != true)
        {
            return new Dictionary<Guid, WarehouseOrderLineGreenGrocerCaseDto>();
        }

        var normalizedLineGuids = lineGuids
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedLineGuids.Length == 0)
        {
            return new Dictionary<Guid, WarehouseOrderLineGreenGrocerCaseDto>();
        }

        try
        {
            var snapshots = await authDbContext.GreenGrocerOrderLineSnapshots
                .AsNoTracking()
                .Where(snapshot => normalizedLineGuids.Contains(snapshot.WarehouseOrderLineGuid))
                .Select(snapshot => new
                {
                    snapshot.WarehouseOrderLineGuid,
                    snapshot.InputQuantity,
                    snapshot.InputMode,
                    snapshot.ConversionMode,
                    snapshot.EstimatedQuantity,
                    snapshot.MicroUnit,
                    snapshot.AverageKgPerCase,
                    snapshot.UnitsPerCase,
                    snapshot.AverageSource,
                    snapshot.AverageRecordCount,
                    snapshot.AverageCaseCount,
                    snapshot.CoefficientOfVariation,
                    snapshot.Confidence,
                    snapshot.RequiresManualApproval,
                    snapshot.AllowOrderLinking,
                    snapshot.OverDeliveryTolerancePercent,
                    snapshot.SevereOverDeliveryMultiplier,
                    snapshot.ActualShippedQuantity,
                    snapshot.ActualShippedCaseCount,
                    snapshot.Status,
                    snapshot.CalculationTier,
                    snapshot.AverageWindowDaysUsed,
                    snapshot.LabelAgeDays,
                    snapshot.PriorAverageKgPerCase,
                    snapshot.DriftPercent,
                    snapshot.ManualApprovalConfirmed,
                    snapshot.ApprovedKgPerCase
                })
                .ToListAsync(cancellationToken);

            return snapshots.ToDictionary(
                snapshot => snapshot.WarehouseOrderLineGuid,
                snapshot => new WarehouseOrderLineGreenGrocerCaseDto(
                    Round(snapshot.InputQuantity),
                    snapshot.InputMode,
                    snapshot.ConversionMode,
                    Round(snapshot.EstimatedQuantity),
                    snapshot.MicroUnit,
                    RoundOrNull(snapshot.AverageKgPerCase),
                    RoundOrNull(snapshot.UnitsPerCase),
                    snapshot.AverageSource,
                    snapshot.AverageRecordCount,
                    snapshot.AverageCaseCount,
                    RoundOrNull(snapshot.CoefficientOfVariation),
                    snapshot.Confidence,
                    snapshot.RequiresManualApproval,
                    snapshot.AllowOrderLinking,
                    Round(snapshot.OverDeliveryTolerancePercent),
                    Round(snapshot.SevereOverDeliveryMultiplier),
                    RoundOrNull(snapshot.ActualShippedQuantity),
                    RoundOrNull(snapshot.ActualShippedCaseCount),
                    snapshot.Status,
                    snapshot.CalculationTier,
                    snapshot.AverageWindowDaysUsed,
                    snapshot.LabelAgeDays,
                    RoundOrNull(snapshot.PriorAverageKgPerCase),
                    snapshot.DriftPercent.HasValue ? Math.Round(snapshot.DriftPercent.Value, 1, MidpointRounding.AwayFromZero) : null,
                    snapshot.ManualApprovalConfirmed,
                    RoundOrNull(snapshot.ApprovedKgPerCase)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Manav siparis kasa snapshot bilgisi okunamadi; Mikro siparis detayi kasa bilgisi olmadan dondurulecek.");
            return new Dictionary<Guid, WarehouseOrderLineGreenGrocerCaseDto>();
        }
    }

    private async Task AddLiveGreenGrocerCaseEstimatesAsync(
        IReadOnlyCollection<LiveGreenGrocerOrderLine> lines,
        IDictionary<Guid, WarehouseOrderLineGreenGrocerCaseDto> casesByLineGuid,
        int sourceWarehouseNo,
        int targetWarehouseNo,
        DateTime? orderDate,
        CancellationToken cancellationToken)
    {
        var options = greenGrocerOptions?.CurrentValue;
        if (options is not { Enabled: true } ||
            greenGrocerProductCaseService is null ||
            sourceWarehouseNo != options.SourceWarehouseNo)
        {
            return;
        }

        foreach (var line in lines)
        {
            if (casesByLineGuid.ContainsKey(line.LineGuid) ||
                line.Quantity <= QuantityTolerance ||
                !GreenGrocerShipmentLineNormalizer.IsGreenGrocerModelCode(line.ModelCode))
            {
                continue;
            }

            try
            {
                var resolution = await greenGrocerProductCaseService.PreviewResolutionAsync(
                    new GreenGrocerProductCaseResolutionRequest(
                        line.StockCode,
                        1d,
                        sourceWarehouseNo,
                        targetWarehouseNo,
                        orderDate),
                    cancellationToken);

                if (!resolution.IsUsable ||
                    resolution.AverageKgPerCase is not > QuantityTolerance ||
                    !string.Equals(resolution.MicroUnit, "KG", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var estimatedCaseCount = line.Quantity / resolution.AverageKgPerCase.Value;
                var deliveredCaseCount = line.DeliveredQuantity / resolution.AverageKgPerCase.Value;
                casesByLineGuid[line.LineGuid] = new WarehouseOrderLineGreenGrocerCaseDto(
                    Round(estimatedCaseCount),
                    GreenGrocerProductCaseModes.InputCase,
                    resolution.ConversionMode,
                    Round(line.Quantity),
                    resolution.MicroUnit,
                    RoundOrNull(resolution.AverageKgPerCase),
                    RoundOrNull(resolution.UnitsPerCase),
                    resolution.AverageSource,
                    resolution.AverageRecordCount,
                    resolution.AverageCaseCount,
                    RoundOrNull(resolution.CoefficientOfVariation),
                    resolution.Confidence,
                    false,
                    resolution.IsOrderLinkable,
                    Round(resolution.OverDeliveryTolerancePercent),
                    Round(resolution.SevereOverDeliveryMultiplier),
                    Round(line.DeliveredQuantity),
                    Round(deliveredCaseCount),
                    "LiveEstimate",
                    resolution.CalculationTier,
                    resolution.AverageWindowDaysUsed,
                    resolution.LabelAgeDays,
                    RoundOrNull(resolution.PriorAverageKgPerCase),
                    resolution.DriftPercent.HasValue
                        ? Math.Round(resolution.DriftPercent.Value, 1, MidpointRounding.AwayFromZero)
                        : null,
                    false,
                    null,
                    true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Manav siparis kasa tahmini uretilemedi; satir Mikro miktariyla dondurulecek. LineGuid={LineGuid}, StockCode={StockCode}",
                    line.LineGuid,
                    line.StockCode);
            }
        }
    }

    private static byte NormalizeUnitPointer(byte? unitPointer) =>
        unitPointer is >= 1 and <= 4 ? unitPointer.Value : (byte)1;

    private static string ResolveUnitName(
        byte unitPointer,
        string? unit1Name,
        string? unit2Name,
        string? unit3Name,
        string? unit4Name) =>
        unitPointer switch
        {
            2 => unit2Name ?? unit1Name ?? string.Empty,
            3 => unit3Name ?? unit1Name ?? string.Empty,
            4 => unit4Name ?? unit1Name ?? string.Empty,
            _ => unit1Name ?? string.Empty
        };

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double? RoundOrNull(double? value) =>
        value.HasValue ? Round(value.Value) : null;

    private sealed record LiveGreenGrocerOrderLine(
        Guid LineGuid,
        string StockCode,
        string ModelCode,
        double Quantity,
        double DeliveredQuantity);
}
