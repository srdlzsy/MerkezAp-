using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class LabelPromotionPriceCalculatorTests
{
    [Fact]
    public void SameProductBuyOneGetOneFree_CalculatesHalfEffectiveUnitPrice()
    {
        var promotion = new LabelPromotionDto
        {
            RequiredProductCode = "016222",
            RequiredQuantity = 2,
            DiscountedProductCode = "016222",
            DiscountedQuantity = 1,
            DiscountType = "PERCENTAGE",
            DiscountValue = 100
        };

        var result = LabelPromotionPriceCalculator.ApplyNormalPrice(promotion, 80d);

        Assert.Equal(80d, result.NormalPrice);
        Assert.Equal(40d, result.PromotionPrice);
        Assert.Equal(40d, result.EffectiveUnitPrice);
    }

    [Fact]
    public void DifferentRewardProduct_DoesNotInventEffectiveUnitPrice()
    {
        var promotion = new LabelPromotionDto
        {
            RequiredProductCode = "A",
            RequiredQuantity = 1,
            DiscountedProductCode = "B",
            DiscountedQuantity = 1,
            DiscountType = "PERCENTAGE",
            DiscountValue = 100
        };

        var result = LabelPromotionPriceCalculator.ApplyNormalPrice(promotion, 80d);

        Assert.Equal(80d, result.PromotionPrice);
        Assert.Null(result.EffectiveUnitPrice);
    }
}
