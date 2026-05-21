namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationOptions
{
    public bool Enabled { get; init; } = true;

    public bool WorkerEnabled { get; init; } = true;

    public bool SchedulerEnabled { get; init; }

    public string MainEndpointUrl { get; init; } = string.Empty;

    public string ExtendedEndpointUrl { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public int DefaultLookbackDays { get; init; } = 7;

    public int PreviewDefaultTake { get; init; } = 10;

    public int EndpointProbeTimeoutSeconds { get; init; } = 5;

    public string OutboxBasePath { get; init; } = string.Empty;

    public AxataWarehouseOrderAutomationOptions WarehouseOrderAutomation { get; init; } = new();

    public Dictionary<string, AxataSynchronizationTaskOptions> Tasks { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AxataWarehouseOrderAutomationOptions
{
    public bool Enabled { get; init; }

    public int[] WarehouseNos { get; init; } = Array.Empty<int>();

    public bool CreateForInterWarehouseShipments { get; init; } = true;

    public bool CreateForWarehouseReturns { get; init; } = true;
}

public sealed class AxataSynchronizationTaskOptions
{
    public bool Enabled { get; init; } = true;

    public bool ScheduleEnabled { get; init; }

    public int IntervalMinutes { get; init; } = 5;

    public int? DefaultWarehouseNo { get; init; }

    public string? LiveOperationName { get; init; }
}
