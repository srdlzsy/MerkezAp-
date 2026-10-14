using System.Collections.Concurrent;
using System.Threading.Channels;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;

internal sealed class OperationsJobQueue
{
    private const int DefaultCapacity = 100;
    private const int DefaultMaximumCompletedJobs = 1000;
    private static readonly TimeSpan DefaultCompletedRetention = TimeSpan.FromHours(1);

    private readonly IClock clock;
    private readonly Channel<OperationsJobWorkItem> channel;
    private readonly ConcurrentDictionary<Guid, OperationsJobRecord> jobs = new();
    private readonly TimeSpan completedRetention;
    private readonly int maximumCompletedJobs;
    private long completionSequence;

    public OperationsJobQueue(IClock clock)
        : this(clock, DefaultCapacity, DefaultCompletedRetention, DefaultMaximumCompletedJobs)
    {
    }

    internal OperationsJobQueue(
        IClock clock,
        int capacity,
        TimeSpan completedRetention,
        int maximumCompletedJobs)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (completedRetention < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(completedRetention));
        if (maximumCompletedJobs <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCompletedJobs));

        this.clock = clock;
        this.completedRetention = completedRetention;
        this.maximumCompletedJobs = maximumCompletedJobs;
        channel = Channel.CreateBounded<OperationsJobWorkItem>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public OperationJobDto Enqueue(OperationFileKind kind, int warehouseNo, Guid requestedByUserId)
    {
        CleanupCompletedJobs();

        var jobId = Guid.NewGuid();
        var createdAtUtc = clock.UtcNow;
        var record = new OperationsJobRecord(jobId, kind, warehouseNo, requestedByUserId, createdAtUtc);

        jobs[jobId] = record;
        if (!channel.Writer.TryWrite(new OperationsJobWorkItem(jobId, kind, warehouseNo, requestedByUserId)))
        {
            jobs.TryRemove(jobId, out _);
            throw new InvalidOperationException(
                "Operations job queue is full. Wait for a queued operation to finish and try again.");
        }

        return record.ToSummary();
    }

    public OperationJobDetailDto Get(Guid jobId)
    {
        if (!jobs.TryGetValue(jobId, out var record))
        {
            throw new KeyNotFoundException("Operation job was not found.");
        }

        return record.ToDetail();
    }

    public ValueTask<OperationsJobWorkItem> DequeueAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAsync(cancellationToken);

    public void MarkRunning(Guid jobId)
    {
        var record = GetRecord(jobId);

        lock (record.SyncRoot)
        {
            record.Status = OperationsJobStatus.Running;
            record.StartedAtUtc = clock.UtcNow;
            record.Message = "Operation is running.";
            record.ErrorMessage = null;
        }
    }

    public void MarkSucceeded(Guid jobId, string message, IReadOnlyCollection<GeneratedOperationFileDto> files)
    {
        var record = GetRecord(jobId);

        lock (record.SyncRoot)
        {
            record.Status = OperationsJobStatus.Succeeded;
            record.CompletedAtUtc = clock.UtcNow;
            record.CompletionSequence = Interlocked.Increment(ref completionSequence);
            record.Message = message;
            record.ErrorMessage = null;
            record.Files = files;
        }

        CleanupCompletedJobs();
    }

    public void MarkFailed(Guid jobId, string errorMessage)
    {
        var record = GetRecord(jobId);

        lock (record.SyncRoot)
        {
            record.Status = OperationsJobStatus.Failed;
            record.CompletedAtUtc = clock.UtcNow;
            record.CompletionSequence = Interlocked.Increment(ref completionSequence);
            record.Message = "Operation failed.";
            record.ErrorMessage = errorMessage;
        }

        CleanupCompletedJobs();
    }

    private void CleanupCompletedJobs()
    {
        var now = clock.UtcNow;
        var completed = jobs.Values
            .Select(record =>
            {
                lock (record.SyncRoot)
                {
                    return (Record: record, record.CompletedAtUtc, record.CompletionSequence);
                }
            })
            .Where(item => item.CompletedAtUtc.HasValue)
            .OrderBy(item => item.CompletedAtUtc)
            .ThenBy(item => item.CompletionSequence)
            .ToArray();

        foreach (var item in completed.Where(item => now - item.CompletedAtUtc!.Value >= completedRetention))
        {
            jobs.TryRemove(item.Record.JobId, out _);
        }

        var retainedCompleted = completed
            .Where(item => jobs.ContainsKey(item.Record.JobId))
            .ToArray();
        var overflow = retainedCompleted.Length - maximumCompletedJobs;
        for (var index = 0; index < overflow; index++)
        {
            jobs.TryRemove(retainedCompleted[index].Record.JobId, out _);
        }
    }

    private OperationsJobRecord GetRecord(Guid jobId) =>
        jobs.TryGetValue(jobId, out var record)
            ? record
            : throw new KeyNotFoundException("Operation job was not found.");
}

internal sealed class OperationsJobRecord(
    Guid jobId,
    OperationFileKind kind,
    int warehouseNo,
    Guid requestedByUserId,
    DateTime createdAtUtc)
{
    public object SyncRoot { get; } = new();

    public Guid JobId { get; } = jobId;

    public OperationFileKind Kind { get; } = kind;

    public int WarehouseNo { get; } = warehouseNo;

    public Guid RequestedByUserId { get; } = requestedByUserId;

    public DateTime CreatedAtUtc { get; } = createdAtUtc;

    public OperationsJobStatus Status { get; set; } = OperationsJobStatus.Queued;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public long CompletionSequence { get; set; }

    public string? Message { get; set; } = "Operation is queued.";

    public string? ErrorMessage { get; set; }

    public IReadOnlyCollection<GeneratedOperationFileDto> Files { get; set; } =
        Array.Empty<GeneratedOperationFileDto>();

    public OperationJobDto ToSummary() =>
        new(
            JobId,
            Kind.ToExternalValue(),
            Status.ToExternalValue(),
            WarehouseNo,
            CreatedAtUtc);

    public OperationJobDetailDto ToDetail() =>
        new(
            JobId,
            Kind.ToExternalValue(),
            Status.ToExternalValue(),
            WarehouseNo,
            RequestedByUserId,
            CreatedAtUtc,
            StartedAtUtc,
            CompletedAtUtc,
            Message,
            ErrorMessage,
            Files);
}

internal sealed record OperationsJobWorkItem(
    Guid JobId,
    OperationFileKind Kind,
    int WarehouseNo,
    Guid RequestedByUserId);

internal enum OperationFileKind
{
    ScalesFile = 1,
    ProductBarcodePluNoFile = 2,
    CashierFile = 3,
    PromoFile = 4,
    CustomerFile = 5
}

internal enum OperationsJobStatus
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4
}

internal static class OperationsJobMappings
{
    public static string ToExternalValue(this OperationFileKind kind) =>
        kind switch
        {
            OperationFileKind.ScalesFile => "ScalesFile",
            OperationFileKind.ProductBarcodePluNoFile => "ProductBarcodePluNoFile",
            OperationFileKind.CashierFile => "CashierFile",
            OperationFileKind.PromoFile => "PromoFile",
            OperationFileKind.CustomerFile => "CustomerFile",
            _ => kind.ToString()
        };

    public static string ToExternalValue(this OperationsJobStatus status) =>
        status switch
        {
            OperationsJobStatus.Queued => "Queued",
            OperationsJobStatus.Running => "Running",
            OperationsJobStatus.Succeeded => "Succeeded",
            OperationsJobStatus.Failed => "Failed",
            _ => status.ToString()
        };
}
