namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class CashierEntity
{
    public int CashierId { get; set; }

    public int CreateUser { get; set; }

    public DateTime CreateDate { get; set; }

    public int UpdateUser { get; set; }

    public DateTime UpdateDate { get; set; }

    public int CashierCode { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public string CashierPassword { get; set; } = string.Empty;

    public string CashierAuthorization { get; set; } = string.Empty;

    public bool CashierState { get; set; }
}
