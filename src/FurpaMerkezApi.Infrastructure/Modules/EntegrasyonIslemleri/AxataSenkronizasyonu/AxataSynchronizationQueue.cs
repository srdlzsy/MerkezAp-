using System.Collections.Concurrent;
using System.Globalization;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationQueue(IClock clock)
{
    private readonly ConcurrentDictionary<Guid, AxataSynchronizationJobRecord> jobs = new();
    private readonly SemaphoreSlim signal = new(0);
    private readonly ConcurrentQueue<AxataSynchronizationJobWorkItem> pending = new();

    public AxataSynchronizationJobDto Enqueue(
        AxataSynchronizationTaskDefinition definition,
        AxataSynchronizationJobExecutionMode executionMode,
        AxataSynchronizationJobTriggerSource triggerSource,
        int? warehouseNo,
        Guid requestedByUserId)
    {
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
        pending.Enqueue(new AxataSynchronizationJobWorkItem(
            jobId,
            definition.Code,
            definition.Name,
            executionMode,
            triggerSource,
            warehouseNo,
            requestedByUserId,
            createdAtUtc));
        signal.Release();

        return record.ToSummary();
    }

    public async ValueTask<AxataSynchronizationJobWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        await signal.WaitAsync(cancellationToken);

        if (pending.TryDequeue(out var workItem))
        {
            return workItem;
        }

        throw new InvalidOperationException("AXATA synchronization queue signal was received without any job.");
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
    }

    private AxataSynchronizationJobRecord GetRecord(Guid jobId) =>
        jobs.TryGetValue(jobId, out var record)
            ? record
            : throw new KeyNotFoundException($"AXATA synchronization job '{jobId}' was not found.");
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
    Outbox = 2
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

        throw new ArgumentException(
            $"Unsupported execution mode '{executionMode}'. Allowed values: DryRun, Outbox.",
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
