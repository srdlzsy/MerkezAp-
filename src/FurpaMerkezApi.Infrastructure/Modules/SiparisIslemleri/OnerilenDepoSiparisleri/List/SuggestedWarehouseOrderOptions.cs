namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;

public sealed class SuggestedWarehouseOrderOptions
{
    public const string SectionName = "SuggestedWarehouseOrders";

    public SuggestedWarehouseOpenIncomingOrderDeductionOptions OpenIncomingOrderDeduction { get; init; } = new();
}

public sealed class SuggestedWarehouseOpenIncomingOrderDeductionOptions
{
    public bool Enabled { get; init; } = true;

    public int[] TrustedSourceWarehouseNos { get; init; } = [50];
}
