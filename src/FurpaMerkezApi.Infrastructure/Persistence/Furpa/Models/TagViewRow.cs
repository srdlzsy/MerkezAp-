namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class TagViewRow
{
    public int BranchNo { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string ProductionCity { get; set; } = string.Empty;

    public string ProductionDistrict { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string GoodsType { get; set; } = string.Empty;

    public string GoodsGenus { get; set; } = string.Empty;

    public double Quantity { get; set; }

    public string TakenTag { get; set; } = string.Empty;

    public string Buyer { get; set; } = string.Empty;

    public DateTime ProductionDate { get; set; }

    public double BuyingPrice { get; set; }

    public DateTime ShippingDate { get; set; }

    public string Manufacturer { get; set; } = string.Empty;
}
