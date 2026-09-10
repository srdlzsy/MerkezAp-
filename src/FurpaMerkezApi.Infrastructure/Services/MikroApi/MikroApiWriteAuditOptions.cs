namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroApiWriteAuditOptions
{
    public const string SectionName = "MikroApiWriteAudit";

    public bool Enabled { get; init; } = true;

    public int MaxResponseLength { get; init; } = 8000;

    public int PersistenceTimeoutSeconds { get; init; } = 10;

    public bool ReconciliationEnabled { get; init; } = true;

    public int ReconciliationIntervalSeconds { get; init; } = 300;

    public int StalePendingAfterSeconds { get; init; } = 900;

    public int ReconciliationBatchSize { get; init; } = 100;
}
