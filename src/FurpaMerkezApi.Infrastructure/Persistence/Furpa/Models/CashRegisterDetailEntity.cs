namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class CashRegisterDetailEntity
{
    public int Id { get; set; }

    public string CashRegisterNo { get; set; } = string.Empty;

    public string Bank { get; set; } = string.Empty;

    public string TerminalId { get; set; } = string.Empty;

    public string MerchantNo { get; set; } = string.Empty;

    public int? CashNo { get; set; }
}
