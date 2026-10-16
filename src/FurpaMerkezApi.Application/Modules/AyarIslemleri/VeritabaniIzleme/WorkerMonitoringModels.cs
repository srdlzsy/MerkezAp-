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
    string StatusName,
    string Activity,
    DateTime RegisteredAtUtc,
    DateTime? LastHeartbeatAtUtc,
    long? HeartbeatAgeSeconds,
    DateTime? LastSuccessAtUtc,
    DateTime? LastFailureAtUtc,
    string? LastError,
    bool HasUnresolvedFailure,
    bool RequiresAttention,
    string Recommendation,
    int QueuedCount,
    int RunningCount,
    DateTime? OldestQueuedAtUtc,
    long? OldestQueuedAgeSeconds,
    int StaleAfterSeconds,
    WorkerJobMonitoringDto? CurrentJob = null,
    WorkerJobMonitoringDto? LastJob = null,
    int UnresolvedFailureCount = 0,
    WorkerJobMonitoringDto? LastFailedJob = null);

public sealed record WorkerJobMonitoringDto(
    Guid JobId,
    string TaskCode,
    string TaskName,
    string Status,
    string ExecutionMode,
    string TriggerSource,
    int? WarehouseNo,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    long? DurationSeconds,
    int AffectedRecordCount,
    string? Message,
    string? ErrorMessage);
