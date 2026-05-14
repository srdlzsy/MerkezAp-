namespace FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;

public sealed class ShopigoPayment
{
    public int Id { get; set; }

    public string? SaleUuid { get; set; }

    public string? PaymentMethod { get; set; }

    public double? Amount { get; set; }

    public int Refunded { get; set; }

    public DateTime? DeletedAt { get; set; }
}
