using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.TrendyolGo;

public sealed class TrendyolGoPriceStockWorkbenchTests
{
    [Fact]
    public void Options_DefaultToTheDedicatedTrendyolPriceList()
    {
        var options = new TrendyolGoOptions();

        Assert.Equal(3, options.PriceListNo);
        Assert.Equal(0, options.PaymentPlanNo);
        Assert.Equal(300, options.PreviewRefreshIntervalSeconds);
    }

    [Fact]
    public void SnapshotCoordinator_DeduplicatesRefreshRequestsPerStore()
    {
        var coordinator = new TrendyolGoPriceStockSnapshotCoordinator();

        Assert.True(coordinator.RequestRefresh(486064));
        Assert.False(coordinator.RequestRefresh(486064));
        Assert.True(coordinator.TryDequeue(out var storeId));
        Assert.Equal(486064, storeId);
        Assert.Equal(SnapshotRefreshState.Queued, coordinator.GetState(storeId).RefreshState);

        coordinator.Complete(storeId);

        Assert.True(coordinator.RequestRefresh(storeId));
    }

    [Fact]
    public void SnapshotCoordinator_KeepsReadySnapshotWhenRefreshFails()
    {
        var coordinator = new TrendyolGoPriceStockSnapshotCoordinator();
        var generatedAtUtc = DateTime.UtcNow;
        var snapshot = new TrendyolGoPriceStockSnapshot(
            486064, 50, "Merkez Test Subesi", 1, 1, "HASH", generatedAtUtc,
            [new TrendyolGoPriceStockWorkbench.TrendyolProduct("8690000000000", "Product", 10m, 1)],
            [new("8690000000000", "015550", "Product", 10m, 1, 11m, 2, "Ready", null)]);

        coordinator.Publish(snapshot);
        coordinator.MarkFailed(486064, "Temporary upstream error.");

        var state = coordinator.GetState(486064);
        Assert.Same(snapshot, state.Snapshot);
        Assert.Equal(SnapshotRefreshState.Failed, state.RefreshState);
        Assert.Equal("Temporary upstream error.", state.RefreshError);
    }

    [Fact]
    public void BuildRow_UsesMikroPriceAndFlooredWarehouseQuantity()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 90m, 7),
            new("015550", "Mikro Product", 99.90m, 8.9m, false));

        Assert.Equal("Ready", row.Status);
        Assert.Equal(99.90m, row.MikroPrice);
        Assert.Equal(8, row.MikroQuantity);
        Assert.Equal("015550", row.StockCode);
    }

    [Fact]
    public void BuildRow_SkipsMissingProduct()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 10m, 3), null);

        Assert.Equal("Skipped", row.Status);
        Assert.Equal("Mikro barkod eslesmesi yok.", row.Reason);
    }

    [Fact]
    public void BuildRow_SkipsZeroPrice()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 10m, 3),
            new("015550", "Product", 0m, 4m, false));

        Assert.Equal("Skipped", row.Status);
        Assert.Equal("Mikro satis fiyati yok veya sifir.", row.Reason);
    }

    [Fact]
    public void BuildRow_TreatsNegativeStockAsZeroAndAvoidsUnchangedResend()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 99m, null),
            new("015550", "Product", 99m, -2m, false));

        Assert.Equal(0, row.MikroQuantity);
        Assert.Equal("Unchanged", row.Status);
    }
}
