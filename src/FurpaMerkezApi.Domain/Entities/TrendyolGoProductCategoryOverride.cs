namespace FurpaMerkezApi.Domain.Entities;

public sealed class TrendyolGoProductCategoryOverride
{
    private TrendyolGoProductCategoryOverride()
    {
        StockCode = string.Empty;
        UpdatedBy = string.Empty;
    }

    public TrendyolGoProductCategoryOverride(
        Guid id,
        string stockCode,
        long categoryId,
        string updatedBy,
        DateTime updatedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Override id can not be empty.", nameof(id));
        Id = id;
        Update(stockCode, categoryId, updatedBy, updatedAtUtc);
        CreatedAtUtc = UpdatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string StockCode { get; private set; } = string.Empty;
    public long CategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string UpdatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string stockCode, long categoryId, string updatedBy, DateTime updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(stockCode)) throw new ArgumentException("Stock code is required.", nameof(stockCode));
        if (categoryId <= 0) throw new ArgumentOutOfRangeException(nameof(categoryId));
        StockCode = stockCode.Trim();
        CategoryId = categoryId;
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

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
