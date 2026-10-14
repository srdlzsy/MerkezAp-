namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;

public interface IWorkerMonitoringService
{
    WorkerMonitoringSnapshotDto GetSnapshot();
}

public sealed record WorkerMonitoringSnapshotDto(
    DateTime GeneratedAtUtc,
    string OverallStatus,
    IReadOnlyCollection<WorkerMonitoringItemDto> Workers);

public sealed record WorkerMonitoringItemDto(
    string Code,
    string Name,
    bool IsCritical,
    bool IsEnabled,
    string Status,
    string Activity,
    DateTime RegisteredAtUtc,
    DateTime? LastHeartbeatAtUtc,
    DateTime? LastSuccessAtUtc,
    DateTime? LastFailureAtUtc,
    string? LastError,
    int QueuedCount,
    int RunningCount,
    DateTime? OldestQueuedAtUtc,
    long? OldestQueuedAgeSeconds,
    int StaleAfterSeconds);
