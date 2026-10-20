namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;

public interface IDatabaseMonitoringService
{
    Task<MonitoringOverviewDto> GetOverviewAsync(
        WorkerMonitoringSnapshotDto workers,
        CancellationToken cancellationToken);

    Task<DatabaseMonitoringSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DatabaseMonitoringIncidentDto>> GetIncidentsAsync(
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SafeWriteMonitoringItemDto>> GetSafeWritesAsync(
        SafeWriteMonitoringRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DatabaseSessionTerminationDto>> GetTerminationHistoryAsync(
        int take,
        CancellationToken cancellationToken);

    Task<DatabaseSessionTerminationDto> TerminateSessionAsync(
        int sessionId,
        TerminateDatabaseSessionRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken);

    Task<DatabaseRollbackStatusDto> GetRollbackStatusAsync(
        int sessionId,
        CancellationToken cancellationToken);
}

public interface ISafeWriteReviewService
{
    Task<SafeWriteReviewResultDto> RecheckOwnedAsync(
        string operationCode,
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);

    Task<SafeWriteReviewResultDto> CancelOwnedRetryAllowedAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);

    Task<SafeWriteReviewResultDto> RecheckAsync(
        Guid safeWriteId,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        CancellationToken cancellationToken);

    Task<SafeWriteReviewResultDto> AllowRetryAsync(
        Guid safeWriteId,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        CancellationToken cancellationToken);

    Task<SafeWriteReviewResultDto> CancelAsync(
        Guid safeWriteId,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SafeWriteReviewAuditDto>> GetHistoryAsync(
        Guid safeWriteId,
        CancellationToken cancellationToken);
}

public interface ISafeWriteRecoveryDispatcher
{
    Task<SafeWriteRecoveryResult> RecoverAsync(
        string operationCode,
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}

public sealed record SafeWriteRecoveryResult(
    string Status,
    object? Result,
    string RecoveryState = "Unknown",
    string RecommendedAction = "NeedsReview",
    string? ErrorCode = null,
    bool? Retryable = null);

public sealed record MonitoringOverviewDto(
    DateTime GeneratedAtUtc,
    string OverallStatus,
    string OverallStatusName,
    string Headline,
    DatabaseMonitoringOverviewDto Database,
    WorkerMonitoringSnapshotDto Workers,
    DatabaseIncidentOverviewDto Incidents,
    SafeWriteMonitoringOverviewDto SafeWrites);

public sealed record DatabaseMonitoringOverviewDto(
    string Status,
    int ActiveRequestCount,
    int BlockedRequestCount,
    int LongRunningRequestCount,
    int OpenTransactionCount);

public sealed record DatabaseIncidentOverviewDto(
    int OpenCount,
    int CriticalCount,
    int WarningCount,
    DateTime? LastSeenAtUtc);

public sealed record SafeWriteMonitoringOverviewDto(
    int WindowHours,
    int ProcessingCount,
    int StaleProcessingCount,
    int RetryableFailureCount,
    int ManualReviewCount,
    int UnclassifiedFailureCount,
    int CompletedCount);

public sealed record DatabaseMonitoringSnapshotDto(
    DateTime GeneratedAtUtc,
    string ServerName,
    string DatabaseName,
    string OverallStatus,
    int ActiveRequestCount,
    int BlockedRequestCount,
    int RootBlockerCount,
    int LongRunningRequestCount,
    int OpenTransactionCount,
    DatabaseMonitoringThresholdsDto Thresholds,
    IReadOnlyCollection<DatabaseActiveRequestDto> Requests,
    IReadOnlyCollection<DatabaseBlockingEdgeDto> Blocking,
    IReadOnlyCollection<DatabaseOpenTransactionDto> OpenTransactions,
    IReadOnlyCollection<DatabaseRecommendationDto> Recommendations);

public sealed record DatabaseMonitoringThresholdsDto(
    int BlockingSeconds,
    int LongRunningSeconds,
    int OpenTransactionSeconds,
    int RefreshSeconds,
    int RetentionHours,
    int MaxIncidentCount);

public sealed record DatabaseActiveRequestDto(
    int SessionId,
    int RequestId,
    DateTime LoginTime,
    int? HostProcessId,
    string? DatabaseName,
    string Status,
    string Command,
    DateTime StartTime,
    long ElapsedMilliseconds,
    string? WaitType,
    long WaitMilliseconds,
    string? WaitResource,
    int? BlockingSessionId,
    long CpuMilliseconds,
    long Reads,
    long LogicalReads,
    long Writes,
    int OpenTransactionCount,
    decimal PercentComplete,
    string LoginName,
    string? HostName,
    string? ProgramName,
    string? ClientAddress,
    string SqlText,
    string Severity,
    string Recommendation,
    bool CanTerminate);

public sealed record DatabaseBlockingEdgeDto(
    int RootSessionId,
    int BlockingSessionId,
    int BlockedSessionId,
    int Depth,
    string? WaitType,
    long WaitMilliseconds,
    string BlockedSqlText);

public sealed record DatabaseOpenTransactionDto(
    int SessionId,
    DateTime LoginTime,
    int? HostProcessId,
    long TransactionId,
    DateTime TransactionBeginTime,
    long AgeMilliseconds,
    string TransactionState,
    int OpenTransactionCount,
    string LoginName,
    string? HostName,
    string? ProgramName,
    string? ClientAddress,
    string? LastSqlText,
    string Severity,
    string Recommendation,
    bool CanTerminate);

public sealed record DatabaseRecommendationDto(
    string Code,
    string Severity,
    string Title,
    string Description,
    int AffectedSessionCount);

public sealed record DatabaseMonitoringIncidentDto(
    Guid Id,
    string Type,
    string Severity,
    int SessionId,
    int? BlockingSessionId,
    string? DatabaseName,
    string? LoginName,
    string? HostName,
    string? ProgramName,
    string? WaitType,
    long ElapsedMilliseconds,
    string? SqlText,
    string Recommendation,
    DateTime FirstSeenAtUtc,
    DateTime LastSeenAtUtc,
    int OccurrenceCount,
    DateTime? ResolvedAtUtc);

public sealed record SafeWriteMonitoringRequest(
    string? Status,
    string? OperationCode,
    int? WarehouseNo,
    bool? Retryable,
    bool IncludeCompleted,
    int Take);

public sealed record SafeWriteMonitoringItemDto(
    Guid Id,
    string OperationCode,
    Guid RequestedByUserId,
    int WarehouseNo,
    Guid ClientRequestId,
    string Status,
    string? ErrorCode,
    bool? Retryable,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? CompletedAtUtc,
    long AgeSeconds);

public sealed record SafeWriteReviewRequest(string Reason);

public sealed record SafeWriteReviewResultDto(
    string Action,
    string Outcome,
    string Message,
    SafeWriteMonitoringItemDto SafeWrite,
    object? Result,
    string RecoveryState,
    string RecommendedAction);

public sealed record SafeWriteReviewAuditDto(
    Guid Id,
    Guid SafeWriteId,
    string Action,
    string Outcome,
    string Reason,
    Guid ReviewedByUserId,
    DateTime ReviewedAtUtc,
    string PreviousStatus,
    string? PreviousErrorCode,
    string ResultStatus,
    string? ResultErrorCode,
    string? Message);

public sealed record TerminateDatabaseSessionRequest(
    DateTime ExpectedLoginTime,
    int? ExpectedHostProcessId,
    string? ExpectedProgramName,
    string Reason);

public sealed record DatabaseSessionTerminationDto(
    Guid Id,
    int SessionId,
    DateTime LoginTime,
    int? HostProcessId,
    string? LoginName,
    string? HostName,
    string? ProgramName,
    string? DatabaseName,
    string? SqlText,
    string Reason,
    Guid RequestedByUserId,
    DateTime RequestedAtUtc,
    bool IsSucceeded,
    DateTime? CompletedAtUtc,
    string? Error);

public sealed record DatabaseRollbackStatusDto(
    int SessionId,
    bool IsRollingBack,
    string Message,
    DateTime CheckedAtUtc);
