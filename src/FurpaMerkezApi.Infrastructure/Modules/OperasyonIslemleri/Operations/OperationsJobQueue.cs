using System.Collections.Concurrent;
using System.Threading.Channels;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;

internal sealed class OperationsJobQueue(IClock clock)
{
    private readonly Channel<OperationsJobWorkItem> channel = Channel.CreateUnbounded<OperationsJobWorkItem>();
    private readonly ConcurrentDictionary<Guid, OperationsJobRecord> jobs = new();

    public OperationJobDto Enqueue(OperationFileKind kind, int warehouseNo, Guid requestedByUserId)
    {
        var jobId = Guid.NewGuid();
        var createdAtUtc = clock.UtcNow;
        var record = new OperationsJobRecord(jobId, kind, warehouseNo, requestedByUserId, createdAtUtc);

        jobs[jobId] = record;
        channel.Writer.TryWrite(new OperationsJobWorkItem(jobId, kind, warehouseNo, requestedByUserId));
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
            record.Message = message;
            record.ErrorMessage = null;
            record.Files = files;
        }
    }

    public void MarkFailed(Guid jobId, string errorMessage)
    {
        var record = GetRecord(jobId);

        lock (record.SyncRoot)
        {
            record.Status = OperationsJobStatus.Failed;
            record.CompletedAtUtc = clock.UtcNow;
            record.Message = "Operation failed.";
            record.ErrorMessage = errorMessage;
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
    PromoFile = 4
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
