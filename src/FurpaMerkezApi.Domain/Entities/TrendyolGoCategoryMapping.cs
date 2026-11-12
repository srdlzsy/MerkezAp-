namespace FurpaMerkezApi.Domain.Entities;

public sealed class TrendyolGoCategoryMapping
{
    private TrendyolGoCategoryMapping()
    {
        MappingKey = string.Empty;
        MikroCategoryCode = string.Empty;
        UpdatedBy = string.Empty;
    }

    public TrendyolGoCategoryMapping(
        Guid id,
        string mikroCategoryCode,
        string? mikroMainGroupCode,
        long categoryId,
        string updatedBy,
        DateTime updatedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Mapping id can not be empty.", nameof(id));
        Id = id;
        Update(mikroCategoryCode, mikroMainGroupCode, categoryId, updatedBy, updatedAtUtc);
        CreatedAtUtc = UpdatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string MappingKey { get; private set; } = string.Empty;
    public string MikroCategoryCode { get; private set; } = string.Empty;
    public string? MikroMainGroupCode { get; private set; }
    public long CategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string UpdatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        string mikroCategoryCode,
        string? mikroMainGroupCode,
        long categoryId,
        string updatedBy,
        DateTime updatedAtUtc)
    {
        MikroCategoryCode = RequireCode(mikroCategoryCode, nameof(mikroCategoryCode));
        MikroMainGroupCode = NormalizeOptionalCode(mikroMainGroupCode);
        if (categoryId <= 0) throw new ArgumentOutOfRangeException(nameof(categoryId));
        CategoryId = categoryId;
        MappingKey = BuildKey(MikroCategoryCode, MikroMainGroupCode);
        UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim();
        UpdatedAtUtc = NormalizeUtc(updatedAtUtc);
        IsActive = true;
    }

    public void Deactivate(string updatedBy, DateTime updatedAtUtc)
    {
        IsActive = false;
        UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim();
        UpdatedAtUtc = NormalizeUtc(updatedAtUtc);
    }

    public static string BuildKey(string categoryCode, string? mainGroupCode) =>
        $"{RequireCode(categoryCode, nameof(categoryCode)).ToUpperInvariant()}|{NormalizeOptionalCode(mainGroupCode)?.ToUpperInvariant() ?? string.Empty}";

    private static string RequireCode(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Mikro category code is required.", parameterName)
            : value.Trim();

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
