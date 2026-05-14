using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

public sealed class StockReceiptDetailQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte StockReceiptDocumentType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte NormalMovement = 0;
    private const byte OutageMovementGenre = 4;
    private const byte ExpenseMovementGenre = 5;

    internal async Task<StockReceiptDetailDto> ExecuteAsync(
        StockReceiptDetailRequest request,
        StockReceiptKind kind,
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

        var movementGenre = ResolveMovementGenre(kind);
        var documentSerie = request.DocumentSerie.Trim();

        var rows = await (
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            where movement.sth_evraktip == StockReceiptDocumentType &&
                  movement.sth_tip == OutgoingMovementType &&
                  movement.sth_normal_iade == NormalMovement &&
                  movement.sth_cins == movementGenre &&
                  movement.sth_cikis_depo_no == request.WarehouseNo &&
                  movement.sth_evrakno_seri == documentSerie &&
                  movement.sth_evrakno_sira == request.DocumentOrderNo
            join outputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals outputWarehouse.dep_no into outputWarehouseGroup
            from outputWarehouse in outputWarehouseGroup.DefaultIfEmpty()
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
                OutputWarehouseName = outputWarehouse.dep_adi,
                movement.sth_HareketGrupKodu1,
                movement.sth_HareketGrupKodu2,
                movement.sth_isemri_gider_kodu,
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_cins,
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
            throw new KeyNotFoundException("Stock receipt detail was not found.");
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_belge_tarih,
                row.sth_belge_no,
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cikis_depo_no,
                row.sth_HareketGrupKodu1,
                row.sth_HareketGrupKodu2,
                row.sth_isemri_gider_kodu,
                row.sth_evraktip,
                row.sth_tip,
                row.sth_cins
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one stock receipt matched the requested serie and order number for the selected warehouse.");
        }

        var firstRow = rows[0];
        var normalizedDocumentSerie = firstRow.sth_evrakno_seri ?? documentSerie;
        var normalizedDocumentOrderNo = firstRow.sth_evrakno_sira ?? request.DocumentOrderNo;

        var items = rows
            .Select(row =>
            {
                var unitPointer = NormalizeUnitPointer(row.sth_birim_pntr);
                var quantity = row.sth_miktar ?? 0d;
                var lineAmount = row.sth_tutar ?? 0d;

                return new StockReceiptLineItemDto(
                    row.sth_satirno ?? 0,
                    row.sth_stok_kod ?? string.Empty,
                    row.StockName ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
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

        var header = new StockReceiptHeaderDto(
            firstRow.sth_belge_tarih,
            rows.Min(row => row.sth_create_date),
            firstRow.sth_tarih,
            firstRow.sth_belge_no ?? string.Empty,
            normalizedDocumentSerie,
            normalizedDocumentOrderNo,
            firstRow.sth_cikis_depo_no ?? request.WarehouseNo,
            firstRow.OutputWarehouseName ?? string.Empty,
            firstRow.sth_HareketGrupKodu1 ?? string.Empty,
            firstRow.sth_HareketGrupKodu2 ?? string.Empty,
            firstRow.sth_isemri_gider_kodu ?? string.Empty,
            firstRow.sth_evraktip ?? 0,
            firstRow.sth_tip ?? 0,
            firstRow.sth_cins ?? 0,
            firstRow.sth_aciklama ?? string.Empty,
            items.Length,
            items.Sum(item => item.Quantity),
            items.Sum(item => item.LineAmount));

        return new StockReceiptDetailDto(header, items);
    }

    private static byte ResolveMovementGenre(StockReceiptKind kind) =>
        kind switch
        {
            StockReceiptKind.OutageReceipt => OutageMovementGenre,
            StockReceiptKind.ExpenseReceipt => ExpenseMovementGenre,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported stock receipt kind.")
        };

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
