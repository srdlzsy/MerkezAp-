namespace FurpaMerkezApi.Domain.Entities;

public sealed class SafeWriteReviewAudit
{
    private SafeWriteReviewAudit()
    {
        Action = string.Empty;
        Outcome = string.Empty;
        Reason = string.Empty;
        PreviousStatus = string.Empty;
        ResultStatus = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid SafeWriteId { get; private set; }
    public string Action { get; private set; }
    public string Outcome { get; private set; }
    public string Reason { get; private set; }
    public Guid ReviewedByUserId { get; private set; }
    public DateTime ReviewedAtUtc { get; private set; }
    public string PreviousStatus { get; private set; }
    public string? PreviousErrorCode { get; private set; }
    public string ResultStatus { get; private set; }
    public string? ResultErrorCode { get; private set; }
    public string? Message { get; private set; }

    public SafeWriteReviewAudit(
        Guid id,
        Guid safeWriteId,
        string action,
        string outcome,
        string reason,
        Guid reviewedByUserId,
        DateTime reviewedAtUtc,
        string previousStatus,
        string? previousErrorCode,
        string resultStatus,
        string? resultErrorCode,
        string? message)
    {
        Id = RequiredGuid(id, nameof(id));
        SafeWriteId = RequiredGuid(safeWriteId, nameof(safeWriteId));
        Action = Required(action, nameof(action), 30);
        Outcome = Required(outcome, nameof(outcome), 40);
        Reason = Required(reason, nameof(reason), 500);
        ReviewedByUserId = RequiredGuid(reviewedByUserId, nameof(reviewedByUserId));
        ReviewedAtUtc = NormalizeUtc(reviewedAtUtc);
        PreviousStatus = Required(previousStatus, nameof(previousStatus), 20);
        PreviousErrorCode = Optional(previousErrorCode, 100);
        ResultStatus = Required(resultStatus, nameof(resultStatus), 20);
        ResultErrorCode = Optional(resultErrorCode, 100);
        Message = Optional(message, 1000);
    }

    private static Guid RequiredGuid(Guid value, string name) =>
        value == Guid.Empty ? throw new ArgumentException($"{name} is required.", name) : value;

    private static string Required(string value, string name, int maxLength) =>
        Optional(value, maxLength) ?? throw new ArgumentException($"{name} is required.", name);

    private static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
