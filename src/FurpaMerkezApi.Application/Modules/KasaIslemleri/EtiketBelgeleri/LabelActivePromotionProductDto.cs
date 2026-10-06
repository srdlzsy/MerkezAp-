namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelActivePromotionProductDto
{
    public string ProductCode { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public int PluNo { get; init; }

    public string Barcode { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Barcodes { get; init; } = Array.Empty<string>();

    public double Price { get; init; }

    public string UnitName { get; init; } = string.Empty;

    public string AlternativeUnitName { get; init; } = string.Empty;

    public double UnitPriceFactor { get; init; }

    public LabelPromotionDto Promotion { get; init; } = new();
}
