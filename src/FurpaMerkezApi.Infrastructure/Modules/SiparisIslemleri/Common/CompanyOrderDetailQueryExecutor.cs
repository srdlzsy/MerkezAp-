using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

public sealed class CompanyOrderDetailQueryExecutor(MikroDbContext mikroDbContext)
{
    internal async Task<CompanyOrderDetailDto> ExecuteAsync(
        CompanyOrderDetailRequest request,
        CompanyOrderListDirection direction,
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
        var orderType = direction == CompanyOrderListDirection.Issued ? (byte)1 : (byte)0;

        var rows = await (
            from order in mikroDbContext.SIPARISLERs.AsNoTracking()
            where order.sip_depono == request.WarehouseNo &&
                  order.sip_cins == 0 &&
                  order.sip_tip == orderType &&
                  order.sip_evrakno_seri == documentSerie &&
                  order.sip_evrakno_sira == request.DocumentOrderNo
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking() on order.sip_depono equals warehouse.dep_no into warehouseGroup
            from warehouse in warehouseGroup.DefaultIfEmpty()
            join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking() on order.sip_musteri_kod equals customer.cari_kod into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join address in mikroDbContext.CARI_HESAP_ADRESLERIs.AsNoTracking()
                on new
                {
                    CustomerCode = order.sip_musteri_kod,
                    AddressNo = order.sip_adresno ?? 1
                }
                equals new
                {
                    CustomerCode = address.adr_cari_kod,
                    AddressNo = address.adr_adres_no ?? 0
                }
                into addressGroup
            from address in addressGroup.DefaultIfEmpty()
            join stock in mikroDbContext.STOKLARs.AsNoTracking() on order.sip_stok_kod equals stock.sto_kod into stockGroup
            from stock in stockGroup.DefaultIfEmpty()
            orderby order.sip_satirno, order.sip_stok_kod
            select new
            {
                order.sip_tarih,
                order.sip_teslim_tarih,
                order.sip_Guid,
                order.sip_evrakno_seri,
                order.sip_evrakno_sira,
                order.sip_belgeno,
                order.sip_depono,
                WarehouseName = warehouse.dep_adi,
                order.sip_musteri_kod,
                CustomerName = customer.cari_unvan1,
                CustomerTitle = customer.cari_unvan2,
                CustomerRepresentativeCode = address.adr_temsilci_kodu ?? customer.cari_temsilci_kodu,
                AddressLine = address.adr_cadde,
                Neighborhood = address.adr_mahalle,
                Street = address.adr_sokak,
                District = address.adr_ilce,
                Province = address.adr_il,
                order.sip_aciklama,
                order.sip_aciklama2,
                order.sip_HareketGrupKodu2,
                order.sip_HareketGrupKodu3,
                order.sip_cagrilabilir_fl,
                order.sip_satirno,
                order.sip_stok_kod,
                StockName = stock.sto_isim,
                stock.sto_birim1_ad,
                stock.sto_birim2_ad,
                stock.sto_birim3_ad,
                stock.sto_birim4_ad,
                order.sip_birim_pntr,
                order.sip_miktar,
                order.sip_teslim_miktar,
                order.sip_b_fiyat,
                order.sip_tutar,
                order.sip_kapat_fl,
                order.sip_paket_kod,
                order.sip_projekodu
            }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException("Company order detail was not found.");
        }

        var headerCount = rows
            .Select(row => new
            {
                DocumentDate = row.sip_tarih?.Date,
                row.sip_belgeno,
                row.sip_depono,
                row.sip_musteri_kod
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one company order matched the requested serie and order number for the selected warehouse.");
        }

        var firstRow = rows[0];
        var warehouseNo = firstRow.sip_depono ?? request.WarehouseNo;
        var normalizedDocumentSerie = firstRow.sip_evrakno_seri ?? documentSerie;
        var normalizedDocumentOrderNo = firstRow.sip_evrakno_sira ?? request.DocumentOrderNo;

        var items = rows
            .Select(row =>
            {
                var unitPointer = NormalizeUnitPointer(row.sip_birim_pntr);
                var quantity = row.sip_miktar ?? 0d;
                var deliveredQuantity = row.sip_teslim_miktar ?? 0d;

                return new CompanyOrderLineItemDto(
                    row.sip_satirno ?? 0,
                    row.sip_stok_kod ?? string.Empty,
                    row.StockName ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
                    quantity,
                    deliveredQuantity,
                    quantity - deliveredQuantity,
                    row.sip_b_fiyat ?? 0d,
                    row.sip_tutar ?? 0d,
                    row.sip_kapat_fl ?? false,
                    row.sip_aciklama ?? string.Empty,
                    row.sip_paket_kod ?? string.Empty,
                    row.sip_projekodu ?? string.Empty,
                    row.sip_Guid);
            })
            .ToArray();

        var header = new CompanyOrderHeaderDto(
            CompanyOrderDocumentKey.CreateOrNull(warehouseNo, normalizedDocumentSerie, normalizedDocumentOrderNo),
            firstRow.sip_tarih ?? DateTime.MinValue,
            rows.Max(row => row.sip_teslim_tarih),
            normalizedDocumentSerie,
            normalizedDocumentOrderNo,
            firstRow.sip_belgeno ?? string.Empty,
            warehouseNo,
            firstRow.WarehouseName ?? string.Empty,
            firstRow.sip_musteri_kod ?? string.Empty,
            firstRow.CustomerName ?? string.Empty,
            firstRow.CustomerTitle ?? string.Empty,
            JoinNonEmpty(firstRow.CustomerName, firstRow.CustomerTitle),
            JoinNonEmpty(firstRow.AddressLine, firstRow.Neighborhood, firstRow.Street, firstRow.District, firstRow.Province),
            firstRow.CustomerRepresentativeCode ?? string.Empty,
            firstRow.sip_aciklama ?? string.Empty,
            firstRow.sip_aciklama2 ?? string.Empty,
            firstRow.sip_HareketGrupKodu2 ?? string.Empty,
            firstRow.sip_HareketGrupKodu3 ?? string.Empty,
            firstRow.sip_cagrilabilir_fl ?? false,
            items.Length,
            items.Sum(item => item.Quantity),
            items.Sum(item => item.DeliveredQuantity),
            items.Sum(item => item.RemainingQuantity),
            items.Sum(item => item.LineAmount),
            items.All(item => item.IsClosed));

        return new CompanyOrderDetailDto(header, items);
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

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
}
