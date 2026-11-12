using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoRuntimeSettingsProvider(
    AuthDbContext authDbContext,
    IOptionsMonitor<TrendyolGoOptions> options)
{
    public async Task<TrendyolGoRuntimeSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        var configured = options.CurrentValue;
        var runtime = await authDbContext.TrendyolGoRuntimeSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == Domain.Entities.TrendyolGoRuntimeSetting.SingletonId, cancellationToken);
        var overrides = await authDbContext.TrendyolGoStoreSettings
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var stores = configured.Stores
            .Where(item => item.StoreId > 0 && item.WarehouseNo > 0)
            .ToDictionary(
                item => item.StoreId,
                item => new TrendyolGoRuntimeStore(
                    item.StoreId,
                    item.WarehouseNo,
                    item.StoreName,
                    item.StockReserveQuantity,
                    "Configuration"));

        foreach (var storeOverride in overrides)
        {
            if (!storeOverride.IsActive)
            {
                stores.Remove(storeOverride.StoreId);
                continue;
            }

            stores[storeOverride.StoreId] = new TrendyolGoRuntimeStore(
                storeOverride.StoreId,
                storeOverride.WarehouseNo,
                storeOverride.StoreName,
                storeOverride.StockReserveQuantity,
                "Database");
        }

        return new TrendyolGoRuntimeSettingsSnapshot(
            runtime?.PriceListNo ?? configured.PriceListNo,
            runtime?.PaymentPlanNo ?? configured.PaymentPlanNo,
            runtime?.StockReserveQuantity ?? configured.StockReserveQuantity,
            runtime?.BranchPosPriceSyncEnabled ?? configured.BranchPosPriceSync.Enabled,
            stores.Values.OrderBy(item => item.WarehouseNo).ToArray(),
            runtime is null ? "Configuration" : "Database",
            runtime?.UpdatedBy,
            runtime?.UpdatedAtUtc);
    }
}

internal sealed record TrendyolGoRuntimeSettingsSnapshot(
    int PriceListNo,
    int PaymentPlanNo,
    int StockReserveQuantity,
    bool BranchPosPriceSyncEnabled,
    IReadOnlyCollection<TrendyolGoRuntimeStore> Stores,
    string Source,
    string? UpdatedBy,
    DateTime? UpdatedAtUtc)
{
    public TrendyolGoRuntimeStore RequireStore(long storeId) =>
        Stores.SingleOrDefault(item => item.StoreId == storeId)
        ?? throw new ArgumentException($"storeId {storeId} is not configured.");

    public int ResolveStockReserveQuantity(TrendyolGoRuntimeStore store)
    {
        var quantity = store.StockReserveQuantity ?? StockReserveQuantity;
        if (quantity is < 0 or > 100000)
        {
            throw new InvalidOperationException(
                $"Trendyol Go stock reserve for warehouse {store.WarehouseNo} must be between 0 and 100000.");
        }

        return quantity;
    }
}

internal sealed record TrendyolGoRuntimeStore(
    long StoreId,
    int WarehouseNo,
    string StoreName,
    int? StockReserveQuantity,
    string Source);
