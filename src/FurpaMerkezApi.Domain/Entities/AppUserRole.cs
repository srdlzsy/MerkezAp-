namespace FurpaMerkezApi.Domain.Entities;

public sealed class AppUserRole
{
    private AppUserRole()
    {
    }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public DateTime AssignedAtUtc { get; private set; }

    public AppUser User { get; private set; } = null!;

    public AppRole Role { get; private set; } = null!;

    public AppUserRole(Guid userId, Guid roleId, DateTime assignedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role id can not be empty.", nameof(roleId));
        }

        UserId = userId;
        RoleId = roleId;
        AssignedAtUtc = DateTime.SpecifyKind(assignedAtUtc, DateTimeKind.Utc);
    }
}
