namespace FurpaMerkezApi.Domain.Entities;

public sealed class AppRolePermission
{
    private AppRolePermission()
    {
    }

    public Guid RoleId { get; private set; }

    public Guid PermissionId { get; private set; }

    public DateTime AssignedAtUtc { get; private set; }

    public AppRole Role { get; private set; } = null!;

    public AppPermission Permission { get; private set; } = null!;

    public AppRolePermission(Guid roleId, Guid permissionId, DateTime assignedAtUtc)
    {
        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role id can not be empty.", nameof(roleId));
        }

        if (permissionId == Guid.Empty)
        {
            throw new ArgumentException("Permission id can not be empty.", nameof(permissionId));
        }

        RoleId = roleId;
        PermissionId = permissionId;
        AssignedAtUtc = DateTime.SpecifyKind(assignedAtUtc, DateTimeKind.Utc);
    }
}
