namespace FurpaMerkezApi.Domain.Entities;

public sealed class AppUser
{
    private AppUser()
    {
        Username = string.Empty;
        NormalizedUsername = string.Empty;
        Email = string.Empty;
        NormalizedEmail = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        WarehouseNo = string.Empty;
        WarehouseName = string.Empty;
        PasswordHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; }

    public string NormalizedUsername { get; private set; }

    public string Email { get; private set; }

    public string NormalizedEmail { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string WarehouseNo { get; private set; }

    public string WarehouseName { get; private set; }

    public string PasswordHash { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public List<AppUserRole> UserRoles { get; private set; } = [];

    public AppUser(
        Guid id,
        string username,
        string email,
        string firstName,
        string lastName,
        string warehouseNo,
        string warehouseName,
        string passwordHash,
        bool isActive,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User id can not be empty.", nameof(id));
        }

        Id = id;
        Username = NormalizeRequired(username, nameof(username), 50);
        NormalizedUsername = NormalizeLookupValue(Username);
        Email = NormalizeEmail(email);
        NormalizedEmail = NormalizeLookupValue(Email);
        FirstName = NormalizeRequired(firstName, nameof(firstName), 100);
        LastName = NormalizeRequired(lastName, nameof(lastName), 100);
        WarehouseNo = NormalizeRequired(warehouseNo, nameof(warehouseNo), 50);
        WarehouseName = NormalizeRequired(warehouseName, nameof(warehouseName), 150);
        PasswordHash = NormalizeRequired(passwordHash, nameof(passwordHash), 500);
        IsActive = isActive;
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    }

    public void UpdateProfile(
        string email,
        string firstName,
        string lastName,
        string warehouseNo,
        string warehouseName,
        bool isActive,
        DateTime updatedAtUtc)
    {
        Email = NormalizeEmail(email);
        NormalizedEmail = NormalizeLookupValue(Email);
        FirstName = NormalizeRequired(firstName, nameof(firstName), 100);
        LastName = NormalizeRequired(lastName, nameof(lastName), 100);
        WarehouseNo = NormalizeRequired(warehouseNo, nameof(warehouseNo), 50);
        WarehouseName = NormalizeRequired(warehouseName, nameof(warehouseName), 150);
        IsActive = isActive;
        UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
    }

    public void RenameUsername(string username, DateTime updatedAtUtc)
    {
        Username = NormalizeRequired(username, nameof(username), 50);
        NormalizedUsername = NormalizeLookupValue(Username);
        UpdatedAtUtc = DateTime.SpecifyKind(updatedAtUtc, DateTimeKind.Utc);
    }

    public void ChangePassword(string passwordHash, DateTime updatedAtUtc)
    {
        PasswordHash = NormalizeRequired(passwordHash, nameof(passwordHash), 500);
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

    private static string NormalizeEmail(string email)
    {
        var normalized = NormalizeRequired(email, nameof(email), 200);
        return normalized.ToLowerInvariant();
    }

    private static string NormalizeLookupValue(string value) => value.Trim().ToUpperInvariant();
}
