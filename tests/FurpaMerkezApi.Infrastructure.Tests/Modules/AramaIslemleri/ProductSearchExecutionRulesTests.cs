using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchProducts;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AramaIslemleri;

public sealed class ProductSearchExecutionRulesTests
{
    [Fact]
    public void BuildProductSearchSql_AddsOnlyRequestedFilters()
    {
        var sql = SearchProductsUseCase.BuildProductSearchSql(
            barcode: "8690000000000",
            stockCode: null,
            stockName: "sut",
            supplierCode: "32000001");

        Assert.Contains("barcode.bar_kodu = @barcode", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("price.sfiyat_stokkod = @stockCode", sql, StringComparison.Ordinal);
        Assert.Contains("stock.sto_isim LIKE N'%' + @stockName + N'%'", sql, StringComparison.Ordinal);
        Assert.Contains("terms.sas_cari_kod = @supplierCode", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("__StokveFiyatArama_Gokhan", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProductSearchSql_UsesExactStockCodePredicate()
    {
        var sql = SearchProductsUseCase.BuildProductSearchSql(
            barcode: null,
            stockCode: "000001",
            stockName: null,
            supplierCode: null);

        Assert.Contains("price.sfiyat_stokkod = @stockCode", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("barcode.bar_kodu = @barcode", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@supplierCode", sql, StringComparison.Ordinal);
    }
}
