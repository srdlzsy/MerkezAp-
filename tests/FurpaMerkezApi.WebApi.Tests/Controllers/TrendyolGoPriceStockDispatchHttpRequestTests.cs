using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.TrendyolGo;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Controllers;

public sealed class TrendyolGoPriceStockDispatchHttpRequestTests
{
    [Theory]
    [InlineData("selected", TrendyolGoPriceStockDispatchMode.Selected)]
    [InlineData("changed", TrendyolGoPriceStockDispatchMode.Changed)]
    [InlineData("all-existing", TrendyolGoPriceStockDispatchMode.AllExisting)]
    [InlineData("ALL-EXISTING", TrendyolGoPriceStockDispatchMode.AllExisting)]
    public void ResolveMode_UsesExplicitMode(
        string mode,
        TrendyolGoPriceStockDispatchMode expected)
    {
        var request = new TrendyolGoPriceStockDispatchHttpRequest { Mode = mode };

        Assert.Equal(expected, request.ResolveMode());
    }

    [Fact]
    public void ResolveMode_PreservesLegacySendAllBehavior()
    {
        var request = new TrendyolGoPriceStockDispatchHttpRequest { SendAll = true };

        Assert.Equal(TrendyolGoPriceStockDispatchMode.Changed, request.ResolveMode());
    }

    [Fact]
    public void ResolveMode_DefaultsToSelectedForLegacyBarcodeRequests()
    {
        var request = new TrendyolGoPriceStockDispatchHttpRequest();

        Assert.Equal(TrendyolGoPriceStockDispatchMode.Selected, request.ResolveMode());
    }
}
