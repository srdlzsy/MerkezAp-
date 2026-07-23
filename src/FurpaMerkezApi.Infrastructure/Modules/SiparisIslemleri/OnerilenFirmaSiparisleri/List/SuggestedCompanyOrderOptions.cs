namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;

public sealed class SuggestedCompanyOrderOptions
{
    public const string SectionName = "SuggestedCompanyOrders";

    public SuggestedCompanyOpenIssuedOrderDeductionOptions OpenIssuedOrderDeduction { get; init; } = new();
}

public sealed class SuggestedCompanyOpenIssuedOrderDeductionOptions
{
    public bool Enabled { get; init; }

    public string[] TrustedSupplierCodes { get; init; } = [];
}
