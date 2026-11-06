namespace FurpaMerkezApi.Domain.Entities;

public sealed class TrendyolGoBatchFollowUpTask
{
    private TrendyolGoBatchFollowUpTask()
    {
        BatchRequestId = string.Empty;
        PayloadJson = "{}";
    }

    public TrendyolGoBatchFollowUpTask(
        Guid id,
        TrendyolGoBatchFollowUpKind kind,
        string batchRequestId,
        long storeId,
        int warehouseNo,
        string payloadJson,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Task id can not be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(batchRequestId)) throw new ArgumentException("Batch request id is required.", nameof(batchRequestId));
        if (storeId <= 0) throw new ArgumentOutOfRangeException(nameof(storeId));
        if (warehouseNo <= 0) throw new ArgumentOutOfRangeException(nameof(warehouseNo));
        if (string.IsNullOrWhiteSpace(payloadJson)) throw new ArgumentException("Payload is required.", nameof(payloadJson));

        Id = id;
        Kind = kind;
        BatchRequestId = batchRequestId.Trim();
        StoreId = storeId;
        WarehouseNo = warehouseNo;
        PayloadJson = payloadJson;
        Status = TrendyolGoBatchFollowUpStatus.Pending;
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
        NextAttemptAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public TrendyolGoBatchFollowUpKind Kind { get; private set; }
    public string BatchRequestId { get; private set; }
    public long StoreId { get; private set; }
    public int WarehouseNo { get; private set; }
    public string PayloadJson { get; private set; }
    public TrendyolGoBatchFollowUpStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public string? LastError { get; private set; }
    public string? ResultSummary { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public void ScheduleRetry(string? error, DateTime nextAttemptAtUtc)
    {
        AttemptCount++;
        Status = TrendyolGoBatchFollowUpStatus.Pending;
        LastError = Trim(error, 2000);
        NextAttemptAtUtc = NormalizeUtc(nextAttemptAtUtc);
    }

    public void MarkSucceeded(string summary, DateTime completedAtUtc)
    {
        Status = TrendyolGoBatchFollowUpStatus.Succeeded;
        ResultSummary = Trim(summary, 2000);
        LastError = null;
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
    }

    public void MarkPartiallySucceeded(string summary, string error, DateTime completedAtUtc)
    {
        Status = TrendyolGoBatchFollowUpStatus.PartiallySucceeded;
        ResultSummary = Trim(summary, 2000);
        LastError = Trim(error, 2000);
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
    }

    public void MarkFailed(string error, DateTime completedAtUtc)
    {
        Status = TrendyolGoBatchFollowUpStatus.Failed;
        LastError = Trim(error, 2000) ?? "Trendyol Go batch follow-up failed.";
        CompletedAtUtc = NormalizeUtc(completedAtUtc);
    }

    private static string? Trim(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed[..Math.Min(trimmed.Length, maximumLength)];
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public enum TrendyolGoBatchFollowUpKind
{
    PriceStock = 1,
    CatalogThenPriceStock = 2,
    SaleStatus = 3
}

public enum TrendyolGoBatchFollowUpStatus
{
    Pending = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
    PartiallySucceeded = 5
}
