namespace FurpaMerkezApi.Domain.Entities;

public sealed class AnnouncementTarget
{
    private AnnouncementTarget()
    {
        WarehouseName = string.Empty;
        Username = string.Empty;
        UserFullName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid AnnouncementId { get; private set; }

    public Announcement Announcement { get; private set; } = null!;

    public AnnouncementTargetType Type { get; private set; }

    public int? WarehouseNo { get; private set; }

    public string? WarehouseName { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Username { get; private set; }

    public string? UserFullName { get; private set; }

    public AnnouncementTarget(
        Guid id,
        Guid announcementId,
        AnnouncementTargetType type,
        int? warehouseNo,
        string? warehouseName,
        Guid? userId,
        string? username,
        string? userFullName)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Announcement target id can not be empty.", nameof(id));
        }

        if (announcementId == Guid.Empty)
        {
            throw new ArgumentException("Announcement id can not be empty.", nameof(announcementId));
        }

        ValidateTarget(type, warehouseNo, userId);

        Id = id;
        AnnouncementId = announcementId;
        Type = type;
        WarehouseNo = warehouseNo;
        WarehouseName = NormalizeOptional(warehouseName, 150);
        UserId = userId;
        Username = NormalizeOptional(username, 50);
        UserFullName = NormalizeOptional(userFullName, 201);
    }

    private static void ValidateTarget(AnnouncementTargetType type, int? warehouseNo, Guid? userId)
    {
        switch (type)
        {
            case AnnouncementTargetType.AllWarehouses:
                if (warehouseNo.HasValue || userId.HasValue)
                {
                    throw new ArgumentException("All warehouses target can not include warehouse or user values.");
                }

                return;
            case AnnouncementTargetType.Warehouse:
                if (!warehouseNo.HasValue || warehouseNo.Value <= 0)
                {
                    throw new ArgumentException("Warehouse target requires a valid warehouse no.", nameof(warehouseNo));
                }

                if (userId.HasValue)
                {
                    throw new ArgumentException("Warehouse target can not include user value.", nameof(userId));
                }

                return;
            case AnnouncementTargetType.User:
                if (!userId.HasValue || userId.Value == Guid.Empty)
                {
                    throw new ArgumentException("User target requires a valid user id.", nameof(userId));
                }

                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Announcement target type is invalid.");
        }
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

public enum AnnouncementTargetType
{
    AllWarehouses = 1,
    Warehouse = 2,
    User = 3
}
