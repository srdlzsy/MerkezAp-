namespace FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;

public sealed class ShopigoBranch
{
    public int Id { get; set; }

    public string? MarketId { get; set; }

    public string? Name { get; set; }

    public int DepoId { get; set; }

    public DateTime? DeletedAt { get; set; }
}
