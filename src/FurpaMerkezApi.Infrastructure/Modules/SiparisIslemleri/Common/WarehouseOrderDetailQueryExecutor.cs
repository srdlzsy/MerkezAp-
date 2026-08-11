using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

public sealed class WarehouseOrderDetailQueryExecutor(
    MikroDbContext mikroDbContext,
    AuthDbContext authDbContext)
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

        var baseQuery = mikroDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_evrakno_seri == documentSerie &&
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

        var greenGrocerCaseByLineGuid = await GetGreenGrocerCaseByLineGuidAsync(
            rows.Select(row => row.ssip_Guid).ToArray(),
            cancellationToken);

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
                    greenGrocerCaseByLineGuid.GetValueOrDefault(row.ssip_Guid));
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

    private async Task<IReadOnlyDictionary<Guid, WarehouseOrderLineGreenGrocerCaseDto>> GetGreenGrocerCaseByLineGuidAsync(
        IReadOnlyCollection<Guid> lineGuids,
        CancellationToken cancellationToken)
    {
        var normalizedLineGuids = lineGuids
            .Where(guid => guid != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedLineGuids.Length == 0)
        {
            return new Dictionary<Guid, WarehouseOrderLineGreenGrocerCaseDto>();
        }

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
                snapshot.ActualShippedQuantity,
                snapshot.ActualShippedCaseCount,
                snapshot.Status
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
                RoundOrNull(snapshot.ActualShippedQuantity),
                RoundOrNull(snapshot.ActualShippedCaseCount),
                snapshot.Status));
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
}
