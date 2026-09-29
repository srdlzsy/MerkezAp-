namespace FurpaMerkezApi.Domain.Entities;

public sealed class TrendyolGoBranchPosPriceSyncTask
{
    private TrendyolGoBranchPosPriceSyncTask()
    {
        PayloadJson = "[]";
    }

    public TrendyolGoBranchPosPriceSyncTask(Guid id, long storeId, int warehouseNo, string payloadJson, DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Task id can not be empty.", nameof(id));
        if (storeId <= 0) throw new ArgumentOutOfRangeException(nameof(storeId));
        if (warehouseNo <= 0) throw new ArgumentOutOfRangeException(nameof(warehouseNo));
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new ArgumentException("Payload is required.", nameof(payloadJson));

        Id = id;
        StoreId = storeId;
        WarehouseNo = warehouseNo;
        PayloadJson = payloadJson;
        Status = TrendyolGoBranchPosPriceSyncStatus.Pending;
        NextAttemptAtUtc = NormalizeUtc(createdAtUtc);
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
    }

    public Guid Id { get; private set; }
    public long StoreId { get; private set; }
    public int WarehouseNo { get; private set; }
    public string PayloadJson { get; private set; }
    public TrendyolGoBranchPosPriceSyncStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public void MarkSucceeded(DateTime completedAtUtc)
    {
        Status = TrendyolGoBranchPosPriceSyncStatus.Succeeded;
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
        LastError = null;
    }

    public void MarkProcessing(DateTime leaseExpiresAtUtc)
    {
        Status = TrendyolGoBranchPosPriceSyncStatus.Processing;
        NextAttemptAtUtc = NormalizeUtc(leaseExpiresAtUtc);
    }

    public void ScheduleRetry(string error, DateTime nextAttemptAtUtc)
    {
        AttemptCount++;
        Status = TrendyolGoBranchPosPriceSyncStatus.Pending;
        LastError = string.IsNullOrWhiteSpace(error) ? "Branch POS price synchronization failed." : error[..Math.Min(error.Length, 2000)];
        NextAttemptAtUtc = NormalizeUtc(nextAttemptAtUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public enum TrendyolGoBranchPosPriceSyncStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3
}
