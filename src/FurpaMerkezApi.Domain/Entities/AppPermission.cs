namespace FurpaMerkezApi.Domain.Entities;

public sealed class AppPermission
{
    private AppPermission()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public List<AppRolePermission> RolePermissions { get; private set; } = [];

    public AppPermission(Guid id, string code, string name, string? description, DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Permission id can not be empty.", nameof(id));
        }

        Id = id;
        Code = NormalizeRequired(code, nameof(code), 100).ToLowerInvariant();
        Name = NormalizeRequired(name, nameof(name), 100);
        Description = NormalizeOptional(description, 250);
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    }

    public void Update(string code, string name, string? description, DateTime updatedAtUtc)
    {
        Code = NormalizeRequired(code, nameof(code), 100).ToLowerInvariant();
        Name = NormalizeRequired(name, nameof(name), 100);
        Description = NormalizeOptional(description, 250);
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
