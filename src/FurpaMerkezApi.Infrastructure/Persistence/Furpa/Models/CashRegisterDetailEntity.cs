namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class CashRegisterDetailEntity
{
    public int Id { get; set; }

    public string? CashRegisterNo { get; set; }

    public string? Bank { get; set; }

    public string? TerminalId { get; set; }

    public string? MerchantNo { get; set; }

    public int? CashNo { get; set; }
}
