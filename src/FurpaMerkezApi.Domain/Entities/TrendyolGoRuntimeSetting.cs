namespace FurpaMerkezApi.Domain.Entities;

public sealed class TrendyolGoRuntimeSetting
{
    public const int SingletonId = 1;

    private TrendyolGoRuntimeSetting()
    {
        UpdatedBy = string.Empty;
    }

    public TrendyolGoRuntimeSetting(
        int priceListNo,
        int paymentPlanNo,
        int stockReserveQuantity,
        bool branchPosPriceSyncEnabled,
        string updatedBy,
        DateTime updatedAtUtc)
    {
        Id = SingletonId;
        Update(priceListNo, paymentPlanNo, stockReserveQuantity, branchPosPriceSyncEnabled, updatedBy, updatedAtUtc);
    }

    public int Id { get; private set; }
    public int PriceListNo { get; private set; }
    public int PaymentPlanNo { get; private set; }
    public int StockReserveQuantity { get; private set; }
    public bool BranchPosPriceSyncEnabled { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        int priceListNo,
        int paymentPlanNo,
        int stockReserveQuantity,
        bool branchPosPriceSyncEnabled,
        string updatedBy,
        DateTime updatedAtUtc)
    {
        if (priceListNo is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(priceListNo));
        if (paymentPlanNo is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(paymentPlanNo));
        if (stockReserveQuantity is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(stockReserveQuantity));
        PriceListNo = priceListNo;
        PaymentPlanNo = paymentPlanNo;
        StockReserveQuantity = stockReserveQuantity;
        BranchPosPriceSyncEnabled = branchPosPriceSyncEnabled;
        UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? "system" : updatedBy.Trim();
        UpdatedAtUtc = updatedAtUtc.Kind == DateTimeKind.Utc ? updatedAtUtc : updatedAtUtc.ToUniversalTime();
    }
}
