using FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ResolveBarcode;

public sealed class ResolveBarcodeUseCase(MikroDbContext mikroDbContext) : IResolveBarcodeUseCase
{
    public async Task<BarcodeResolutionDto> ExecuteAsync(
        BarcodeResolutionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var barcode = NormalizeOrNull(request.Barcode)
            ?? throw new ArgumentException("Barcode is required.", nameof(request.Barcode));
        var screenCode = NormalizeOrNull(request.ScreenCode);

        var barcodeMatch = await mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(row => row.bar_kodu == barcode)
            .Select(row => new BarcodeRow(
                row.bar_kodu ?? string.Empty,
                row.bar_stokkodu,
                row.bar_birimpntr,
                row.bar_master ?? false))
            .FirstOrDefaultAsync(cancellationToken);

        string? stockCode = null;
        string? resolutionSource = null;
        var matchedUnitPointer = 1;
        var matchedBarcode = barcode;

        if (barcodeMatch is not null)
        {
            stockCode = NormalizeOrNull(barcodeMatch.StockCode);
            matchedUnitPointer = Math.Max((int)barcodeMatch.UnitPointer.GetValueOrDefault(1), 1);
            resolutionSource = "barcode";
        }
        else
        {
            var stockSeed = await mikroDbContext.STOKLARs
                .AsNoTracking()
                .Where(stock => stock.sto_kod == barcode || stock.sto_kuresel_urun_numarasi == barcode)
                .Select(stock => new
                {
                    StockCode = stock.sto_kod,
                    Source = stock.sto_kod == barcode ? "stock-code" : "gtin"
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (stockSeed is null)
            {
                return new BarcodeResolutionDto(
                    false,
                    barcode,
                    request.WarehouseNo,
                    screenCode,
                    "not-found",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    false,
                    false,
                    false,
                    false,
                    false,
                    "Barkod veya stok referansi sistemde bulunamadi.",
                    null,
                    null);
            }

            stockCode = stockSeed.StockCode;
            resolutionSource = stockSeed.Source;
            matchedBarcode = resolutionSource == "stock-code" ? null : barcode;
        }

        if (string.IsNullOrWhiteSpace(stockCode))
        {
            return new BarcodeResolutionDto(
                false,
                barcode,
                request.WarehouseNo,
                screenCode,
                resolutionSource,
                null,
                null,
                matchedBarcode,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                false,
                false,
                false,
                "Barkod kaydi bulundu ancak bagli stok kodu bos geldi.",
                null,
                null);
        }

        var stock = await mikroDbContext.STOKLARs
            .AsNoTracking()
            .Where(item => item.sto_kod == stockCode)
            .Select(item => new StockSnapshot(
                item.sto_kod,
                item.sto_isim,
                item.sto_kuresel_urun_numarasi,
                item.sto_sat_cari_kod,
                item.sto_birim1_ad,
                item.sto_birim1_katsayi,
                item.sto_birim2_ad,
                item.sto_birim2_katsayi,
                item.sto_birim3_ad,
                item.sto_birim3_katsayi,
                item.sto_birim4_ad,
                item.sto_birim4_katsayi,
                item.sto_satis_dursun,
                item.sto_siparis_dursun,
                item.sto_malkabul_dursun))
            .FirstOrDefaultAsync(cancellationToken);

        if (stock is null)
        {
            return new BarcodeResolutionDto(
                false,
                barcode,
                request.WarehouseNo,
                screenCode,
                resolutionSource,
                stockCode,
                null,
                matchedBarcode,
                null,
                null,
                null,
                matchedUnitPointer,
                null,
                null,
                false,
                false,
                false,
                false,
                false,
                "Stok karti bulunamadi.",
                null,
                null);
        }

        var productBarcodes = await mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(row => row.bar_stokkodu == stockCode && row.bar_kodu != null && row.bar_kodu != string.Empty)
            .Select(row => new BarcodeRow(
                row.bar_kodu ?? string.Empty,
                row.bar_stokkodu,
                row.bar_birimpntr,
                row.bar_master ?? false))
            .ToListAsync(cancellationToken);

        var primaryBarcode = productBarcodes
            .FirstOrDefault(row => row.UnitPointer.GetValueOrDefault(1) == 1)
            ?.Barcode
            ?? productBarcodes.FirstOrDefault()?.Barcode
            ?? stock.GlobalTradeItemNo;

        var caseBarcodeRow = productBarcodes
            .OrderByDescending(row => row.IsMaster)
            .ThenByDescending(row => GetUnitMultiplier(stock, row.UnitPointer))
            .FirstOrDefault(row => row.IsMaster || GetUnitMultiplier(stock, row.UnitPointer) > 1d);
        var caseBarcode = caseBarcodeRow?.Barcode;
        var unitsPerCase = caseBarcodeRow is null ? null : GetUnitMultiplier(stock, caseBarcodeRow.UnitPointer);
        var matchedUnitName = GetUnitName(stock, matchedUnitPointer);
        var matchedUnitMultiplier = GetUnitMultiplier(stock, matchedUnitPointer);

        var defaultSupplierCode = NormalizeOrNull(stock.DefaultSupplierCode);
        var defaultSupplierName = defaultSupplierCode is null
            ? null
            : await mikroDbContext.CARI_HESAPLARs
                .AsNoTracking()
                .Where(customer => customer.cari_kod == defaultSupplierCode)
                .Select(customer => customer.cari_unvan1)
                .FirstOrDefaultAsync(cancellationToken);

        var isSalesBlocked = IsBlocked(stock.SalesBlockCode);
        var isOrderBlocked = IsBlocked(stock.OrderBlockCode);
        var isGoodsAcceptanceBlocked = IsBlocked(stock.GoodsAcceptanceBlockCode);
        var (isUsableInScreen, usabilityReason) = EvaluateScreenUsability(
            screenCode,
            isSalesBlocked,
            isOrderBlocked,
            isGoodsAcceptanceBlocked);

        return new BarcodeResolutionDto(
            true,
            barcode,
            request.WarehouseNo,
            screenCode,
            resolutionSource,
            stock.StockCode,
            stock.StockName,
            matchedBarcode,
            primaryBarcode,
            caseBarcode,
            unitsPerCase,
            matchedUnitPointer,
            matchedUnitName,
            matchedUnitMultiplier,
            isSalesBlocked || isOrderBlocked || isGoodsAcceptanceBlocked,
            isSalesBlocked,
            isOrderBlocked,
            isGoodsAcceptanceBlocked,
            isUsableInScreen,
            usabilityReason,
            defaultSupplierCode,
            NormalizeOrNull(defaultSupplierName));
    }

    private static (bool IsUsable, string Reason) EvaluateScreenUsability(
        string? screenCode,
        bool isSalesBlocked,
        bool isOrderBlocked,
        bool isGoodsAcceptanceBlocked)
    {
        var normalizedScreenCode = NormalizeScreenCode(screenCode);

        if (normalizedScreenCode is null)
        {
            return (true, "Ekran baglami verilmedigi icin sadece blok bilgisi donduruldu.");
        }

        return normalizedScreenCode switch
        {
            "depo-mal-kabulleri" or "firma-mal-kabulleri" or "mal-kabulleri" => isGoodsAcceptanceBlocked
                ? (false, "Urun mal kabul icin bloklu.")
                : (true, "Urun mal kabul ekraninda kullanilabilir."),

            "sayim-sonuclari" => (true, "Urun sayim ekraninda kullanilabilir."),

            "verilen-depo-siparisleri" or "verilen-firma-siparisleri" => isOrderBlocked
                ? (false, "Urun siparis icin bloklu.")
                : (true, "Urun siparis ekraninda kullanilabilir."),

            "giden-firma-sevkleri" or "giden-depolar-arasi-sevkler" or "firma-iadeleri" or "giden-depo-iadeleri" => isSalesBlocked
                ? (false, "Urun sevk veya iade icin bloklu.")
                : (true, "Urun sevk veya iade ekraninda kullanilabilir."),

            _ => (true, "Bu ekran icin ozel kullanilabilirlik kurali tanimli degil.")
        };
    }

    private static string? NormalizeScreenCode(string? value)
    {
        var normalized = NormalizeOrNull(value)?.ToLowerInvariant();
        return normalized switch
        {
            "depo-mal-kabul" => "depo-mal-kabulleri",
            "firma-mal-kabul" => "firma-mal-kabulleri",
            "mal-kabul" => "mal-kabulleri",
            _ => normalized
        };
    }

    private static bool IsBlocked(byte? value) =>
        value.GetValueOrDefault() != 0;

    private static string? GetUnitName(StockSnapshot stock, int? unitPointer) =>
        Math.Max(unitPointer.GetValueOrDefault(1), 1) switch
        {
            1 => NormalizeOrNull(stock.Unit1Name),
            2 => NormalizeOrNull(stock.Unit2Name),
            3 => NormalizeOrNull(stock.Unit3Name),
            4 => NormalizeOrNull(stock.Unit4Name),
            _ => NormalizeOrNull(stock.Unit1Name)
        };

    private static double? GetUnitMultiplier(StockSnapshot stock, int? unitPointer) =>
        Math.Max(unitPointer.GetValueOrDefault(1), 1) switch
        {
            1 => NormalizeMultiplier(stock.Unit1Multiplier),
            2 => NormalizeMultiplier(stock.Unit2Multiplier),
            3 => NormalizeMultiplier(stock.Unit3Multiplier),
            4 => NormalizeMultiplier(stock.Unit4Multiplier),
            _ => NormalizeMultiplier(stock.Unit1Multiplier)
        };

    private static double? NormalizeMultiplier(double? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value <= 0d ? 1d : value.Value;
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record BarcodeRow(
        string Barcode,
        string? StockCode,
        byte? UnitPointer,
        bool IsMaster);

    private sealed record StockSnapshot(
        string StockCode,
        string? StockName,
        string? GlobalTradeItemNo,
        string? DefaultSupplierCode,
        string? Unit1Name,
        double? Unit1Multiplier,
        string? Unit2Name,
        double? Unit2Multiplier,
        string? Unit3Name,
        double? Unit3Multiplier,
        string? Unit4Name,
        double? Unit4Multiplier,
        byte? SalesBlockCode,
        byte? OrderBlockCode,
        byte? GoodsAcceptanceBlockCode);
}
