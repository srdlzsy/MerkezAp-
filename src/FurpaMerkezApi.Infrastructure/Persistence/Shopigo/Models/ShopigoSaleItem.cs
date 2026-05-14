namespace FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;

public sealed class ShopigoSaleItem
{
    public int Id { get; set; }

    public string? SaleUuid { get; set; }

    public decimal? Quantity { get; set; }

    public double? TotalPrice { get; set; }

    public int Refunded { get; set; }

    public DateTime? DeletedAt { get; set; }
}
