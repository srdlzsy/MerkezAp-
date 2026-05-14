using System.Globalization;
using FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.EtiketBelgeleri;

public sealed class LabelProductQueryExecutor(MikroDbContext mikroDbContext)
{
    internal async Task<IReadOnlyCollection<LabelPriceChangedProductDto>> ListPriceChangedProductsAsync(
        int warehouseNo,
        DateTime dateTimeFilter,
        CancellationToken cancellationToken)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var rows = await (
            from stock in mikroDbContext.STOKLARs.AsNoTracking()
            let latestPriceChange = mikroDbContext.STOK_FIYAT_DEGISIKLIKLERIs
                .AsNoTracking()
                .Where(item =>
                    item.fid_stok_kod == stock.sto_kod &&
                    item.fid_depo_no == warehouseNo &&
                    item.fid_yapildi_fl == 1 &&
                    (item.fid_tarih ?? item.fid_belge_tarih ?? item.fid_create_date) > dateTimeFilter)
                .OrderByDescending(item => item.fid_tarih ?? item.fid_belge_tarih ?? item.fid_create_date)
                .ThenByDescending(item => item.fid_lastup_date ?? item.fid_create_date)
                .Select(item => new
                {
                    item.fid_eskifiy_tutar,
                    item.fid_yenifiy_tutar,
                    item.fid_tarih,
                    item.fid_belge_tarih,
                    item.fid_create_date
                })
                .FirstOrDefault()
            where latestPriceChange != null &&
                  (stock.sto_satis_dursun ?? 0) == 0
            let currentPrice = mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs
                .AsNoTracking()
                .Where(item =>
                    item.sfiyat_stokkod == stock.sto_kod &&
                    item.sfiyat_deposirano == warehouseNo &&
                    item.sfiyat_birim_pntr == 1)
                .OrderBy(item => item.sfiyat_listesirano ?? int.MaxValue)
                .ThenByDescending(item => item.sfiyat_lastup_date ?? item.sfiyat_create_date)
                .Select(item => item.sfiyat_fiyati)
                .FirstOrDefault()
            let barcode = mikroDbContext.BARKOD_TANIMLARIs
                .AsNoTracking()
                .Where(item => item.bar_stokkodu == stock.sto_kod)
                .OrderByDescending(item => item.bar_master ?? false)
                .ThenBy(item => item.bar_birimpntr ?? 0)
                .ThenByDescending(item => item.bar_create_date)
                .Select(item => item.bar_kodu)
                .FirstOrDefault()
            select new
            {
                stock.sto_kod,
                stock.sto_isim,
                stock.sto_plu_no,
                stock.sto_birim2_ad,
                stock.sto_mensei,
                stock.sto_birim2_katsayi,
                stock.sto_birim1_ad,
                Barcode = barcode,
                CurrentPrice = currentPrice,
                LatestPriceChange = latestPriceChange
            }).ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var price = row.CurrentPrice ?? row.LatestPriceChange?.fid_yenifiy_tutar ?? 0d;
                var oldPrice = row.LatestPriceChange?.fid_eskifiy_tutar ?? price;
                var priceChangeDate = row.LatestPriceChange?.fid_tarih
                    ?? row.LatestPriceChange?.fid_belge_tarih
                    ?? row.LatestPriceChange?.fid_create_date;

                return new
                {
                    SortDate = priceChangeDate ?? DateTime.MinValue,
                    Product = new LabelPriceChangedProductDto
                    {
                        ProductCode = row.sto_kod,
                        ProductName = row.sto_isim ?? string.Empty,
                        PluNo = row.sto_plu_no,
                        AlternativeUnitName = row.sto_birim2_ad ?? string.Empty,
                        Barcode = row.Barcode ?? string.Empty,
                        IsDomestic = Convert.ToByte(string.Equals(row.sto_mensei, "TR", StringComparison.OrdinalIgnoreCase)),
                        OldPrice = oldPrice,
                        Origin = row.sto_mensei ?? string.Empty,
                        Price = price,
                        PriceChangeDate = priceChangeDate?.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR")) ?? string.Empty,
                        UnitPriceFactor = CalculateUnitPriceFactor(price, row.sto_birim2_katsayi),
                        UnitName = row.sto_birim1_ad ?? string.Empty
                    }
                };
            })
            .OrderBy(item => item.SortDate)
            .Select(item => item.Product)
            .ToArray();
    }

    internal async Task<IReadOnlyDictionary<string, LabelDocumentProductDto>> ExecuteAsync(
        int warehouseNo,
        IReadOnlyCollection<string> productCodes,
        int documentId,
        CancellationToken cancellationToken)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        if (productCodes.Count == 0)
        {
            return new Dictionary<string, LabelDocumentProductDto>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedCodes = productCodes
            .Select(NormalizeCode)
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var rows = await (
            from stock in mikroDbContext.STOKLARs.AsNoTracking()
            where normalizedCodes.Contains(stock.sto_kod)
            join warehouseDetail in mikroDbContext.STOK_DEPO_DETAYLARIs.AsNoTracking().Where(detail => detail.sdp_depo_no == warehouseNo)
                on stock.sto_kod equals warehouseDetail.sdp_depo_kod into warehouseDetailGroup
            from warehouseDetail in warehouseDetailGroup.DefaultIfEmpty()
            let barcode = mikroDbContext.BARKOD_TANIMLARIs
                .AsNoTracking()
                .Where(item => item.bar_stokkodu == stock.sto_kod)
                .OrderByDescending(item => item.bar_master ?? false)
                .ThenBy(item => item.bar_birimpntr ?? 0)
                .ThenByDescending(item => item.bar_create_date)
                .Select(item => item.bar_kodu)
                .FirstOrDefault()
            let currentPrice = mikroDbContext.STOK_SATIS_FIYAT_LISTELERIs
                .AsNoTracking()
                .Where(item =>
                    item.sfiyat_stokkod == stock.sto_kod &&
                    item.sfiyat_deposirano == warehouseNo &&
                    item.sfiyat_birim_pntr == 1)
                .OrderBy(item => item.sfiyat_listesirano ?? int.MaxValue)
                .ThenByDescending(item => item.sfiyat_lastup_date ?? item.sfiyat_create_date)
                .Select(item => new
                {
                    item.sfiyat_fiyati,
                    item.sfiyat_lastup_date,
                    item.sfiyat_create_date
                })
                .FirstOrDefault()
            let latestPriceChange = mikroDbContext.STOK_FIYAT_DEGISIKLIKLERIs
                .AsNoTracking()
                .Where(item =>
                    item.fid_stok_kod == stock.sto_kod &&
                    item.fid_depo_no == warehouseNo)
                .OrderByDescending(item => item.fid_tarih ?? item.fid_belge_tarih ?? item.fid_create_date)
                .ThenByDescending(item => item.fid_lastup_date ?? item.fid_create_date)
                .Select(item => new
                {
                    item.fid_eskifiy_tutar,
                    item.fid_yenifiy_tutar,
                    item.fid_tarih,
                    item.fid_belge_tarih,
                    item.fid_create_date
                })
                .FirstOrDefault()
            select new
            {
                stock.sto_kod,
                stock.sto_isim,
                stock.sto_paket_kodu,
                stock.sto_birim1_katsayi,
                stock.sto_birim1_ad,
                stock.sto_birim2_katsayi,
                stock.sto_birim2_ad,
                stock.sto_perakende_vergi,
                stock.sto_toptan_vergi,
                stock.sto_satis_dursun,
                stock.sto_siparis_dursun,
                stock.sto_malkabul_dursun,
                stock.sto_pasif_fl,
                stock.sto_cins,
                stock.sto_mensei,
                stock.sto_plu_no,
                stock.sto_sektor_kodu,
                stock.sto_toplam_rafomru,
                stock.sto_anagrup_kod,
                stock.sto_kategori_kodu,
                stock.sto_lastup_date,
                stock.sto_create_date,
                stock.sto_sat_cari_kod,
                Barcode = barcode,
                warehouseDetail.sdp_sat_cari_kod,
                warehouseDetail.sdp_satisdursun,
                warehouseDetail.sdp_sipdursun,
                warehouseDetail.sdp_malkabuldursun,
                warehouseDetail.sdp_Pasif_fl,
                CurrentPrice = currentPrice,
                LatestPriceChange = latestPriceChange
            }).ToListAsync(cancellationToken);

        var products = new Dictionary<string, LabelDocumentProductDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var oldPrice = row.LatestPriceChange?.fid_eskifiy_tutar ?? row.CurrentPrice?.sfiyat_fiyati ?? 0d;
            var price = row.CurrentPrice?.sfiyat_fiyati ?? row.LatestPriceChange?.fid_yenifiy_tutar ?? 0d;
            var lastUpdateDate = row.sto_lastup_date ?? row.CurrentPrice?.sfiyat_lastup_date ?? row.sto_create_date;
            var priceChangeDate = row.LatestPriceChange?.fid_tarih ?? row.LatestPriceChange?.fid_belge_tarih;
            var isPassive = row.sdp_Pasif_fl ?? row.sto_pasif_fl ?? false;

            products[row.sto_kod] = new LabelDocumentProductDto
            {
                Package = row.sto_paket_kodu ?? string.Empty,
                PackageFactor = FormatNumber(row.sto_birim2_katsayi ?? row.sto_birim1_katsayi),
                LastUpdateDate = lastUpdateDate,
                BarcodeContent = row.Barcode ?? string.Empty,
                BulkSaleTaxRate = Convert.ToByte(row.sto_toptan_vergi ?? 0),
                RetailSaleTaxRate = Convert.ToByte(row.sto_perakende_vergi ?? 0),
                ProductCode = row.sto_kod,
                ProductName = row.sto_isim ?? string.Empty,
                Barcode = row.Barcode ?? string.Empty,
                OldPrice = oldPrice,
                Price = price,
                PriceChangeDate = priceChangeDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                SupplierCode = row.sdp_sat_cari_kod ?? row.sto_sat_cari_kod ?? string.Empty,
                IsClosedToSale = Convert.ToByte(row.sdp_satisdursun ?? row.sto_satis_dursun ?? 0),
                IsClosedToOrder = Convert.ToByte(row.sdp_sipdursun ?? row.sto_siparis_dursun ?? 0),
                IsClosedToReceiving = Convert.ToByte(row.sdp_malkabuldursun ?? row.sto_malkabul_dursun ?? 0),
                IsPassive = isPassive,
                UnitName = row.sto_birim1_ad ?? string.Empty,
                UnitName2 = row.sto_birim2_ad ?? string.Empty,
                TypeCode = row.sto_cins?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                IsDomestic = Convert.ToByte(string.Equals(row.sto_mensei, "TR", StringComparison.OrdinalIgnoreCase)),
                Origin = row.sto_mensei ?? string.Empty,
                UnitPriceFactor = row.sto_birim2_katsayi ?? 1d,
                AlternativeUnitName = row.sto_birim2_ad ?? string.Empty,
                PluNo = row.sto_plu_no,
                SectorCode = row.sto_sektor_kodu ?? string.Empty,
                ShelfLife = row.sto_toplam_rafomru ?? 0,
                Type = row.sto_anagrup_kod ?? string.Empty,
                OrderGuid = null,
                CanBeCalled = !isPassive,
                Quantity = 0d,
                DeliveredQuantity = 0d,
                DocumentOrderNo = documentId,
                CategoryCode = row.sto_kategori_kodu ?? string.Empty
            };
        }

        foreach (var code in normalizedCodes)
        {
            products.TryAdd(code, CreateFallbackProduct(code, documentId));
        }

        return products;
    }

    private static LabelDocumentProductDto CreateFallbackProduct(string productCode, int documentId) =>
        new()
        {
            ProductCode = productCode,
            BarcodeContent = string.Empty,
            Barcode = string.Empty,
            ProductName = string.Empty,
            LastUpdateDate = DateTime.MinValue,
            DocumentOrderNo = documentId,
            CanBeCalled = false
        };

    private static string NormalizeCode(string value) =>
        value.Trim();

    private static string FormatNumber(double? value) =>
        value.HasValue
            ? value.Value.ToString("0.####", CultureInfo.InvariantCulture)
            : string.Empty;

    private static double CalculateUnitPriceFactor(double price, double? factor)
    {
        if (!factor.HasValue || factor.Value == 0d)
        {
            return 0d;
        }

        return factor.Value > 0d
            ? price / factor.Value
            : price * (-1 * factor.Value);
    }
}
