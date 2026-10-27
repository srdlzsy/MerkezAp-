using System.Collections.Concurrent;
using System.Globalization;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationQueue(IClock clock)
{
    private const int MaxPendingJobCount = 100;
    private const int MaxRetainedJobCount = 500;
    private readonly ConcurrentDictionary<Guid, AxataSynchronizationJobRecord> jobs = new();
    private readonly object enqueueSyncRoot = new();
    private readonly SemaphoreSlim orderDispatchSignal = new(0);
    private readonly SemaphoreSlim generalSignal = new(0);
    private readonly ConcurrentQueue<AxataSynchronizationJobWorkItem> orderDispatchPending = new();
    private readonly ConcurrentQueue<AxataSynchronizationJobWorkItem> operationalPending = new();
    private readonly ConcurrentQueue<AxataSynchronizationJobWorkItem> backgroundPending = new();
    private int pendingCount;
    private int runningCount;

    public AxataSynchronizationJobDto Enqueue(
        AxataSynchronizationTaskDefinition definition,
        AxataSynchronizationJobExecutionMode executionMode,
        AxataSynchronizationJobTriggerSource triggerSource,
        int? warehouseNo,
        Guid requestedByUserId)
    {
        lock (enqueueSyncRoot)
        {
            RemoveOldCompletedJobs();

            if (Volatile.Read(ref pendingCount) >= MaxPendingJobCount)
            {
                throw new InvalidOperationException(
                    $"AXATA synchronization queue reached its {MaxPendingJobCount} job capacity.");
            }

            if (HasActiveJob(definition.Code, warehouseNo))
            {
                throw new InvalidOperationException(
                    $"AXATA synchronization task '{definition.Code}' already has an active job for warehouse '{warehouseNo}'.");
            }

            var jobId = Guid.NewGuid();
            var createdAtUtc = clock.UtcNow;
            var record = new AxataSynchronizationJobRecord(
                jobId,
                definition.Code,
                definition.Name,
                executionMode,
                triggerSource,
                warehouseNo,
                requestedByUserId,
                createdAtUtc);

            jobs[jobId] = record;
            var workItem = new AxataSynchronizationJobWorkItem(
                jobId,
                definition.Code,
                definition.Name,
                executionMode,
                triggerSource,
                warehouseNo,
                requestedByUserId,
                createdAtUtc);
            var queueLane = ResolveQueueLane(definition.Code);
            ResolveQueue(queueLane, triggerSource).Enqueue(workItem);
            Interlocked.Increment(ref pendingCount);
            ResolveSignal(queueLane).Release();

            return record.ToSummary();
        }
    }

    public async ValueTask<AxataSynchronizationJobWorkItem> DequeueOrderDispatchAsync(
        CancellationToken cancellationToken)
    {
        await orderDispatchSignal.WaitAsync(cancellationToken);

        if (orderDispatchPending.TryDequeue(out var workItem))
        {
            Interlocked.Decrement(ref pendingCount);
            return workItem;
        }

        throw new InvalidOperationException("AXATA order dispatch queue signal was received without any job.");
    }

    public async ValueTask<AxataSynchronizationJobWorkItem> DequeueGeneralAsync(
        CancellationToken cancellationToken)
    {
        await generalSignal.WaitAsync(cancellationToken);

        if (operationalPending.TryDequeue(out var workItem) ||
            backgroundPending.TryDequeue(out workItem))
        {
            Interlocked.Decrement(ref pendingCount);
            return workItem;
        }

        throw new InvalidOperationException("AXATA general queue signal was received without any job.");
    }

    public AxataSynchronizationJobDetailDto Get(Guid jobId) =>
        GetRecord(jobId).ToDetail();

    public IReadOnlyCollection<AxataSynchronizationJobDto> ListRecent(int take) =>
        jobs.Values
            .OrderByDescending(record => record.CreatedAtUtc)
            .Take(Math.Max(1, take))
            .Select(record => record.ToSummary())
            .ToArray();

    public bool HasActiveJob(string taskCode, int? warehouseNo) =>
        jobs.Values.Any(record =>
            string.Equals(record.TaskCode, taskCode, StringComparison.OrdinalIgnoreCase) &&
            record.WarehouseNo == warehouseNo &&
            (record.Status == AxataSynchronizationJobStatus.Queued ||
             record.Status == AxataSynchronizationJobStatus.Running));

    public void MarkRunning(Guid jobId)
    {
        var record = GetRecord(jobId);

        lock (record.SyncRoot)
        {
            record.Status = AxataSynchronizationJobStatus.Running;
            record.StartedAtUtc = clock.UtcNow;
            record.Message = "Senkronizasyon gorevi calisiyor.";
            record.ErrorMessage = null;
        }

        Interlocked.Increment(ref runningCount);
    }

    public void MarkSucceeded(Guid jobId, AxataSynchronizationTaskExecutionResult result)
    {
        var record = GetRecord(jobId);

        lock (record.SyncRoot)
        {
            record.Status = AxataSynchronizationJobStatus.Succeeded;
            record.CompletedAtUtc = clock.UtcNow;
            record.Message = result.Message;
            record.ErrorMessage = null;
            record.AffectedRecordCount = result.AffectedRecordCount;
            record.Artifacts = result.Artifacts;
        }

        Interlocked.Decrement(ref runningCount);
    }

    public void MarkFailed(Guid jobId, string errorMessage)
    {
        var record = GetRecord(jobId);

        lock (record.SyncRoot)
        {
            record.Status = AxataSynchronizationJobStatus.Failed;
            record.CompletedAtUtc = clock.UtcNow;
            record.Message = "Senkronizasyon gorevi basarisiz oldu.";
            record.ErrorMessage = errorMessage;
        }

        Interlocked.Decrement(ref runningCount);
    }

    internal WorkerBacklogSnapshot GetBacklogSnapshot()
    {
        var oldestQueuedAtUtc = GetOldestQueuedAtUtc();
        var snapshots = jobs.Values
            .Select(record => record.ToRuntimeSnapshot())
            .ToArray();
        var currentJob = snapshots
            .Where(job => string.Equals(job.Status, "Running", StringComparison.Ordinal))
            .OrderByDescending(job => job.StartedAtUtc ?? job.CreatedAtUtc)
            .FirstOrDefault();
        var lastJob = snapshots
            .Where(job => job.CompletedAtUtc.HasValue)
            .OrderByDescending(job => job.CompletedAtUtc)
            .FirstOrDefault();
        var unresolvedFailures = snapshots
            .Where(job => job.CompletedAtUtc.HasValue)
            .GroupBy(job => job.TaskCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(job => job.CompletedAtUtc).First())
            .Where(job => string.Equals(job.Status, "Failed", StringComparison.Ordinal))
            .OrderByDescending(job => job.CompletedAtUtc)
            .ToArray();

        return new WorkerBacklogSnapshot(
            Math.Max(0, Volatile.Read(ref pendingCount)),
            Math.Max(0, Volatile.Read(ref runningCount)),
            oldestQueuedAtUtc,
            currentJob,
            lastJob,
            unresolvedFailures.Length,
            unresolvedFailures.FirstOrDefault());
    }

    private AxataSynchronizationJobRecord GetRecord(Guid jobId) =>
        jobs.TryGetValue(jobId, out var record)
            ? record
            : throw new KeyNotFoundException($"AXATA synchronization job '{jobId}' was not found.");

    private ConcurrentQueue<AxataSynchronizationJobWorkItem> ResolveQueue(
        AxataSynchronizationQueueLane queueLane,
        AxataSynchronizationJobTriggerSource triggerSource) =>
        queueLane == AxataSynchronizationQueueLane.OrderDispatch
            ? orderDispatchPending
            : triggerSource == AxataSynchronizationJobTriggerSource.Manual ||
              queueLane == AxataSynchronizationQueueLane.Operational
                ? operationalPending
                : backgroundPending;

    private static AxataSynchronizationQueueLane ResolveQueueLane(
        string taskCode) =>
        AxataSynchronizationCatalog.IsOrderDispatchTask(taskCode)
            ? AxataSynchronizationQueueLane.OrderDispatch
            : AxataSynchronizationCatalog.IsOperationalTask(taskCode)
                ? AxataSynchronizationQueueLane.Operational
                : AxataSynchronizationQueueLane.Background;

    private SemaphoreSlim ResolveSignal(AxataSynchronizationQueueLane queueLane) =>
        queueLane == AxataSynchronizationQueueLane.OrderDispatch
            ? orderDispatchSignal
            : generalSignal;

    private DateTime? GetOldestQueuedAtUtc()
    {
        return new DateTime?[]
            {
                orderDispatchPending.TryPeek(out var orderDispatch) ? orderDispatch.CreatedAtUtc : null,
                operationalPending.TryPeek(out var operational) ? operational.CreatedAtUtc : null,
                backgroundPending.TryPeek(out var background) ? background.CreatedAtUtc : null
            }
            .Where(value => value.HasValue)
            .Min();
    }

    private void RemoveOldCompletedJobs()
    {
        var removeCount = jobs.Count - MaxRetainedJobCount + 1;
        if (removeCount <= 0)
        {
            return;
        }

        var removableJobIds = jobs.Values
            .Where(record =>
                record.Status == AxataSynchronizationJobStatus.Succeeded ||
                record.Status == AxataSynchronizationJobStatus.Failed)
            .OrderBy(record => record.CompletedAtUtc ?? record.CreatedAtUtc)
            .Take(removeCount)
            .Select(record => record.JobId)
            .ToArray();

        foreach (var jobId in removableJobIds)
        {
            jobs.TryRemove(jobId, out _);
        }
    }
}

internal sealed class AxataSynchronizationJobRecord(
    Guid jobId,
    string taskCode,
    string taskName,
    AxataSynchronizationJobExecutionMode executionMode,
    AxataSynchronizationJobTriggerSource triggerSource,
    int? warehouseNo,
    Guid requestedByUserId,
    DateTime createdAtUtc)
{
    public object SyncRoot { get; } = new();

    public Guid JobId { get; } = jobId;

    public string TaskCode { get; } = taskCode;

    public string TaskName { get; } = taskName;

    public AxataSynchronizationJobStatus Status { get; set; } = AxataSynchronizationJobStatus.Queued;

    public AxataSynchronizationJobExecutionMode ExecutionMode { get; } = executionMode;

    public AxataSynchronizationJobTriggerSource TriggerSource { get; } = triggerSource;

    public int? WarehouseNo { get; } = warehouseNo;

    public Guid RequestedByUserId { get; } = requestedByUserId;

    public DateTime CreatedAtUtc { get; } = createdAtUtc;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public int AffectedRecordCount { get; set; }

    public string? Message { get; set; } = "Senkronizasyon gorevi kuyruga alindi.";

    public string? ErrorMessage { get; set; }

    public IReadOnlyCollection<AxataSynchronizationJobArtifactDto> Artifacts { get; set; } =
        Array.Empty<AxataSynchronizationJobArtifactDto>();

    public AxataSynchronizationJobDto ToSummary() =>
        new(
            JobId,
            TaskCode,
            TaskName,
            Status.ToExternalValue(),
            ExecutionMode.ToExternalValue(),
            TriggerSource.ToExternalValue(),
            WarehouseNo,
            CreatedAtUtc);

    public AxataSynchronizationJobDetailDto ToDetail() =>
        new(
            JobId,
            TaskCode,
            TaskName,
            Status.ToExternalValue(),
            ExecutionMode.ToExternalValue(),
            TriggerSource.ToExternalValue(),
            WarehouseNo,
            RequestedByUserId,
            CreatedAtUtc,
            StartedAtUtc,
            CompletedAtUtc,
            AffectedRecordCount,
            Message,
            ErrorMessage,
            Artifacts);

    public WorkerJobRuntimeSnapshot ToRuntimeSnapshot()
    {
        lock (SyncRoot)
        {
            return new WorkerJobRuntimeSnapshot(
                JobId,
                TaskCode,
                TaskName,
                Status.ToExternalValue(),
                ExecutionMode.ToExternalValue(),
                TriggerSource.ToExternalValue(),
                WarehouseNo,
                CreatedAtUtc,
                StartedAtUtc,
                CompletedAtUtc,
                AffectedRecordCount,
                Message,
                ErrorMessage);
        }
    }
}

internal sealed record AxataSynchronizationJobWorkItem(
    Guid JobId,
    string TaskCode,
    string TaskName,
    AxataSynchronizationJobExecutionMode ExecutionMode,
    AxataSynchronizationJobTriggerSource TriggerSource,
    int? WarehouseNo,
    Guid RequestedByUserId,
    DateTime CreatedAtUtc);

internal enum AxataSynchronizationQueueLane
{
    OrderDispatch = 1,
    Operational = 2,
    Background = 3
}

internal enum AxataSynchronizationJobStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4
}

internal enum AxataSynchronizationJobExecutionMode
{
    DryRun = 1,
    Outbox = 2,
    Live = 3
}

internal enum AxataSynchronizationJobTriggerSource
{
    Manual = 1,
    Scheduled = 2
}

internal static class AxataSynchronizationJobMappings
{
    public static AxataSynchronizationJobExecutionMode ParseExecutionMode(string executionMode)
    {
        if (string.Equals(executionMode, "DryRun", StringComparison.OrdinalIgnoreCase))
        {
            return AxataSynchronizationJobExecutionMode.DryRun;
        }

        if (string.Equals(executionMode, "Outbox", StringComparison.OrdinalIgnoreCase))
        {
            return AxataSynchronizationJobExecutionMode.Outbox;
        }

        if (string.Equals(executionMode, "Live", StringComparison.OrdinalIgnoreCase))
        {
            return AxataSynchronizationJobExecutionMode.Live;
        }

        throw new ArgumentException(
            $"Unsupported execution mode '{executionMode}'. Allowed values: DryRun, Outbox, Live.",
            nameof(executionMode));
    }

    public static string ToExternalValue(this AxataSynchronizationJobStatus status) =>
        status switch
        {
            AxataSynchronizationJobStatus.Queued => "Queued",
            AxataSynchronizationJobStatus.Running => "Running",
            AxataSynchronizationJobStatus.Succeeded => "Succeeded",
            AxataSynchronizationJobStatus.Failed => "Failed",
            _ => status.ToString()
        };

    public static string ToExternalValue(this AxataSynchronizationJobExecutionMode executionMode) =>
        executionMode switch
        {
            AxataSynchronizationJobExecutionMode.DryRun => "DryRun",
            AxataSynchronizationJobExecutionMode.Outbox => "Outbox",
            AxataSynchronizationJobExecutionMode.Live => "Live",
            _ => executionMode.ToString()
        };

    public static string ToExternalValue(this AxataSynchronizationJobTriggerSource triggerSource) =>
        triggerSource switch
        {
            AxataSynchronizationJobTriggerSource.Manual => "Manual",
            AxataSynchronizationJobTriggerSource.Scheduled => "Scheduled",
            _ => triggerSource.ToString()
        };
}
