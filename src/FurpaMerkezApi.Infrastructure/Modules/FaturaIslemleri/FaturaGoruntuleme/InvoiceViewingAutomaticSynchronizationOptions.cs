namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class InvoiceViewingAutomaticSynchronizationOptions
{
    public const string SectionName = "FaturaGoruntuleme:AutomaticSynchronization";

    public bool Enabled { get; init; }

    public string StartTime { get; init; } = "08:30";

    public string EndTime { get; init; } = "17:30";

    public int IntervalMinutes { get; init; } = 120;

    public bool RunAtEndTime { get; init; } = true;

    public bool IncludeStatuses { get; init; }

    public int TriggerWindowMinutes { get; init; } = 5;

    public int PollIntervalSeconds { get; init; } = 60;
}
