namespace FurpaMerkezApi.Domain.Entities;

public sealed class StockAnomalyEvent
{
    private StockAnomalyEvent()
    {
        Message = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid StockAnomalyId { get; private set; }

    public StockAnomalyEventType EventType { get; private set; }

    public StockAnomalyStatus Status { get; private set; }

    public string Message { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public StockAnomalyEvent(
        Guid id,
        Guid stockAnomalyId,
        StockAnomalyEventType eventType,
        StockAnomalyStatus status,
        string message,
        Guid? changedByUserId,
        DateTime occurredAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Stock anomaly event id can not be empty.", nameof(id));
        }

        if (stockAnomalyId == Guid.Empty)
        {
            throw new ArgumentException("Stock anomaly id can not be empty.", nameof(stockAnomalyId));
        }

        Id = id;
        StockAnomalyId = stockAnomalyId;
        EventType = eventType;
        Status = status;
        Message = string.IsNullOrWhiteSpace(message) ? "Anomali hareketi." : message.Trim();
        ChangedByUserId = changedByUserId;
        OccurredAtUtc = occurredAtUtc.Kind == DateTimeKind.Utc ? occurredAtUtc : occurredAtUtc.ToUniversalTime();
    }
}
