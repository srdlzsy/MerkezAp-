namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelPromotionDto
{
    public bool IsActive { get; init; }

    public string PromotionCode { get; init; } = string.Empty;

    public string PromotionType { get; init; } = string.Empty;

    public string PromotionName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public double NormalPrice { get; init; }

    public double PromotionPrice { get; init; }

    public double DiscountRate { get; init; }

    public double DiscountAmount { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? ExpirationDate { get; init; }
}
