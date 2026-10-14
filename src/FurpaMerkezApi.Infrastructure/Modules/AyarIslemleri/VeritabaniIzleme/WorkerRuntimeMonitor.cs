using System.Collections.Concurrent;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

internal sealed class WorkerRuntimeMonitor(IClock clock) : IWorkerMonitoringService
{
    internal const string AxataWorkerCode = "axata-synchronization-worker";
    internal const string AxataSchedulerCode = "axata-synchronization-scheduler";

    private static readonly TimeSpan BacklogWarningAge = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan BacklogCriticalAge = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<string, WorkerRuntimeState> states =
        new(StringComparer.OrdinalIgnoreCase);

    internal void Register(
        string code,
        string name,
        bool isCritical,
        TimeSpan staleAfter)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Worker code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Worker name is required.", nameof(name));
        if (staleAfter <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(staleAfter));

        states.GetOrAdd(
            code,
            _ => new WorkerRuntimeState(
                code.Trim(),
                name.Trim(),
                isCritical,
                staleAfter,
                clock.UtcNow));
    }

    internal void ReportDisabled(string code, string activity, WorkerBacklogSnapshot? backlog = null) =>
        Update(code, state =>
        {
            state.IsEnabled = false;
            state.Activity = activity;
            state.LastHeartbeatAtUtc = clock.UtcNow;
            ApplyBacklog(state, backlog);
        });

    internal void ReportHeartbeat(string code, string activity, WorkerBacklogSnapshot? backlog = null) =>
        Update(code, state =>
        {
            state.IsEnabled = true;
            state.Activity = activity;
            state.LastHeartbeatAtUtc = clock.UtcNow;
            ApplyBacklog(state, backlog);
        });

    internal void ReportSuccess(string code, string activity, WorkerBacklogSnapshot? backlog = null) =>
        Update(code, state =>
        {
            var now = clock.UtcNow;
            state.IsEnabled = true;
            state.Activity = activity;
            state.LastHeartbeatAtUtc = now;
            state.LastSuccessAtUtc = now;
            state.LastError = null;
            ApplyBacklog(state, backlog);
        });

    internal void ReportFailure(
        string code,
        string activity,
        Exception exception,
        WorkerBacklogSnapshot? backlog = null) =>
        Update(code, state =>
        {
            var now = clock.UtcNow;
            state.IsEnabled = true;
            state.Activity = activity;
            state.LastHeartbeatAtUtc = now;
            state.LastFailureAtUtc = now;
            state.LastError = Limit(exception.Message, 2000);
            ApplyBacklog(state, backlog);
        });

    public WorkerMonitoringSnapshotDto GetSnapshot()
    {
        var now = clock.UtcNow;
        var workers = states.Values
            .Select(state => ToDto(state, now))
            .OrderByDescending(item => item.IsCritical)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var overallStatus = workers.Any(item => item.Status == "Unhealthy")
            ? "critical"
            : workers.Any(item => item.Status == "Degraded")
                ? "warning"
                : "healthy";

        return new WorkerMonitoringSnapshotDto(now, overallStatus, workers);
    }

    private void Update(string code, Action<WorkerRuntimeState> update)
    {
        if (!states.TryGetValue(code, out var state))
        {
            throw new InvalidOperationException($"Worker '{code}' must be registered before reporting runtime state.");
        }

        lock (state.SyncRoot)
        {
            update(state);
        }
    }

    private static void ApplyBacklog(WorkerRuntimeState state, WorkerBacklogSnapshot? backlog)
    {
        if (backlog is null)
        {
            return;
        }

        state.QueuedCount = Math.Max(0, backlog.QueuedCount);
        state.RunningCount = Math.Max(0, backlog.RunningCount);
        state.OldestQueuedAtUtc = backlog.OldestQueuedAtUtc;
    }

    private static WorkerMonitoringItemDto ToDto(WorkerRuntimeState state, DateTime now)
    {
        lock (state.SyncRoot)
        {
            var oldestAge = state.OldestQueuedAtUtc.HasValue
                ? MaxZero(now - state.OldestQueuedAtUtc.Value)
                : (TimeSpan?)null;
            var heartbeatAge = state.LastHeartbeatAtUtc.HasValue
                ? MaxZero(now - state.LastHeartbeatAtUtc.Value)
                : (TimeSpan?)null;
            var hasUnresolvedFailure = state.LastFailureAtUtc.HasValue &&
                (!state.LastSuccessAtUtc.HasValue || state.LastFailureAtUtc > state.LastSuccessAtUtc);
            var heartbeatIsStale = !heartbeatAge.HasValue || heartbeatAge > state.StaleAfter;
            var backlogIsCritical = state.QueuedCount > 0 && oldestAge >= BacklogCriticalAge;
            var backlogIsWarning = state.QueuedCount > 0 && oldestAge >= BacklogWarningAge;

            var status = !state.IsEnabled
                ? "Disabled"
                : heartbeatIsStale || backlogIsCritical
                    ? state.IsCritical ? "Unhealthy" : "Degraded"
                    : hasUnresolvedFailure || backlogIsWarning
                        ? "Degraded"
                        : "Healthy";

            return new WorkerMonitoringItemDto(
                state.Code,
                state.Name,
                state.IsCritical,
                state.IsEnabled,
                status,
                state.Activity,
                state.RegisteredAtUtc,
                state.LastHeartbeatAtUtc,
                state.LastSuccessAtUtc,
                state.LastFailureAtUtc,
                state.LastError,
                state.QueuedCount,
                state.RunningCount,
                state.OldestQueuedAtUtc,
                oldestAge.HasValue ? (long)oldestAge.Value.TotalSeconds : null,
                (int)state.StaleAfter.TotalSeconds);
        }
    }

    private static TimeSpan MaxZero(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

internal sealed class WorkerRuntimeState(
    string code,
    string name,
    bool isCritical,
    TimeSpan staleAfter,
    DateTime registeredAtUtc)
{
    public object SyncRoot { get; } = new();
    public string Code { get; } = code;
    public string Name { get; } = name;
    public bool IsCritical { get; } = isCritical;
    public TimeSpan StaleAfter { get; } = staleAfter;
    public DateTime RegisteredAtUtc { get; } = registeredAtUtc;
    public bool IsEnabled { get; set; } = true;
    public string Activity { get; set; } = "Starting";
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public DateTime? LastSuccessAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }
    public string? LastError { get; set; }
    public int QueuedCount { get; set; }
    public int RunningCount { get; set; }
    public DateTime? OldestQueuedAtUtc { get; set; }
}

internal sealed record WorkerBacklogSnapshot(
    int QueuedCount,
    int RunningCount,
    DateTime? OldestQueuedAtUtc);
