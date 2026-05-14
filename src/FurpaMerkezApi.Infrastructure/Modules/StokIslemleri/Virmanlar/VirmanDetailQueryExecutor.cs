using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar;

public sealed class VirmanDetailQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte VirmanDocumentType = 6;
    private const byte VirmanMovementGenre = 3;
    private const byte NormalMovement = 0;

    internal async Task<VirmanDetailDto> ExecuteAsync(
        VirmanDetailRequest request,
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

        var documentSerie = request.DocumentSerie.Trim();

        var rows = await (
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            where movement.sth_evraktip == VirmanDocumentType &&
                  movement.sth_normal_iade == NormalMovement &&
                  movement.sth_cins == VirmanMovementGenre &&
                  movement.sth_cikis_depo_no == request.WarehouseNo &&
                  movement.sth_evrakno_seri == documentSerie &&
                  movement.sth_evrakno_sira == request.DocumentOrderNo
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals warehouse.dep_no into warehouseGroup
            from warehouse in warehouseGroup.DefaultIfEmpty()
            join stock in mikroDbContext.STOKLARs.AsNoTracking()
                on movement.sth_stok_kod equals stock.sto_kod into stockGroup
            from stock in stockGroup.DefaultIfEmpty()
            orderby movement.sth_satirno, movement.sth_stok_kod
            select new
            {
                movement.sth_belge_tarih,
                movement.sth_create_date,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                WarehouseName = warehouse.dep_adi,
                movement.sth_evraktip,
                movement.sth_cins,
                movement.sth_tip,
                movement.sth_aciklama,
                movement.sth_satirno,
                movement.sth_stok_kod,
                StockName = stock.sto_isim,
                stock.sto_birim1_ad,
                stock.sto_birim2_ad,
                stock.sto_birim3_ad,
                stock.sto_birim4_ad,
                movement.sth_birim_pntr,
                movement.sth_miktar,
                movement.sth_miktar2,
                movement.sth_tutar,
                movement.sth_parti_kodu,
                movement.sth_lot_no,
                movement.sth_proje_kodu
            }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException("Virman detail was not found.");
        }

        var firstRow = rows[0];
        var items = rows
            .Select(row =>
            {
                var unitPointer = NormalizeUnitPointer(row.sth_birim_pntr);
                var quantity = row.sth_miktar ?? 0d;
                var lineAmount = row.sth_tutar ?? 0d;

                return new VirmanLineItemDto(
                    row.sth_satirno ?? 0,
                    row.sth_stok_kod ?? string.Empty,
                    row.StockName ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
                    row.sth_tip ?? 0,
                    quantity,
                    row.sth_miktar2 ?? 0d,
                    quantity == 0d ? 0d : lineAmount / quantity,
                    lineAmount,
                    row.sth_aciklama ?? string.Empty,
                    row.sth_parti_kodu ?? string.Empty,
                    row.sth_lot_no ?? 0,
                    row.sth_proje_kodu ?? string.Empty);
            })
            .ToArray();

        var header = new VirmanHeaderDto(
            firstRow.sth_belge_tarih,
            rows.Min(row => row.sth_create_date),
            firstRow.sth_tarih,
            firstRow.sth_belge_no ?? string.Empty,
            firstRow.sth_evrakno_seri ?? documentSerie,
            firstRow.sth_evrakno_sira ?? request.DocumentOrderNo,
            firstRow.sth_cikis_depo_no ?? request.WarehouseNo,
            firstRow.WarehouseName ?? string.Empty,
            firstRow.sth_evraktip ?? 0,
            firstRow.sth_cins ?? 0,
            rows.Select(row => row.sth_tip ?? 0).Distinct().OrderBy(movementType => movementType).ToArray(),
            firstRow.sth_aciklama ?? string.Empty,
            items.Length,
            items.Sum(item => item.Quantity),
            items.Sum(item => item.LineAmount));

        return new VirmanDetailDto(header, items);
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
