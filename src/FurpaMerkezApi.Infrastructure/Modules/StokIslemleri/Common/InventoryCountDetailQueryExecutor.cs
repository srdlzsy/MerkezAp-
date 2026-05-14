using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

public sealed class InventoryCountDetailQueryExecutor(MikroDbContext mikroDbContext)
{
    internal async Task<InventoryCountDetailDto> ExecuteAsync(
        InventoryCountDetailRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.DocumentNo < 0)
        {
            throw new ArgumentException("Document no can not be negative.", nameof(request.DocumentNo));
        }

        var documentDate = request.DocumentDate.Date;
        var documentDateExclusive = documentDate.AddDays(1);

        var rows = await (
            from result in mikroDbContext.SAYIM_SONUCLARIs.AsNoTracking()
            where result.sym_depono == request.WarehouseNo &&
                  result.sym_evrakno == request.DocumentNo &&
                  result.sym_tarihi.HasValue &&
                  result.sym_tarihi.Value >= documentDate &&
                  result.sym_tarihi.Value < documentDateExclusive
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on result.sym_depono equals warehouse.dep_no into warehouseGroup
            from warehouse in warehouseGroup.DefaultIfEmpty()
            join stock in mikroDbContext.STOKLARs.AsNoTracking()
                on result.sym_Stokkodu equals stock.sto_kod into stockGroup
            from stock in stockGroup.DefaultIfEmpty()
            orderby result.sym_satirno, result.sym_Stokkodu
            select new
            {
                result.sym_tarihi,
                result.sym_create_date,
                result.sym_evrakno,
                result.sym_depono,
                WarehouseName = warehouse.dep_adi,
                result.sym_parti_kodu,
                result.sym_satirno,
                result.sym_Stokkodu,
                StockName = stock.sto_isim,
                stock.sto_birim1_ad,
                stock.sto_birim2_ad,
                stock.sto_birim3_ad,
                stock.sto_birim4_ad,
                result.sym_barkod,
                result.sym_birim_pntr,
                result.sym_miktar1,
                result.sym_miktar2,
                result.sym_miktar3,
                result.sym_miktar4,
                result.sym_miktar5
            }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException("Inventory count detail was not found.");
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sym_tarihi,
                row.sym_evrakno,
                row.sym_depono,
                row.sym_parti_kodu
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one inventory count matched the requested document no and date for the selected warehouse.");
        }

        var firstRow = rows[0];
        var items = rows
            .Select(row =>
            {
                var unitPointer = NormalizeUnitPointer(row.sym_birim_pntr);

                return new InventoryCountLineItemDto(
                    row.sym_satirno ?? 0,
                    row.sym_Stokkodu ?? string.Empty,
                    row.StockName ?? string.Empty,
                    row.sym_barkod ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
                    row.sym_miktar1 ?? 0d,
                    row.sym_miktar2 ?? 0d,
                    row.sym_miktar3 ?? 0d,
                    row.sym_miktar4 ?? 0d,
                    row.sym_miktar5 ?? 0d);
            })
            .ToArray();

        var header = new InventoryCountHeaderDto(
            firstRow.sym_tarihi,
            rows.Min(row => row.sym_create_date),
            firstRow.sym_evrakno ?? request.DocumentNo,
            firstRow.sym_depono ?? request.WarehouseNo,
            firstRow.WarehouseName ?? string.Empty,
            firstRow.sym_parti_kodu ?? string.Empty,
            items.Length,
            items.Sum(item => item.Quantity1));

        return new InventoryCountDetailDto(header, items);
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
}
