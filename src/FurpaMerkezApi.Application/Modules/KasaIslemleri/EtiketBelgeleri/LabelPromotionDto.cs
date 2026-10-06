namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelPromotionDto
{
    public string Source { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public string PromotionCode { get; init; } = string.Empty;

    public string PromotionType { get; init; } = string.Empty;

    public string PromotionName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string CampaignText { get; init; } = string.Empty;

    public string ProductRole { get; init; } = string.Empty;

    public string RequiredProductCode { get; init; } = string.Empty;

    public double RequiredQuantity { get; init; }

    public string DiscountedProductCode { get; init; } = string.Empty;

    public double DiscountedQuantity { get; init; }

    public string DiscountType { get; init; } = string.Empty;

    public double DiscountValue { get; init; }

    public double NormalPrice { get; init; }

    public double PromotionPrice { get; init; }

    public double? EffectiveUnitPrice { get; init; }

    public double DiscountRate { get; init; }

    public double DiscountAmount { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? ExpirationDate { get; init; }
}
