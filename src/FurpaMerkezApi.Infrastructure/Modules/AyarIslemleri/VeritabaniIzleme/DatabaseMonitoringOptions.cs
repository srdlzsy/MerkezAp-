namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class DatabaseMonitoringOptions
{
    public const string SectionName = "DatabaseMonitoring";

    public bool Enabled { get; set; }
    public int CollectionIntervalSeconds { get; set; } = 30;
    public int UiRefreshSeconds { get; set; } = 10;
    public int BlockingThresholdSeconds { get; set; } = 15;
    public int LongRunningThresholdSeconds { get; set; } = 30;
    public int OpenTransactionThresholdSeconds { get; set; } = 60;
    public int RetentionHours { get; set; } = 72;
    public int MaxIncidentCount { get; set; } = 5000;
    public int SqlTextMaxLength { get; set; } = 2000;
    public int CommandTimeoutSeconds { get; set; } = 5;
}
