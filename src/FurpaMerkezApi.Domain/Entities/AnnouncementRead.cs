namespace FurpaMerkezApi.Domain.Entities;

public sealed class AnnouncementRead
{
    private AnnouncementRead()
    {
    }

    public Guid AnnouncementId { get; private set; }

    public Announcement Announcement { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public AppUser User { get; private set; } = null!;

    public DateTime ReadAtUtc { get; private set; }

    public AnnouncementRead(Guid announcementId, Guid userId, DateTime readAtUtc)
    {
        if (announcementId == Guid.Empty)
        {
            throw new ArgumentException("Announcement id can not be empty.", nameof(announcementId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(userId));
        }

        AnnouncementId = announcementId;
        UserId = userId;
        ReadAtUtc = DateTime.SpecifyKind(readAtUtc, DateTimeKind.Utc);
    }
}
