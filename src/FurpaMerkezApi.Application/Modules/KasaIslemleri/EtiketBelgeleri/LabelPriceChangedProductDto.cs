namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelPriceChangedProductDto
{
    public string ProductCode { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public int PluNo { get; init; }

    public string AlternativeUnitName { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public byte IsDomestic { get; init; }

    public double OldPrice { get; init; }

    public string Origin { get; init; } = string.Empty;

    public double Price { get; init; }

    public string PriceChangeDate { get; init; } = string.Empty;

    public double UnitPriceFactor { get; init; }

    public string UnitName { get; init; } = string.Empty;
}
