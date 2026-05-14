namespace FurpaMerkezApi.Infrastructure.Persistence.Shopigo.Models;

public sealed class ShopigoEmployee
{
    public int Id { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public DateTime? DeletedAt { get; set; }
}
