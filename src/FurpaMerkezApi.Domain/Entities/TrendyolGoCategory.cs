namespace FurpaMerkezApi.Domain.Entities;

public sealed class TrendyolGoCategory
{
    private TrendyolGoCategory()
    {
        Name = string.Empty;
        Path = string.Empty;
    }

    public TrendyolGoCategory(
        long categoryId,
        string name,
        long? parentCategoryId,
        string path,
        bool isLeaf,
        bool isActive,
        DateTime syncedAtUtc)
    {
        if (categoryId <= 0) throw new ArgumentOutOfRangeException(nameof(categoryId));
        CategoryId = categoryId;
        Update(name, parentCategoryId, path, isLeaf, isActive, syncedAtUtc);
    }

    public long CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public long? ParentCategoryId { get; private set; }
    public string Path { get; private set; } = string.Empty;
    public bool IsLeaf { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime SyncedAtUtc { get; private set; }

    public void Update(
        string name,
        long? parentCategoryId,
        string path,
        bool isLeaf,
        bool isActive,
        DateTime syncedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Category name is required.", nameof(name));
        Name = name.Trim();
        ParentCategoryId = parentCategoryId;
        Path = string.IsNullOrWhiteSpace(path) ? Name : path.Trim();
        IsLeaf = isLeaf;
        IsActive = isActive;
        SyncedAtUtc = NormalizeUtc(syncedAtUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
