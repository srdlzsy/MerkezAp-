namespace FurpaMerkezApi.Domain.Entities;

public sealed class AppUserClientRole
{
    private AppUserClientRole()
    {
        ClientType = string.Empty;
    }

    public Guid UserId { get; private set; }

    public string ClientType { get; private set; }

    public Guid RoleId { get; private set; }

    public DateTime AssignedAtUtc { get; private set; }

    public AppUser User { get; private set; } = null!;

    public AppRole Role { get; private set; } = null!;

    public AppUserClientRole(Guid userId, string clientType, Guid roleId, DateTime assignedAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(userId));
        }

        if (roleId == Guid.Empty)
        {
            throw new ArgumentException("Role id can not be empty.", nameof(roleId));
        }

        if (string.IsNullOrWhiteSpace(clientType))
        {
            throw new ArgumentException("Client type is required.", nameof(clientType));
        }

        var normalizedClientType = clientType.Trim().ToLowerInvariant();
        if (normalizedClientType.Length > 20)
        {
            throw new ArgumentException("Client type can not exceed 20 characters.", nameof(clientType));
        }

        UserId = userId;
        ClientType = normalizedClientType;
        RoleId = roleId;
        AssignedAtUtc = DateTime.SpecifyKind(assignedAtUtc, DateTimeKind.Utc);
    }
}
