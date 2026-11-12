using System.Text.Json;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.TrendyolGo;

public sealed class TrendyolGoManagementTests
{
    [Fact]
    public async Task RuntimeSettings_DatabaseValuesOverrideConfigurationAndInactiveStoreIsHidden()
    {
        await using var db = CreateDbContext();
        var now = new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);
        db.TrendyolGoRuntimeSettings.Add(new TrendyolGoRuntimeSetting(5, 7, 4, false, "tester", now));
        var inactiveStore = new TrendyolGoStoreSetting(1002, 102, "Inactive Store", null, "tester", now);
        inactiveStore.Update(102, "Inactive Store", null, false, "tester", now);
        db.TrendyolGoStoreSettings.AddRange(
            new TrendyolGoStoreSetting(1001, 150, "Database Store", 8, "tester", now),
            inactiveStore,
            new TrendyolGoStoreSetting(1003, 103, "New Store", null, "tester", now));
        await db.SaveChangesAsync();

        var provider = new TrendyolGoRuntimeSettingsProvider(
            db,
            new StaticOptionsMonitor<TrendyolGoOptions>(new TrendyolGoOptions
            {
                PriceListNo = 3,
                PaymentPlanNo = 0,
                StockReserveQuantity = 2,
                BranchPosPriceSync = new TrendyolGoBranchPosPriceSyncOptions { Enabled = true },
                Stores =
                [
                    new TrendyolGoStoreMappingOptions
                    {
                        StoreId = 1001, WarehouseNo = 101, StoreName = "Configured Store"
                    },
                    new TrendyolGoStoreMappingOptions
                    {
                        StoreId = 1002, WarehouseNo = 102, StoreName = "Configured Inactive Store"
                    }
                ]
            }));

        var result = await provider.GetAsync(CancellationToken.None);

        Assert.Equal(5, result.PriceListNo);
        Assert.Equal(7, result.PaymentPlanNo);
        Assert.Equal(4, result.StockReserveQuantity);
        Assert.False(result.BranchPosPriceSyncEnabled);
        Assert.Equal("Database", result.Source);
        Assert.Collection(
            result.Stores.OrderBy(item => item.StoreId),
            store =>
            {
                Assert.Equal(1001, store.StoreId);
                Assert.Equal(150, store.WarehouseNo);
                Assert.Equal(8, store.StockReserveQuantity);
                Assert.Equal("Database", store.Source);
            },
            store =>
            {
                Assert.Equal(1003, store.StoreId);
                Assert.Equal(103, store.WarehouseNo);
                Assert.Null(store.StockReserveQuantity);
            });
    }

    [Fact]
    public void ParseCategories_ReadsNestedCategoryTreeAndBuildsFullPath()
    {
        using var json = JsonDocument.Parse("""
            {
              "categories": [
                {
                  "id": 1410,
                  "name": "Meyve & Sebze",
                  "leaf": false,
                  "subCategories": [
                    { "id": 1469, "name": "Meyve", "leaf": true },
                    { "id": 1470, "name": "Sebze", "leaf": true }
                  ]
                }
              ]
            }
            """);

        var result = TrendyolGoAdministrationService.ParseCategories(json.RootElement);

        Assert.Equal(3, result.Count);
        var vegetable = Assert.Single(result, item => item.CategoryId == 1470);
        Assert.Equal(1410, vegetable.ParentCategoryId);
        Assert.Equal("Meyve & Sebze > Sebze", vegetable.Path);
        Assert.True(vegetable.IsLeaf);
    }

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"trendyol-go-management-{Guid.NewGuid():N}")
            .Options;
        return new AuthDbContext(options);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
