namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class PaymentTypeDefinitionEntity
{
    public int Id { get; set; }

    public int? CreateUser { get; set; }

    public DateTime? CreateDate { get; set; }

    public int? UpdateUser { get; set; }

    public DateTime? UpdateDate { get; set; }

    public int PaymentTypeNo { get; set; }

    public string? PaymentName { get; set; }

    public int PaymentGenus { get; set; }

    public string? AccountCode { get; set; }
}
