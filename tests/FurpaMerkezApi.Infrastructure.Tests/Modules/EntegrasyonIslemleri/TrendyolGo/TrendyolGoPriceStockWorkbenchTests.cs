using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Domain.Entities;
using System.Text.Json;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.TrendyolGo;

public sealed class TrendyolGoPriceStockWorkbenchTests
{
    [Fact]
    public void ReadProduct_PreservesTrendyolStockCodeForCatalogReconciliation()
    {
        using var document = JsonDocument.Parse("""
            {
              "barcode": "8690793020075",
              "stockCode": "007921",
              "title": "OMO MATIK",
              "sellingPrice": 199.5,
              "quantity": 10
            }
            """);

        var product = TrendyolGoPriceStockWorkbench.ReadProduct(document.RootElement);

        Assert.Equal("007921", product.StockCode);
    }

    [Fact]
    public void BatchResultParser_ExtractsSuccessfulAndFailedBarcodes()
    {
        using var document = JsonDocument.Parse("""
            {
              "status": "COMPLETED",
              "itemCount": 2,
              "failedItemCount": 1,
              "items": [
                { "status": "SUCCESS", "requestItem": { "barcode": "success" } },
                {
                  "status": "FAILED",
                  "requestItem": { "product": { "barcode": "failed" } },
                  "failureReasons": ["Catalog missing"]
                }
              ]
            }
            """);

        var result = TrendyolGoBatchResultParser.Parse(document.RootElement);

        Assert.True(result.IsCompleted);
        Assert.False(result.IsFailed);
        Assert.Equal(1, result.FailedItemCount);
        Assert.Contains("success", result.SuccessfulBarcodes);
        Assert.Contains("failed", result.FailedBarcodes);
        Assert.Contains("Catalog missing", result.FailureReasons);
    }

    [Fact]
    public void BatchFollowUp_OnlySelectsSuccessfulItemsForBranchPos()
    {
        TrendyolGoDeferredPriceStockItem[] items =
        [
            new("success", "001", 10m, 2, 1, "ADET", 0, 0, 0, null, null, 3),
            new("failed", "002", 20m, 3, 1, "ADET", 0, 0, 0, null, null, 3)
        ];
        var parsed = new TrendyolGoParsedBatchResult(
            "COMPLETED", true, false, 2, 1,
            new HashSet<string>(["success"], StringComparer.Ordinal),
            new HashSet<string>(["failed"], StringComparer.Ordinal),
            ["Catalog missing"]);

        var selected = TrendyolGoBatchFollowUpProcessor.SelectSuccessfulItems(items, parsed);

        Assert.Single(selected);
        Assert.Equal("success", selected[0].Barcode);
    }

    [Fact]
    public void BatchFollowUp_SelectsAllPayloadItemsWhenBatchHasNoFailures()
    {
        TrendyolGoDeferredPriceStockItem[] items =
        [
            new("one", "001", 10m, 2, 1, "ADET", 0, 0, 0, null, null, 3),
            new("two", "002", 20m, 3, 1, "ADET", 0, 0, 0, null, null, 3)
        ];
        var parsed = new TrendyolGoParsedBatchResult(
            "COMPLETED", true, false, 2, 0,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            []);

        var selected = TrendyolGoBatchFollowUpProcessor.SelectSuccessfulItems(items, parsed);

        Assert.Equal(2, selected.Length);
    }

    [Theory]
    [InlineData("Product not found in catalog")]
    [InlineData("PRODUCT_NOT_FOUND")]
    [InlineData("Urun katalogda bulunamadi")]
    public void BatchFollowUp_DetectsMissingCatalogFailures(string reason)
    {
        Assert.True(TrendyolGoBatchFollowUpProcessor.ReportsMissingCatalog([reason]));
    }

    [Fact]
    public void BatchFollowUp_DoesNotTreatPriceValidationAsMissingCatalog()
    {
        Assert.False(TrendyolGoBatchFollowUpProcessor.ReportsMissingCatalog(
            ["Selling price must be greater than zero"]));
    }

    [Fact]
    public void Options_DefaultToTheDedicatedTrendyolPriceList()
    {
        var options = new TrendyolGoOptions();

        Assert.Equal(3, options.PriceListNo);
        Assert.Equal(0, options.PaymentPlanNo);
        Assert.Equal(2, options.StockReserveQuantity);
        Assert.Equal(300, options.PreviewRefreshIntervalSeconds);
        Assert.Equal(0, options.Catalog.DefaultBrandId);
        Assert.Equal(0, options.Catalog.DefaultCategoryId);
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
            486064, 50, "Merkez Test Subesi", 2, 1, 1, "HASH", generatedAtUtc,
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
    public void BuildRow_LeavesConfiguredStockReserveInTheWarehouse()
    {
        var row = TrendyolGoPriceStockWorkbench.BuildRow(
            new("8690000000000", "Product", 90m, 7),
            new("015550", "Mikro Product", 99.90m, 8.9m, false),
            stockReserveQuantity: 2);

        Assert.Equal(8, row.WarehouseQuantity);
        Assert.Equal(2, row.StockReserveQuantity);
        Assert.Equal(6, row.MikroQuantity);
    }

    [Fact]
    public void SellableQuantity_NeverDropsBelowZero()
    {
        Assert.Equal(0, TrendyolGoPriceStockWorkbench.CalculateSellableQuantity(1.9m, 2));
        Assert.Equal(0, TrendyolGoPriceStockWorkbench.CalculateSellableQuantity(-4m, 2));
    }

    [Fact]
    public void StoreStockReserve_OverridesGlobalReserve()
    {
        var config = new TrendyolGoOptions { StockReserveQuantity = 2 };
        var store = new TrendyolGoStoreMappingOptions
        {
            StoreId = 1,
            WarehouseNo = 103,
            StockReserveQuantity = 5
        };

        Assert.Equal(5, TrendyolGoPriceStockWorkbench.ResolveStockReserveQuantity(config, store));
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
    public void CatalogBuildRow_IgnoresUnsafeDefaultMappings()
    {
        var product = new TrendyolGoCatalogWorkbench.MikroCatalogProduct(
            "015550", "Product", "8690000000000", "MARKA", "KATEGORI", 10, 25m, false);

        var row = TrendyolGoCatalogWorkbench.BuildRow(
            product, new HashSet<string>(), new HashSet<string>(),
            new Dictionary<string, long>(), new Dictionary<string, long>(),
            defaultBrandId: 100, defaultCategoryId: 200);

        Assert.Equal("MappingRequired", row.Status);
        Assert.Null(row.TrendyolBrandId);
        Assert.Null(row.TrendyolCategoryId);
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
            "pg_advisory_xact_lock",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "public.stoksatisfiyat",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "market.stoksatisfiyat",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "SET fiyati = CASE",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "sto_birim_ad = CASE",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "sdp_satisdursun = CASE",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sfiyat_fiyati = CASE",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "sfiyat_birim_pntr",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "@sfiyat_lastup_date > sfiyat_lastup_date",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "@sdp_lastup_date > sdp_lastup_date",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ON CONFLICT",
            TrendyolGoBranchPosPriceSyncService.UpsertCommandText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BranchPosSync_AdvancesOnlineSalesPriceKeyAfterPriceRows()
    {
        Assert.Contains(
            "public.sync",
            TrendyolGoBranchPosPriceSyncService.AdvanceSyncKeyCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "online_satis_fiyatlari_sync",
            TrendyolGoBranchPosPriceSyncService.AdvanceSyncKeyCommandText,
            StringComparison.Ordinal);
        Assert.Contains(
            "ON CONFLICT (sync_type) DO UPDATE",
            TrendyolGoBranchPosPriceSyncService.AdvanceSyncKeyCommandText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BranchPosTask_MarkFailed_StopsAutomaticRetryAndRecordsFinalAttempt()
    {
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
        var task = new TrendyolGoBranchPosPriceSyncTask(Guid.NewGuid(), 486064, 50, "[]", now);

        task.ScheduleRetry("connection refused", now.AddMinutes(5));
        task.MarkFailed("connection refused", now.AddMinutes(10));

        Assert.Equal(TrendyolGoBranchPosPriceSyncStatus.Failed, task.Status);
        Assert.Equal(2, task.AttemptCount);
        Assert.Equal(now.AddMinutes(10), task.CompletedAtUtc);
        Assert.Equal(task.CompletedAtUtc, task.NextAttemptAtUtc);
    }

    [Fact]
    public void BatchFollowUp_HasDedicatedSaleStatusKind()
    {
        Assert.Equal(3, (int)TrendyolGoBatchFollowUpKind.SaleStatus);
    }

    [Fact]
    public void DirectDispatch_AutoCreatesOnlyForMissingCatalogErrorsWhenEnabled()
    {
        var missingCatalog = new HttpRequestException(
            "Trendyol Go Price and inventory update failed: product not found in catalog.");
        var unauthorized = new HttpRequestException(
            "Trendyol Go Price and inventory update failed with HTTP 401: Unauthorized.");

        Assert.True(TrendyolGoPriceStockWorkbench.ShouldAutoCreateMissingCatalog(true, missingCatalog));
        Assert.False(TrendyolGoPriceStockWorkbench.ShouldAutoCreateMissingCatalog(false, missingCatalog));
        Assert.False(TrendyolGoPriceStockWorkbench.ShouldAutoCreateMissingCatalog(true, unauthorized));
        Assert.False(TrendyolGoPriceStockWorkbench.ShouldAutoCreateMissingCatalog(
            true,
            new InvalidOperationException("product not found in catalog")));
    }

    [Fact]
    public void ResolveDirectTargetStores_ThrowsWhenNoTargetIsSelected()
    {
        var request = new TrendyolGoDirectPriceStockDispatchRequest(
            StoreIds: [],
            AllStores: false,
            Mode: "changed");

        var exception = Assert.Throws<ArgumentException>(() =>
            TrendyolGoPriceStockWorkbench.ResolveDirectTargetStores([], request));

        Assert.Contains("StoreId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDirectTargetStores_RejectsPartiallyUnknownStoreSelection()
    {
        TrendyolGoStoreMappingOptions[] configuredStores =
        [
            new() { StoreId = 100, WarehouseNo = 50, StoreName = "Merkez" }
        ];
        var request = new TrendyolGoDirectPriceStockDispatchRequest(
            StoreIds: [100, 999],
            AllStores: false);

        var exception = Assert.Throws<ArgumentException>(() =>
            TrendyolGoPriceStockWorkbench.ResolveDirectTargetStores(configuredStores, request));

        Assert.Contains("999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DirectPriceStockItem_NormalizesValuesCorrectly()
    {
        var item = new TrendyolGoDirectPriceStockItem(
            Barcode: "8690000000001",
            StockCode: "STK001",
            ProductName: "Test Urun",
            SellingPrice: 49.90m,
            Quantity: 12,
            PriceChangedFlag: true,
            PriceLastUpDate: DateTime.UtcNow,
            WarehouseNo: 103,
            UnitPointer: 1);

        Assert.Equal("8690000000001", item.Barcode);
        Assert.Equal(49.90m, item.SellingPrice);
        Assert.Equal(12, item.Quantity);
        Assert.True(item.PriceChangedFlag);
    }

    [Fact]
    public void SaleOffPayload_IsStoreScopedAndUsesSellerClosedReason()
    {
        TrendyolGoDirectPriceStockItem[] items =
        [
            new("8690000000001", "STK001", "Test Urun", 49.90m, 0, true, null, 50, SalesBlocked: 1)
        ];

        var payload = TrendyolGoPriceStockWorkbench.BuildSaleOffPayload(486064, items);
        var row = payload.GetProperty("items")[0];

        Assert.Equal("8690000000001", row.GetProperty("barcode").GetString());
        Assert.Equal("SELLER_CLOSED", row.GetProperty("saleOffReason").GetString());
        Assert.Equal(486064, row.GetProperty("storeId").GetInt64());
        Assert.False(row.TryGetProperty("sellingPrice", out _));
    }

    private static TrendyolGoPriceStockRow[] CreateDispatchRows() =>
    [
        new("ready", "001", "Ready Product", 10m, 1, 11m, 2, "Ready", null),
        new("unchanged", "002", "Unchanged Product", 20m, 3, 20m, 3, "Unchanged", null),
        new("skipped", null, "Skipped Product", null, null, null, null, "Skipped", "Missing")
    ];

}
