namespace FurpaMerkezApi.Domain.Entities;

public sealed class TrendyolGoStoreSetting
{
    private TrendyolGoStoreSetting()
    {
        StoreName = string.Empty;
        UpdatedBy = string.Empty;
    }

    public TrendyolGoStoreSetting(
        long storeId,
        int warehouseNo,
        string storeName,
        int? stockReserveQuantity,
        string updatedBy,
        DateTime updatedAtUtc)
    {
        StoreId = storeId;
        Update(warehouseNo, storeName, stockReserveQuantity, true, updatedBy, updatedAtUtc);
        CreatedAtUtc = UpdatedAtUtc;
    }

    public long StoreId { get; private set; }
    public int WarehouseNo { get; private set; }
    public string StoreName { get; private set; } = string.Empty;
    public int? StockReserveQuantity { get; private set; }
    public bool IsActive { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        int warehouseNo,
        string storeName,
        int? stockReserveQuantity,
        bool isActive,
        string updatedBy,
        DateTime updatedAtUtc)
    {
        if (StoreId <= 0) throw new ArgumentOutOfRangeException(nameof(StoreId));
        if (warehouseNo <= 0) throw new ArgumentOutOfRangeException(nameof(warehouseNo));
        if (string.IsNullOrWhiteSpace(storeName)) throw new ArgumentException("Store name is required.", nameof(storeName));
        if (stockReserveQuantity is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(stockReserveQuantity));
        WarehouseNo = warehouseNo;
        StoreName = storeName.Trim();
        StockReserveQuantity = stockReserveQuantity;
        IsActive = isActive;
        UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim();
        UpdatedAtUtc = updatedAtUtc.Kind == DateTimeKind.Utc ? updatedAtUtc : updatedAtUtc.ToUniversalTime();
    }
}
