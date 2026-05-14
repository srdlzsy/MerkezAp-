namespace FurpaMerkezApi.Domain.Entities;

public sealed class AppRole
{
    private AppRole()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public List<AppUserRole> UserRoles { get; private set; } = [];

    public List<AppRolePermission> RolePermissions { get; private set; } = [];

    public AppRole(Guid id, string name, string? description, bool isActive, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Role id can not be empty.", nameof(id));
        }

        Id = id;
        Name = NormalizeRequired(name, nameof(name), 100);
        Description = NormalizeOptional(description, 250);
        IsActive = isActive;
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    }

    public void Update(string name, string? description, bool isActive, DateTime updatedAtUtc)
    {
        Name = NormalizeRequired(name, nameof(name), 100);
        Description = NormalizeOptional(description, 250);
        IsActive = isActive;
        UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
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

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"description can not exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
