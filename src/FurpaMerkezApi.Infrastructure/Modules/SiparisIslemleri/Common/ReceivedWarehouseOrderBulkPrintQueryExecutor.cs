using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

public sealed class ReceivedWarehouseOrderBulkPrintQueryExecutor(MikroDbContext mikroDbContext)
{
    private const double QuantityTolerance = 0.000001d;

    public async Task<IReadOnlyCollection<WarehouseOrderDetailDto>> ExecuteAsync(
        IReadOnlyCollection<WarehouseOrderDetailRequest> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count is < 1 or > 100)
        {
            throw new ArgumentException("Between 1 and 100 warehouse orders must be requested.", nameof(requests));
        }

        var normalizedRequests = requests
            .Select(ValidateAndNormalize)
            .DistinctBy(CreateIdentity, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var warehouseNos = normalizedRequests.Select(request => request.WarehouseNo).Distinct().ToArray();
        var documentSeries = normalizedRequests.Select(request => request.DocumentSerie).Distinct().ToArray();
        var documentOrderNos = normalizedRequests.Select(request => request.DocumentOrderNo).Distinct().ToArray();
        var requestedIdentities = normalizedRequests
            .Select(CreateIdentity)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = await (
            from order in mikroDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking()
            where warehouseNos.Contains(order.ssip_cikdepo ?? 0) &&
                  documentSeries.Contains(order.ssip_evrakno_seri!) &&
                  documentOrderNos.Contains(order.ssip_evrakno_sira ?? -1)
            join inWarehouse in mikroDbContext.DEPOLARs.AsNoTracking() on order.ssip_girdepo equals inWarehouse.dep_no into inWarehouseGroup
            from inWarehouse in inWarehouseGroup.DefaultIfEmpty()
            join outWarehouse in mikroDbContext.DEPOLARs.AsNoTracking() on order.ssip_cikdepo equals outWarehouse.dep_no into outWarehouseGroup
            from outWarehouse in outWarehouseGroup.DefaultIfEmpty()
            join stock in mikroDbContext.STOKLARs.AsNoTracking() on order.ssip_stok_kod equals stock.sto_kod into stockGroup
            from stock in stockGroup.DefaultIfEmpty()
            select new PrintRow(
                order.ssip_tarih,
                order.ssip_teslim_tarih,
                order.ssip_evrakno_seri,
                order.ssip_evrakno_sira,
                order.ssip_belgeno,
                order.ssip_Guid,
                order.ssip_satirno,
                order.ssip_stok_kod,
                stock.sto_isim,
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
                inWarehouse.dep_adi,
                order.ssip_cikdepo,
                outWarehouse.dep_adi))
            .ToListAsync(cancellationToken);

        var rowsByIdentity = candidates
            .Where(row => requestedIdentities.Contains(CreateIdentity(row)))
            .GroupBy(CreateIdentity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var missingDocuments = normalizedRequests
            .Where(request => !rowsByIdentity.ContainsKey(CreateIdentity(request)))
            .Select(request => $"{request.DocumentSerie}/{request.DocumentOrderNo}")
            .ToArray();

        if (missingDocuments.Length > 0)
        {
            throw new KeyNotFoundException(
                $"Warehouse order detail was not found: {string.Join(", ", missingDocuments)}.");
        }

        return normalizedRequests
            .Select(request => BuildDocument(request, rowsByIdentity[CreateIdentity(request)]))
            .ToArray();
    }

    private static WarehouseOrderDetailRequest ValidateAndNormalize(WarehouseOrderDetailRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request));
        }

        return request with { DocumentSerie = request.DocumentSerie.Trim() };
    }

    private static WarehouseOrderDetailDto BuildDocument(
        WarehouseOrderDetailRequest request,
        IReadOnlyCollection<PrintRow> rows)
    {
        var orderedRows = rows.OrderBy(row => row.LineNo).ThenBy(row => row.StockCode).ToArray();
        var firstRow = orderedRows[0];
        var headerCount = orderedRows
            .Select(row => new
            {
                DocumentDate = row.DocumentDate?.Date,
                row.DocumentNumber,
                row.InWarehouseNo,
                row.OutWarehouseNo
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                $"More than one warehouse order matched {request.DocumentSerie}/{request.DocumentOrderNo}.");
        }

        var inWarehouseNo = firstRow.InWarehouseNo ?? 0;
        var outWarehouseNo = firstRow.OutWarehouseNo ?? 0;
        var documentSerie = firstRow.DocumentSerie ?? request.DocumentSerie;
        var documentOrderNo = firstRow.DocumentOrderNo ?? request.DocumentOrderNo;

        var items = orderedRows.Select(row =>
        {
            var unitPointer = NormalizeUnitPointer(row.UnitPointer);
            var quantity = row.Quantity ?? 0d;
            var deliveredQuantity = row.DeliveredQuantity ?? 0d;
            var remainingQuantity = quantity - deliveredQuantity;

            return new WarehouseOrderLineItemDto(
                row.LineGuid,
                row.LineNo ?? 0,
                row.StockCode ?? string.Empty,
                row.StockName ?? string.Empty,
                ResolveUnitName(unitPointer, row.Unit1Name, row.Unit2Name, row.Unit3Name, row.Unit4Name),
                unitPointer,
                quantity,
                deliveredQuantity,
                remainingQuantity,
                row.UnitPrice ?? 0d,
                row.LineAmount ?? 0d,
                row.IsClosed == true || deliveredQuantity + QuantityTolerance >= quantity,
                row.Description ?? string.Empty,
                row.PackageCode ?? string.Empty,
                row.ProjectCode ?? string.Empty);
        }).ToArray();

        return new WarehouseOrderDetailDto(
            new WarehouseOrderHeaderDto(
                WarehouseOrderDocumentKey.Create(outWarehouseNo, documentSerie, documentOrderNo),
                firstRow.DocumentDate ?? DateTime.MinValue,
                orderedRows.Max(row => row.DeliveryDate),
                documentSerie,
                documentOrderNo,
                firstRow.DocumentNumber ?? string.Empty,
                outWarehouseNo,
                firstRow.OutWarehouseName ?? string.Empty,
                inWarehouseNo,
                firstRow.InWarehouseName ?? string.Empty,
                inWarehouseNo,
                firstRow.InWarehouseName ?? string.Empty,
                outWarehouseNo,
                firstRow.OutWarehouseName ?? string.Empty,
                items.Length,
                items.Sum(item => item.Quantity),
                items.Sum(item => item.DeliveredQuantity),
                items.Sum(item => item.RemainingQuantity),
                items.Sum(item => item.LineAmount),
                items.All(item => item.IsClosed)),
            items);
    }

    private static string CreateIdentity(WarehouseOrderDetailRequest request) =>
        $"{request.WarehouseNo}\u001f{request.DocumentSerie}\u001f{request.DocumentOrderNo}";

    private static string CreateIdentity(PrintRow row) =>
        $"{row.OutWarehouseNo ?? 0}\u001f{row.DocumentSerie}\u001f{row.DocumentOrderNo ?? -1}";

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

    private sealed record PrintRow(
        DateTime? DocumentDate,
        DateTime? DeliveryDate,
        string? DocumentSerie,
        int? DocumentOrderNo,
        string? DocumentNumber,
        Guid LineGuid,
        int? LineNo,
        string? StockCode,
        string? StockName,
        string? Unit1Name,
        string? Unit2Name,
        string? Unit3Name,
        string? Unit4Name,
        byte? UnitPointer,
        double? Quantity,
        double? DeliveredQuantity,
        double? UnitPrice,
        double? LineAmount,
        bool? IsClosed,
        string? Description,
        string? PackageCode,
        string? ProjectCode,
        int? InWarehouseNo,
        string? InWarehouseName,
        int? OutWarehouseNo,
        string? OutWarehouseName);
}
