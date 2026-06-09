namespace FurpaMerkezApi.Domain.Entities;

public sealed class FeedbackItem
{
    private FeedbackItem()
    {
        Title = string.Empty;
        Message = string.Empty;
        CreatedByUsername = string.Empty;
        CreatedByFullName = string.Empty;
        WarehouseName = string.Empty;
    }

    public Guid Id { get; private set; }

    public FeedbackItemType Type { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public FeedbackItemStatus Status { get; private set; }

    public FeedbackItemPriority Priority { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string CreatedByUsername { get; private set; }

    public string CreatedByFullName { get; private set; }

    public int WarehouseNo { get; private set; }

    public string WarehouseName { get; private set; }

    public string? AdminNote { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    public Guid? ReadByUserId { get; private set; }

    public DateTime? StatusChangedAtUtc { get; private set; }

    public Guid? StatusChangedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public DateTime? ClosedAtUtc { get; private set; }

    public FeedbackItem(
        Guid id,
        FeedbackItemType type,
        string title,
        string message,
        FeedbackItemPriority priority,
        Guid createdByUserId,
        string createdByUsername,
        string createdByFullName,
        int warehouseNo,
        string warehouseName,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Feedback id can not be empty.", nameof(id));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(createdByUserId));
        }

        if (warehouseNo < 0)
        {
            throw new ArgumentException("Warehouse no can not be negative.", nameof(warehouseNo));
        }

        Id = id;
        Type = type;
        Title = NormalizeRequired(title, nameof(title), 120);
        Message = NormalizeRequired(message, nameof(message), 2000);
        Priority = priority;
        Status = FeedbackItemStatus.New;
        CreatedByUserId = createdByUserId;
        CreatedByUsername = NormalizeRequired(createdByUsername, nameof(createdByUsername), 50);
        CreatedByFullName = NormalizeRequired(createdByFullName, nameof(createdByFullName), 201);
        WarehouseNo = warehouseNo;
        WarehouseName = NormalizeRequired(warehouseName, nameof(warehouseName), 150);
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    }

    public void MarkAsRead(Guid readByUserId, DateTime readAtUtc)
    {
        if (readByUserId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(readByUserId));
        }

        var normalizedReadAt = DateTime.SpecifyKind(readAtUtc, DateTimeKind.Utc);
        ReadAtUtc ??= normalizedReadAt;
        ReadByUserId ??= readByUserId;

        if (Status == FeedbackItemStatus.New)
        {
            Status = FeedbackItemStatus.Read;
            StatusChangedAtUtc = normalizedReadAt;
            StatusChangedByUserId = readByUserId;
        }

        UpdatedAtUtc = normalizedReadAt;
    }

    public void ChangeStatus(
        FeedbackItemStatus status,
        string? adminNote,
        Guid changedByUserId,
        DateTime changedAtUtc)
    {
        if (changedByUserId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(changedByUserId));
        }

        var normalizedChangedAt = DateTime.SpecifyKind(changedAtUtc, DateTimeKind.Utc);
        Status = status;
        AdminNote = NormalizeOptional(adminNote, 1000);
        StatusChangedAtUtc = normalizedChangedAt;
        StatusChangedByUserId = changedByUserId;
        UpdatedAtUtc = normalizedChangedAt;

        if (ReadAtUtc is null && status != FeedbackItemStatus.New)
        {
            ReadAtUtc = normalizedChangedAt;
            ReadByUserId = changedByUserId;
        }

        ClosedAtUtc = IsFinalStatus(status) ? normalizedChangedAt : null;
    }

    private static bool IsFinalStatus(FeedbackItemStatus status) =>
        status is FeedbackItemStatus.Resolved or FeedbackItemStatus.Closed or FeedbackItemStatus.Rejected;

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} can not exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value can not exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}

public enum FeedbackItemType
{
    Complaint = 1,
    Suggestion = 2
}

public enum FeedbackItemStatus
{
    New = 1,
    Read = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5,
    Rejected = 6
}

public enum FeedbackItemPriority
{
    Low = 1,
    Normal = 2,
    High = 3
}
