using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

public sealed class CompanyMovementDetailQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte CompanyDispatchDocumentType = 1;
    private const byte ReceivingReceiptDocumentType = 13;
    private const byte IncomingMovementType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;

    internal async Task<CompanyMovementDetailDto> ExecuteAsync(
        CompanyMovementDetailRequest request,
        CompanyMovementKind kind,
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
        var movements = CreateFilteredMovementQuery(
            request.WarehouseNo,
            documentSerie,
            request.DocumentOrderNo,
            kind);

        var rows = await (
            from movement in movements
            join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking()
                on movement.sth_cari_kodu equals customer.cari_kod into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join address in mikroDbContext.CARI_HESAP_ADRESLERIs.AsNoTracking()
                on new
                {
                    CustomerCode = movement.sth_cari_kodu,
                    AddressNo = movement.sth_adres_no ?? 1
                }
                equals new
                {
                    CustomerCode = address.adr_cari_kod,
                    AddressNo = address.adr_adres_no ?? 0
                }
                into addressGroup
            from address in addressGroup.DefaultIfEmpty()
            join inputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_giris_depo_no equals inputWarehouse.dep_no into inputWarehouseGroup
            from inputWarehouse in inputWarehouseGroup.DefaultIfEmpty()
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
                movement.sth_cari_kodu,
                CustomerName = customer.cari_unvan1,
                CustomerTitle = customer.cari_unvan2,
                AddressLine = address.adr_cadde,
                Neighborhood = address.adr_mahalle,
                Street = address.adr_sokak,
                District = address.adr_ilce,
                Province = address.adr_il,
                movement.sth_giris_depo_no,
                InputWarehouseName = inputWarehouse.dep_adi,
                movement.sth_cikis_depo_no,
                OutputWarehouseName = outputWarehouse.dep_adi,
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_normal_iade,
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
                movement.sth_iskonto1,
                movement.sth_iskonto2,
                movement.sth_iskonto3,
                movement.sth_iskonto4,
                movement.sth_iskonto5,
                movement.sth_iskonto6,
                movement.sth_masraf1,
                movement.sth_masraf2,
                movement.sth_masraf3,
                movement.sth_masraf4,
                movement.sth_vergi,
                movement.sth_netagirlik,
                movement.sth_brutagirlik,
                movement.sth_parti_kodu,
                movement.sth_lot_no,
                movement.sth_proje_kodu,
                movement.sth_sip_uid
            }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException("Company movement detail was not found.");
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_belge_tarih,
                row.sth_belge_no,
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cari_kodu,
                row.sth_giris_depo_no,
                row.sth_cikis_depo_no,
                row.sth_evraktip,
                row.sth_tip,
                row.sth_normal_iade
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one company movement matched the requested serie and order number for the selected warehouse.");
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

                return new CompanyMovementLineItemDto(
                    row.sth_satirno ?? 0,
                    row.sth_stok_kod ?? string.Empty,
                    row.StockName ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
                    quantity,
                    row.sth_miktar2 ?? 0d,
                    quantity == 0d ? 0d : lineAmount / quantity,
                    lineAmount,
                    Sum(row.sth_iskonto1, row.sth_iskonto2, row.sth_iskonto3, row.sth_iskonto4, row.sth_iskonto5, row.sth_iskonto6),
                    Sum(row.sth_masraf1, row.sth_masraf2, row.sth_masraf3, row.sth_masraf4),
                    row.sth_vergi ?? 0d,
                    row.sth_netagirlik ?? 0d,
                    row.sth_brutagirlik ?? 0d,
                    row.sth_aciklama ?? string.Empty,
                    row.sth_parti_kodu ?? string.Empty,
                    row.sth_lot_no ?? 0,
                    row.sth_proje_kodu ?? string.Empty,
                    row.sth_sip_uid);
            })
            .ToArray();

        var warehouseNo = kind == CompanyMovementKind.IncomingShipment
            ? firstRow.sth_giris_depo_no ?? request.WarehouseNo
            : firstRow.sth_cikis_depo_no ?? request.WarehouseNo;
        var warehouseName = kind == CompanyMovementKind.IncomingShipment
            ? firstRow.InputWarehouseName ?? string.Empty
            : firstRow.OutputWarehouseName ?? string.Empty;

        var header = new CompanyMovementHeaderDto(
            firstRow.sth_belge_tarih,
            rows.Min(row => row.sth_create_date),
            firstRow.sth_tarih,
            firstRow.sth_belge_no ?? string.Empty,
            normalizedDocumentSerie,
            normalizedDocumentOrderNo,
            firstRow.sth_cari_kodu ?? string.Empty,
            firstRow.CustomerName ?? string.Empty,
            firstRow.CustomerTitle ?? string.Empty,
            JoinNonEmpty(firstRow.CustomerName, firstRow.CustomerTitle),
            JoinNonEmpty(firstRow.AddressLine, firstRow.Neighborhood, firstRow.Street, firstRow.District, firstRow.Province),
            warehouseNo,
            warehouseName,
            firstRow.sth_giris_depo_no ?? 0,
            firstRow.InputWarehouseName ?? string.Empty,
            firstRow.sth_cikis_depo_no ?? 0,
            firstRow.OutputWarehouseName ?? string.Empty,
            firstRow.sth_evraktip ?? 0,
            firstRow.sth_tip ?? 0,
            firstRow.sth_normal_iade ?? 0,
            firstRow.sth_aciklama ?? string.Empty,
            items.Length,
            items.Sum(item => item.Quantity),
            items.Sum(item => item.LineAmount));

        return new CompanyMovementDetailDto(header, items);
    }

    private IQueryable<STOK_HAREKETLERI> CreateFilteredMovementQuery(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        CompanyMovementKind kind)
    {
        var movements = mikroDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo);

        return kind switch
        {
            CompanyMovementKind.OutgoingShipment => movements.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            CompanyMovementKind.IncomingShipment => movements.Where(movement =>
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_giris_depo_no == warehouseNo),

            CompanyMovementKind.PurchaseReturn => movements.Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown company movement kind.")
        };
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

    private static double Sum(params double?[] values) =>
        values.Sum(value => value ?? 0d);

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
}
