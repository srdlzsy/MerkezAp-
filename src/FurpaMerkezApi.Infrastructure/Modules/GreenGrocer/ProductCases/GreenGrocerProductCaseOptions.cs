namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;

public sealed class GreenGrocerProductCaseOptions
{
    public const string SectionName = "GreenGrocerProductCases";

    public bool Enabled { get; init; } = true;

    public bool OrderLinkingEnabled { get; init; }
}
