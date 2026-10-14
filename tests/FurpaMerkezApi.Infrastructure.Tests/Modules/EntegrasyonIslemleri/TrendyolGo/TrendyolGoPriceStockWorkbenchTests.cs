using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
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

    [Fact]
    public void SelectRows_ChangedModeOnlyIncludesDifferences()
    {
        var rows = CreateDispatchRows();

        var selected = TrendyolGoPriceStockWorkbench.SelectRows(
            rows, TrendyolGoPriceStockDispatchMode.Changed, []);

        Assert.Single(selected);
        Assert.Equal("ready", selected[0].Barcode);
    }

    [Fact]
    public void SelectRows_AllExistingModeIncludesChangedAndUnchangedProducts()
    {
        var rows = CreateDispatchRows();

        var selected = TrendyolGoPriceStockWorkbench.SelectRows(
            rows, TrendyolGoPriceStockDispatchMode.AllExisting, []);

        Assert.Equal(2, selected.Length);
        Assert.Contains(selected, item => item.Barcode == "ready");
        Assert.Contains(selected, item => item.Barcode == "unchanged");
        Assert.DoesNotContain(selected, item => item.Barcode == "skipped");
    }

    [Fact]
    public void CatalogBuildRow_OnlyMarksFullyMappedNewProductReady()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "015550", "Product", "8690000000000", "MARKA", "KATEGORI", 10, 25m, false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product, new HashSet<string>(), new HashSet<string>(),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["MARKA"] = 100 },
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["KATEGORI"] = 200 });

        Assert.Equal("Ready", row.Status);
        Assert.Equal(100, row.TrendyolBrandId);
        Assert.Equal(200, row.TrendyolCategoryId);
    }

    [Fact]
    public void CatalogBuildRow_RequiresMissingCategoryMapping()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "015550", "Product", "8690000000000", "MARKA", "KATEGORI", 10, 25m, false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product, new HashSet<string>(), new HashSet<string>(),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["MARKA"] = 100 },
            new Dictionary<string, long>());

        Assert.Equal("MappingRequired", row.Status);
        Assert.Equal("Trendyol kategori eslemesi gerekli.", row.Reason);
    }

    [Fact]
    public void CatalogBuildRow_DoesNotOfferExistingProductForCreation()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "015550", "Product", "8690000000000", null, null, 10, 25m, false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product, new HashSet<string> { "8690000000000" }, new HashSet<string>(),
            new Dictionary<string, long>(), new Dictionary<string, long>());

        Assert.Equal("Existing", row.Status);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(18)]
    public void CatalogBuildRow_SkipsVatRatesNotAcceptedByTrendyol(int vatRate)
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "015550", "Product", "8690000000000", "MARKA", "KATEGORI", vatRate, 25m, false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product, new HashSet<string>(), new HashSet<string>(),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["MARKA"] = 100 },
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["KATEGORI"] = 200 });

        Assert.Equal("Skipped", row.Status);
        Assert.Contains("0, 1, 10 veya 20", row.Reason);
    }

    [Fact]
    public void CatalogBuildRow_SkipsUnsupportedBarcodeCharacters()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "015550", "Product", "8690 000", "MARKA", "KATEGORI", 10, 25m, false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product, new HashSet<string>(), new HashSet<string>(),
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["MARKA"] = 100 },
            new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["KATEGORI"] = 200 });

        Assert.Equal("Skipped", row.Status);
        Assert.Contains("karakterler", row.Reason);
    }

    [Fact]
    public void CatalogSelectRows_SendAllOnlyIncludesReadyProducts()
    {
        TrendyolGoCatalogRow[] rows =
        [
            new("001", "Ready", "1", "B", 10, "C", 20, 10, 25m, "Ready", null),
            new("002", "Existing", "2", "B", 10, "C", 20, 10, 25m, "Existing", null),
            new("003", "Needs mapping", "3", "B", null, "C", null, 10, 25m, "MappingRequired", "Missing")
        ];

        var selected = TrendyolGoCatalogWorkbench.SelectRows(rows, true, []);

        Assert.Single(selected);
        Assert.Equal("1", selected[0].Barcode);
    }

    [Fact]
    public void CatalogTitle_RemovesUnsupportedCharactersAndExtraWhitespace()
    {
        var title = TrendyolGoCatalogWorkbench.NormalizeTitle("URUN / TEST &  100%_YENI");

        Assert.Equal("URUN TEST 100 YENI", title);
    }

    [Fact]
    public void BranchPosVersion_UsesPersistedTaskTimeWhenSourceTimeIsMissing()
    {
        var taskCreatedAtUtc = new DateTime(2026, 10, 14, 9, 30, 0, DateTimeKind.Utc);

        var result = TrendyolGoBranchPosPriceSyncService.ResolveSourceVersion(null, taskCreatedAtUtc);

        Assert.Equal(taskCreatedAtUtc, result);
    }

    [Fact]
    public void BranchPosUpsert_OnlyAppliesStrictlyNewerPriceAndWarehouseVersions()
    {
        Assert.Contains(
            "@sfiyat_lastup_date > sfiyat_lastup_date",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "@sdp_lastup_date > sdp_lastup_date",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WHERE NOT EXISTS (SELECT 1 FROM upd)",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
    }

    private static TrendyolGoPriceStockRow[] CreateDispatchRows() =>
    [
        new("ready", "001", "Ready Product", 10m, 1, 11m, 2, "Ready", null),
        new("unchanged", "002", "Unchanged Product", 20m, 3, 20m, 3, "Unchanged", null),
        new("skipped", null, "Skipped Product", null, null, null, null, "Skipped", "Missing")
    ];
}
