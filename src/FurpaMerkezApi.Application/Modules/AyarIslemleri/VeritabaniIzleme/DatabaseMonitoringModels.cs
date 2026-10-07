namespace FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;

public interface IDatabaseMonitoringService
{
    Task<DatabaseMonitoringSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<DatabaseMonitoringIncidentDto>> GetIncidentsAsync(
        int take,
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
