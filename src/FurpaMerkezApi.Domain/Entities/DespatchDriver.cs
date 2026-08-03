namespace FurpaMerkezApi.Domain.Entities;

public sealed class DespatchDriver
{
    private DespatchDriver()
    {
        FirstName = string.Empty;
        LastName = string.Empty;
        PlateNumber = string.Empty;
        Tckn = string.Empty;
    }

    public Guid Id { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public string PlateNumber { get; private set; } = string.Empty;

    public string Tckn { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public DateTime? UpdatedAtUtc { get; private set; }

    public DespatchDriver(
        Guid id,
        string firstName,
        string lastName,
        string plateNumber,
        string tckn,
        Guid createdByUserId,
        DateTime createdAtUtc,
        bool isActive = true,
        string? notes = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Despatch driver id can not be empty.", nameof(id));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("Created by user id can not be empty.", nameof(createdByUserId));
        }

        Id = id;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = NormalizeUtc(createdAtUtc);
        UpdateCore(firstName, lastName, plateNumber, tckn, isActive, notes);
    }

    public void Update(
        string firstName,
        string lastName,
        string plateNumber,
        string tckn,
        bool isActive,
        string? notes,
        Guid updatedByUserId,
        DateTime updatedAtUtc)
    {
        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Updated by user id can not be empty.", nameof(updatedByUserId));
        }

        UpdateCore(firstName, lastName, plateNumber, tckn, isActive, notes);
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = NormalizeUtc(updatedAtUtc);
    }

    public void Deactivate(Guid updatedByUserId, DateTime updatedAtUtc)
    {
        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Updated by user id can not be empty.", nameof(updatedByUserId));
        }

        IsActive = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = NormalizeUtc(updatedAtUtc);
    }

    private void UpdateCore(
        string firstName,
        string lastName,
        string plateNumber,
        string tckn,
        bool isActive,
        string? notes)
    {
        FirstName = NormalizeRequired(firstName, nameof(firstName), 60);
        LastName = NormalizeRequired(lastName, nameof(lastName), 60);
        PlateNumber = NormalizePlate(plateNumber);
        Tckn = NormalizeTckn(tckn);
        IsActive = isActive;
        Notes = NormalizeOptional(notes, 1000);
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

    private static string NormalizePlate(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value), 20).ToUpperInvariant();

        if (normalized.Length > 20)
        {
            throw new ArgumentException("Plate number can not exceed 20 characters.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeTckn(string value)
    {
        var normalized = NormalizeRequired(value, nameof(value), 11);

        if (normalized.Length != 11 || normalized.Any(character => !char.IsDigit(character)))
        {
            throw new ArgumentException("TCKN must be 11 digits.", nameof(value));
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
            throw new ArgumentException($"Value can not exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
