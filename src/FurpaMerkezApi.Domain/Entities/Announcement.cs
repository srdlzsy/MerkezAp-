namespace FurpaMerkezApi.Domain.Entities;

public sealed class Announcement
{
    private Announcement()
    {
        Title = string.Empty;
        Message = string.Empty;
        CreatedByUsername = string.Empty;
        CreatedByFullName = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public AnnouncementPriority Priority { get; private set; }

    public AnnouncementStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string CreatedByUsername { get; private set; }

    public string CreatedByFullName { get; private set; }

    public DateTime? StartsAtUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public DateTime PublishedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public Guid? ArchivedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public List<AnnouncementTarget> Targets { get; private set; } = [];

    public List<AnnouncementRead> Reads { get; private set; } = [];

    public Announcement(
        Guid id,
        string title,
        string message,
        AnnouncementPriority priority,
        Guid createdByUserId,
        string createdByUsername,
        string createdByFullName,
        DateTime? startsAtUtc,
        DateTime? expiresAtUtc,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Announcement id can not be empty.", nameof(id));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(createdByUserId));
        }

        ValidateDateRange(startsAtUtc, expiresAtUtc);

        Id = id;
        Title = NormalizeRequired(title, nameof(title), 140);
        Message = NormalizeRequired(message, nameof(message), 4000);
        Priority = priority;
        Status = AnnouncementStatus.Published;
        CreatedByUserId = createdByUserId;
        CreatedByUsername = NormalizeRequired(createdByUsername, nameof(createdByUsername), 50);
        CreatedByFullName = NormalizeRequired(createdByFullName, nameof(createdByFullName), 201);
        StartsAtUtc = NormalizeUtc(startsAtUtc);
        ExpiresAtUtc = NormalizeUtc(expiresAtUtc);
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
        PublishedAtUtc = CreatedAtUtc;
    }

    public void Update(
        string title,
        string message,
        AnnouncementPriority priority,
        DateTime? startsAtUtc,
        DateTime? expiresAtUtc,
        DateTime updatedAtUtc)
    {
        if (Status == AnnouncementStatus.Archived)
        {
            throw new InvalidOperationException("Archived announcement can not be updated.");
        }

        ValidateDateRange(startsAtUtc, expiresAtUtc);

        Title = NormalizeRequired(title, nameof(title), 140);
        Message = NormalizeRequired(message, nameof(message), 4000);
        Priority = priority;
        StartsAtUtc = NormalizeUtc(startsAtUtc);
        ExpiresAtUtc = NormalizeUtc(expiresAtUtc);
        UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
    }

    public void Archive(Guid archivedByUserId, DateTime archivedAtUtc)
    {
        if (archivedByUserId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(archivedByUserId));
        }

        if (Status == AnnouncementStatus.Archived)
        {
            return;
        }

        var normalizedArchivedAt = DateTime.SpecifyKind(archivedAtUtc, DateTimeKind.Utc);
        Status = AnnouncementStatus.Archived;
        ArchivedAtUtc = normalizedArchivedAt;
        ArchivedByUserId = archivedByUserId;
        UpdatedAtUtc = normalizedArchivedAt;
    }

    private static void ValidateDateRange(DateTime? startsAtUtc, DateTime? expiresAtUtc)
    {
        var normalizedStartsAtUtc = NormalizeUtc(startsAtUtc);
        var normalizedExpiresAtUtc = NormalizeUtc(expiresAtUtc);

        if (normalizedStartsAtUtc.HasValue &&
            normalizedExpiresAtUtc.HasValue &&
            normalizedExpiresAtUtc.Value <= normalizedStartsAtUtc.Value)
        {
            throw new ArgumentException("Expires at must be later than starts at.", nameof(expiresAtUtc));
        }
    }

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

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;
}

public enum AnnouncementPriority
{
    Normal = 1,
    Important = 2,
    Urgent = 3
}

public enum AnnouncementStatus
{
    Published = 1,
    Archived = 2
}
