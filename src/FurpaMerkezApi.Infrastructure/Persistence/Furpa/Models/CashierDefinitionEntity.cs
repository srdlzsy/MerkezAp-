namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class CashierDefinitionEntity
{
    public int CashierId { get; set; }

    public string? CreatedByUser { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? UpdatedByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CashierCode { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Password { get; set; }

    public string? Authorization { get; set; }

    public bool? IsActive { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? PasswordKey { get; set; }
}
