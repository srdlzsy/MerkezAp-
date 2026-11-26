namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;

public sealed class GreenGrocerProductCaseOptions
{
    public const string SectionName = "GreenGrocerProductCases";

    public bool Enabled { get; init; } = true;

    public bool OrderLinkingEnabled { get; init; }

    public bool RequireOrderUserManualApproval { get; init; }

    public bool AuthDbProfileStorageEnabled { get; init; }

    public bool AuthDbTrackingEnabled { get; init; }

    public int SourceWarehouseNo { get; init; } = 56;

    public double DefaultOverDeliveryTolerancePercent { get; init; } = 20d;

    public double SevereOverDeliveryMultiplier { get; init; } = 1.5d;
}
