using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

internal static class LabelPromotionPriceCalculator
{
    internal static LabelPromotionDto ApplyNormalPrice(LabelPromotionDto promotion, double normalPrice)
    {
        var effectiveUnitPrice = CalculateEffectiveUnitPrice(promotion, normalPrice);

        return promotion with
        {
            NormalPrice = normalPrice,
            PromotionPrice = effectiveUnitPrice ?? normalPrice,
            EffectiveUnitPrice = effectiveUnitPrice
        };
    }

    internal static double? CalculateEffectiveUnitPrice(LabelPromotionDto promotion, double normalPrice)
    {
        if (normalPrice < 0d ||
            promotion.RequiredQuantity <= 0d ||
            promotion.DiscountedQuantity <= 0d ||
            !string.Equals(
                promotion.RequiredProductCode,
                promotion.DiscountedProductCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var discountedQuantity = Math.Min(
            promotion.RequiredQuantity,
            promotion.DiscountedQuantity);
        var normalTotal = normalPrice * promotion.RequiredQuantity;
        double discountedTotal;

        if (string.Equals(promotion.DiscountType, "PERCENTAGE", StringComparison.OrdinalIgnoreCase))
        {
            var discountRate = Math.Clamp(promotion.DiscountValue, 0d, 100d);
            discountedTotal = normalTotal - (normalPrice * discountedQuantity * discountRate / 100d);
        }
        else if (string.Equals(promotion.DiscountType, "FIXED", StringComparison.OrdinalIgnoreCase))
        {
            var fixedUnitPrice = Math.Max(0d, promotion.DiscountValue);
            discountedTotal = normalPrice * (promotion.RequiredQuantity - discountedQuantity) +
                              fixedUnitPrice * discountedQuantity;
        }
        else
        {
            return null;
        }

        return Math.Round(
            Math.Max(0d, discountedTotal / promotion.RequiredQuantity),
            2,
            MidpointRounding.AwayFromZero);
    }
}
