namespace FurpaMerkezApi.Domain.Entities;

public sealed class DocumentFlowEvent
{
    private DocumentFlowEvent()
    {
        Message = string.Empty;
    }

    internal DocumentFlowEvent(
        Guid id,
        Guid documentFlowId,
        DocumentFlowStep step,
        DocumentFlowStatus status,
        string message,
        string? error,
        Guid? changedByUserId,
        DateTime occurredAtUtc)
    {
        Id = id;
        DocumentFlowId = documentFlowId;
        Step = step;
        Status = status;
        var normalizedMessage = string.IsNullOrWhiteSpace(message) ? step.ToString() : message.Trim();
        Message = normalizedMessage.Length <= 500 ? normalizedMessage : normalizedMessage[..500];
        Error = string.IsNullOrWhiteSpace(error)
            ? null
            : error.Trim().Length <= 2000 ? error.Trim() : error.Trim()[..2000];
        ChangedByUserId = changedByUserId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid DocumentFlowId { get; private set; }

    public DocumentFlowStep Step { get; private set; }

    public DocumentFlowStatus Status { get; private set; }

    public string Message { get; private set; }

    public string? Error { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }
}
