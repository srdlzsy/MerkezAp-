using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class MaydayLabelPromotionLookupTests
{
    [Theory]
    [InlineData(100, 20, 0, 80)]
    [InlineData(100, 20, 15, 85)]
    [InlineData(10, 0, 15, 0)]
    [InlineData(99.99, 12.5, 0, 87.49)]
    public void CalculatePromotionPrice_UsesAmountFirstAndNeverReturnsNegative(
        double normalPrice,
        double discountRate,
        double discountAmount,
        double expected)
    {
        var result = MaydayLabelPromotionLookup.CalculatePromotionPrice(
            normalPrice,
            discountRate,
            discountAmount);

        Assert.Equal(expected, result);
    }
}
