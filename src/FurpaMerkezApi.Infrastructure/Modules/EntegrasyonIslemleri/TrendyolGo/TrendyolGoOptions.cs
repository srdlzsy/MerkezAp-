namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

public sealed class TrendyolGoOptions
{
    public const string SectionName = "TrendyolGo";

    public bool Enabled { get; init; }

    public string Environment { get; init; } = "Production";

    public string BaseUrl { get; init; } = "https://api.tgoapis.com";

    public long SupplierId { get; init; }

    public string IntegrationReferenceCode { get; init; } = string.Empty;

    public string ApiKey { get; init; } = string.Empty;

    public string ApiSecret { get; init; } = string.Empty;

    public string AuthorizationToken { get; init; } = string.Empty;

    public string AgentName { get; init; } = "FurpaMerkezApi";

    public string ExecutorUser { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;

    public TrendyolGoStoreMappingOptions[] Stores { get; init; } = [];
}

public sealed class TrendyolGoStoreMappingOptions
{
    public long StoreId { get; init; }

    public int WarehouseNo { get; init; }

    public string StoreName { get; init; } = string.Empty;
}
