namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class CashRegistryDetailEntity
{
    public int DetailId { get; set; }

    public int BranchNo { get; set; }

    public int CashRegisterNo { get; set; }

    public byte CashRegisterType { get; set; }
}
