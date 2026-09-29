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

    // Trendyol Go prices are maintained separately from the standard retail price list.
    public int PriceListNo { get; init; } = 3;

    public int PaymentPlanNo { get; init; }

    // A short-lived preview avoids a second full catalog read when the user immediately sends it.
    public int PreviewCacheSeconds { get; init; } = 120;

    // Limits all-catalog preview fan-out so a store scan does not overload Trendyol Go.
    public int PreviewPageParallelism { get; init; } = 3;

    // Internal TGO read-page size. This is not exposed to the UI.
    public int PreviewFetchPageSize { get; init; } = 100;

    public TrendyolGoBranchPosPriceSyncOptions BranchPosPriceSync { get; init; } = new();

    public TrendyolGoStoreMappingOptions[] Stores { get; init; } = [];
}

public sealed class TrendyolGoBranchPosPriceSyncOptions
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    public Dictionary<string, string> WarehouseHosts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string Database { get; init; } = "market";
    public string Username { get; init; } = "market";
    public string Password { get; init; } = string.Empty;
    public int Port { get; init; } = 5432;
    public int ConnectionTimeoutSeconds { get; init; } = 10;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int WorkerIntervalSeconds { get; init; } = 30;
    public int RetryDelaySeconds { get; init; } = 300;
}

public sealed class TrendyolGoStoreMappingOptions
{
    public long StoreId { get; init; }

    public int WarehouseNo { get; init; }

    public string StoreName { get; init; } = string.Empty;
}
