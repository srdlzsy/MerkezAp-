namespace FurpaMerkezApi.Application.Modules.StokIslemleri.EtiketBelgeleri;

public sealed record LabelTagDto
{
    public int BranchNo { get; init; }

    public string BranchName { get; init; } = string.Empty;

    public string ProductionCity { get; init; } = string.Empty;

    public string ProductionDistrict { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string GoodsType { get; init; } = string.Empty;

    public string GoodsGenus { get; init; } = string.Empty;

    public double Quantity { get; init; }

    public string TakenTag { get; init; } = string.Empty;

    public string Buyer { get; init; } = string.Empty;

    public DateTime ProductionDate { get; init; }

    public double BuyingPrice { get; init; }

    public DateTime ShippingDate { get; init; }

    public string Manufacturer { get; init; } = string.Empty;
}
