using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.TrendyolGo;

public sealed class TrendyolGoPriceStockWorkbenchTests
{
    [Fact]
    public void BuildRow_UsesMikroPriceAndFlooredWarehouseQuantity()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 90m, 7),
            new("015550", "Mikro Product", 99.90m, 8.9m, false));

        Assert.Equal("Ready", row.Status);
        Assert.Equal(99.90m, row.MikroPrice);
        Assert.Equal(8, row.MikroQuantity);
        Assert.Equal("015550", row.StockCode);
    }

    [Fact]
    public void BuildRow_SkipsMissingProduct()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 10m, 3), null);

        Assert.Equal("Skipped", row.Status);
        Assert.Equal("Mikro barkod eslesmesi yok.", row.Reason);
    }

    [Fact]
    public void BuildRow_SkipsZeroPrice()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 10m, 3),
            new("015550", "Product", 0m, 4m, false));

        Assert.Equal("Skipped", row.Status);
        Assert.Equal("Mikro satis fiyati yok veya sifir.", row.Reason);
    }

    [Fact]
    public void BuildRow_TreatsNegativeStockAsZeroAndAvoidsUnchangedResend()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 99m, null),
            new("015550", "Product", 99m, -2m, false));

        Assert.Equal(0, row.MikroQuantity);
        Assert.Equal("Unchanged", row.Status);
    }
}
