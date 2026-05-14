namespace FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;

public sealed class ShopigoPaymentMethod
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int PavoType { get; set; }

    public int PavoMediator { get; set; }

    public int Status { get; set; }
}
