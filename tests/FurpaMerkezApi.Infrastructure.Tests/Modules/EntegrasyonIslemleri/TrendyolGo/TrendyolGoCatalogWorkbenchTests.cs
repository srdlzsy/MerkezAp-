using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.TrendyolGo;

public sealed class TrendyolGoCatalogWorkbenchTests
{
    [Fact]
    public void BuildRow_DoesNotRecreateProductWhenStockCodeAlreadyExistsWithDifferentBarcode()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "007921", "OMO MATIK", "8690793020075", "BRAND", "CATEGORY", 20, 199.50m, false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["BRAND"] = 1 },
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["CATEGORY"] = 2 },
            existingStockCodes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["007921"] = "2700581"
            });

        Assert.Equal("ExistingStockCode", row.Status);
        Assert.Contains("2700581", row.Reason, StringComparison.Ordinal);
        Assert.Contains("yeni urun olusturulmadi", row.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InferMappings_UsesUnambiguousSiblingProductMapping()
    {
        var trendyolProducts = new[]
        {
            new TrendyolGoPriceStockWorkbench.TrendyolProduct("sibling-1", "Erikli Su", 20m, 10, 35246, 1420),
            new TrendyolGoPriceStockWorkbench.TrendyolProduct("sibling-2", "Erikli Su", 25m, 10, 35246, 1420)
        };
        var mikroProducts = new Dictionary<string, TrendyolGoCatalogWorkbench.MikroCatalogProduct>
        {
            ["sibling-1"] = new("001", "ERIKLI SU", "sibling-1", "0327", "01.03.02.01", 1, 20m, false),
            ["sibling-2"] = new("002", "ERIKLI SU", "sibling-2", "0327", "01.03.02.01", 1, 25m, false)
        };

        var mappings = TrendyolGoCatalogWorkbench.InferMappings(
            trendyolProducts,
            mikroProducts,
            product => product.BrandCode,
            product => product.BrandId,
            new Dictionary<string, long>());

        Assert.Equal(35246, mappings["0327"]);
    }

    [Fact]
    public void InferMappings_DoesNotGuessWhenSiblingMappingsConflict()
    {
        var trendyolProducts = new[]
        {
            new TrendyolGoPriceStockWorkbench.TrendyolProduct("sibling-1", "Product 1", 20m, 10, 35246, 1420),
            new TrendyolGoPriceStockWorkbench.TrendyolProduct("sibling-2", "Product 2", 25m, 10, 99999, 1420)
        };
        var mikroProducts = new Dictionary<string, TrendyolGoCatalogWorkbench.MikroCatalogProduct>
        {
            ["sibling-1"] = new("001", "PRODUCT 1", "sibling-1", "0327", "01.03.02.01", 1, 20m, false),
            ["sibling-2"] = new("002", "PRODUCT 2", "sibling-2", "0327", "01.03.02.01", 1, 25m, false)
        };

        var mappings = TrendyolGoCatalogWorkbench.InferMappings(
            trendyolProducts,
            mikroProducts,
            product => product.BrandCode,
            product => product.BrandId,
            new Dictionary<string, long>());

        Assert.DoesNotContain("0327", mappings);
    }

    [Fact]
    public void BuildRow_WhenBarcodeNotInExisting_AndMappingsPresent_ReturnsReady()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "000322",
            "ERİKLİ SU 0,75 LT CAM",
            "8690793020075",
            "0327",
            "01.03.02.01",
            1,
            115.00m,
            false);

        var existingBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var duplicateBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var brandMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["0327"] = 35246 };
        var categoryMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["01.03.02.01"] = 1420 };

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product,
            existingBarcodes,
            duplicateBarcodes,
            brandMappings,
            categoryMappings);

        Assert.Equal("Ready", row.Status);
        Assert.Null(row.Reason);
        Assert.Equal(35246, row.TrendyolBrandId);
        Assert.Equal(1420, row.TrendyolCategoryId);
        Assert.Equal("8690793020075", row.Barcode);
    }

    [Fact]
    public void BuildRow_WhenMappingsMissing_ReturnsMappingRequired()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "000322",
            "ERİKLİ SU 0,75 LT CAM",
            "8690793020075",
            "0327",
            "01.03.02.01",
            1,
            115.00m,
            false);

        var existingBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var duplicateBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var brandMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var categoryMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product,
            existingBarcodes,
            duplicateBarcodes,
            brandMappings,
            categoryMappings);

        Assert.Equal("MappingRequired", row.Status);
        Assert.Equal("Trendyol marka ve kategori eslemesi gerekli.", row.Reason);
    }

    [Fact]
    public void BuildRow_WhenBarcodeAlreadyExists_DoesNotCreateItAgain()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "000322",
            "ERIKLI SU 0,75 LT CAM",
            "8690793020075",
            "0327",
            "01.03.02.01",
            1,
            115.00m,
            false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product,
            new HashSet<string>(StringComparer.Ordinal) { "8690793020075" },
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Existing", row.Status);
        Assert.Null(row.Reason);
    }

    [Fact]
    public void BuildRow_WhenUnsafeDefaultsAreConfigured_StillRequiresExplicitMappings()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "000322",
            "ERİKLİ SU 0,75 LT CAM",
            "8690793020075",
            "0327",
            "01.03.02.01",
            1,
            115.00m,
            false);

        var existingBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var duplicateBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var brandMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var categoryMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product,
            existingBarcodes,
            duplicateBarcodes,
            brandMappings,
            categoryMappings,
            defaultBrandId: 1334359,
            defaultCategoryId: 1420);

        Assert.Equal("MappingRequired", row.Status);
        Assert.Null(row.TrendyolBrandId);
        Assert.Null(row.TrendyolCategoryId);
    }

    [Fact]
    public void BuildRow_WhenProductBlocked_ReturnsSkippedWithReason()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "000322",
            "ERİKLİ SU 0,75 LT CAM",
            "8690793020075",
            "0327",
            "01.03.02.01",
            1,
            115.00m,
            true);

        var existingBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var duplicateBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var brandMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var categoryMappings = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product,
            existingBarcodes,
            duplicateBarcodes,
            brandMappings,
            categoryMappings);

        Assert.Equal("Skipped", row.Status);
        Assert.Equal("Urun Mikro'da pasif veya satisa kapali.", row.Reason);
    }

    [Theory]
    [InlineData("2700123", "FURPA2700123")]
    [InlineData(" 2900139 ", "FURPA2900139")]
    [InlineData("FURPA2900139", "FURPA2900139")]
    [InlineData("2968000009189", "2968000009189")]
    [InlineData("8690793020075", "8690793020075")]
    public void BarcodeRule_AppliesUppercaseFurpaPrefixOnlyForShortScaleBarcodesStartingWith27Or29(
        string rawBarcode,
        string expected)
    {
        var formatted = TrendyolGoBarcodeRule.FormatBarcode(rawBarcode);

        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void BarcodeRule_ExpandLookupBarcodes_IncludesStrippedRawBarcodeForFurpaPrefix()
    {
        var expanded = TrendyolGoBarcodeRule.ExpandLookupBarcodes(["FURPA2700123", "8690793020075"]);

        Assert.Contains("FURPA2700123", expanded);
        Assert.Contains("2700123", expanded);
        Assert.Contains("8690793020075", expanded);
    }

    [Fact]
    public void BarcodeRule_SqlRankedBarcodesCte_PrioritizesPricedMasterAndUnitOneBarcodes()
    {
        var cte = TrendyolGoBarcodeRule.SqlRankedBarcodesCte();

        var pricedIdx = cte.IndexOf("STOK_SATIS_FIYAT_LISTELERI", StringComparison.Ordinal);
        var masterIdx = cte.IndexOf("barcode.bar_master", StringComparison.Ordinal);
        var unitOneIdx = cte.IndexOf("barcode.bar_birimpntr", masterIdx, StringComparison.Ordinal);
        var dateIdx = cte.IndexOf("bar_lastup_date", StringComparison.Ordinal);

        Assert.True(pricedIdx > 0);
        Assert.True(masterIdx > pricedIdx);
        Assert.True(unitOneIdx > masterIdx);
        Assert.True(dateIdx > unitOneIdx);
    }
}
