namespace FurpaMerkezApi.Domain.Entities;

public sealed class StockAnomaly
{
    private readonly List<StockAnomalyEvent> events = [];

    private StockAnomaly()
    {
        SourceKey = string.Empty;
        Message = string.Empty;
    }

    public Guid Id { get; private set; }

    public string SourceKey { get; private set; }

    public StockAnomalyType Type { get; private set; }

    public StockAnomalySeverity Severity { get; private set; }

    public StockAnomalyStatus Status { get; private set; }

    public int WarehouseNo { get; private set; }

    public int? RelatedWarehouseNo { get; private set; }

    public string? WarehouseName { get; private set; }

    public string? RelatedWarehouseName { get; private set; }

    public string? ProductCode { get; private set; }

    public string? ProductName { get; private set; }

    public string? DocumentSerie { get; private set; }

    public int? DocumentOrderNo { get; private set; }

    public string? DocumentNo { get; private set; }

    public Guid? MovementGuid { get; private set; }

    public double? Quantity { get; private set; }

    public double? ExpectedQuantity { get; private set; }

    public double? ActualQuantity { get; private set; }

    public double? AverageQuantity { get; private set; }

    public DateTime? OccurredAtUtc { get; private set; }

    public string Message { get; private set; }

    public string? Evidence { get; private set; }

    public Guid? LastChangedByUserId { get; private set; }

    public DateTime FirstDetectedAtUtc { get; private set; }

    public DateTime LastDetectedAtUtc { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    public IReadOnlyCollection<StockAnomalyEvent> Events => events;

    public StockAnomaly(
        Guid id,
        string sourceKey,
        StockAnomalyType type,
        StockAnomalySeverity severity,
        int warehouseNo,
        DateTime detectedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Stock anomaly id can not be empty.", nameof(id));
        }

        Id = id;
        SourceKey = NormalizeRequired(sourceKey, nameof(sourceKey), 220);
        Type = type;
        Severity = severity;
        Status = StockAnomalyStatus.Open;
        WarehouseNo = ValidateWarehouseNo(warehouseNo, nameof(warehouseNo));
        FirstDetectedAtUtc = NormalizeUtc(detectedAtUtc);
        LastDetectedAtUtc = FirstDetectedAtUtc;
        Message = string.Empty;
    }

    public void Detect(
        StockAnomalySeverity severity,
        int? relatedWarehouseNo,
        string? warehouseName,
        string? relatedWarehouseName,
        string? productCode,
        string? productName,
        string? documentSerie,
        int? documentOrderNo,
        string? documentNo,
        Guid? movementGuid,
        double? quantity,
        double? expectedQuantity,
        double? actualQuantity,
        double? averageQuantity,
        DateTime? occurredAtUtc,
        string message,
        string? evidence,
        DateTime detectedAtUtc)
    {
        Severity = severity;
        RelatedWarehouseNo = relatedWarehouseNo.HasValue
            ? ValidateWarehouseNo(relatedWarehouseNo.Value, nameof(relatedWarehouseNo))
            : null;
        WarehouseName = NormalizeOptional(warehouseName, 120);
        RelatedWarehouseName = NormalizeOptional(relatedWarehouseName, 120);
        ProductCode = NormalizeOptional(productCode, 50);
        ProductName = NormalizeOptional(productName, 200);
        DocumentSerie = NormalizeOptional(documentSerie, 20);
        DocumentOrderNo = documentOrderNo;
        DocumentNo = NormalizeOptional(documentNo, 50);
        MovementGuid = movementGuid;
        Quantity = quantity;
        ExpectedQuantity = expectedQuantity;
        ActualQuantity = actualQuantity;
        AverageQuantity = averageQuantity;
        OccurredAtUtc = occurredAtUtc.HasValue ? NormalizeUtc(occurredAtUtc.Value) : null;
        Message = NormalizeRequired(message, nameof(message), 500);
        Evidence = NormalizeOptional(evidence, 4000);
        LastDetectedAtUtc = NormalizeUtc(detectedAtUtc);

        if (Status == StockAnomalyStatus.Resolved)
        {
            Status = StockAnomalyStatus.Open;
            ResolvedAtUtc = null;
        }

        events.Add(new StockAnomalyEvent(
            Guid.NewGuid(),
            Id,
            StockAnomalyEventType.Detected,
            Status,
            "Anomali taramada yakalandi.",
            null,
            LastDetectedAtUtc));
    }

    public void ChangeStatus(
        StockAnomalyStatus status,
        string? note,
        Guid? changedByUserId,
        DateTime changedAtUtc)
    {
        Status = status;
        LastChangedByUserId = changedByUserId;
        ResolvedAtUtc = status == StockAnomalyStatus.Resolved ? NormalizeUtc(changedAtUtc) : null;

        events.Add(new StockAnomalyEvent(
            Guid.NewGuid(),
            Id,
            StockAnomalyEventType.StatusChanged,
            status,
            NormalizeOptional(note, 500) ?? $"Durum {status} olarak guncellendi.",
            changedByUserId,
            NormalizeUtc(changedAtUtc)));
    }

    private static int ValidateWarehouseNo(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Warehouse no must be greater than zero.");
        }

        return value;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        var normalized = NormalizeOptional(value, maxLength);
        return normalized ?? throw new ArgumentException($"{parameterName} is required.", parameterName);
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public enum StockAnomalyType
{
    NegativeStock = 1,
    DuplicateDocument = 2,
    ReceivingDifference = 3,
    HighQuantity = 4,
    DormantStock = 5,
    PendingInterWarehouseTransfer = 6
}

public enum StockAnomalySeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum StockAnomalyStatus
{
    Open = 1,
    Acknowledged = 2,
    Resolved = 3,
    Ignored = 4
}

public enum StockAnomalyEventType
{
    Detected = 1,
    StatusChanged = 2
}
