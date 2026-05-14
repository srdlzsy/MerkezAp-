namespace FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;

public sealed class ShopigoReceivedSale
{
    public int Id { get; set; }

    public string? Uuid { get; set; }

    public string? Status { get; set; }

    public string? InitiatedBy { get; set; }

    public string? ReceiptNumber { get; set; }

    public double? TotalPrice { get; set; }

    public double? RemainingAmount { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public string? MarketId { get; set; }

    public string? Subeno { get; set; }

    public string? Kasano { get; set; }

    public DateTime? DeletedAt { get; set; }
}
