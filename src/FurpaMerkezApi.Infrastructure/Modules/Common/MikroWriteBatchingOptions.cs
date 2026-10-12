namespace FurpaMerkezApi.Infrastructure.Modules.Common;

public sealed class MikroWriteBatchingOptions
{
    public const string SectionName = "MikroWriteBatching";

    public bool InterWarehouseShipmentEnabled { get; init; }

    public int BulkCopyTimeoutSeconds { get; init; } = 300;
}
