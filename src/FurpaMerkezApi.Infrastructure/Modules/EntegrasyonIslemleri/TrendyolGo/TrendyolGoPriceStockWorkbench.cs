using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoPriceStockWorkbench(
    ITrendyolGoIntegrationService trendyol,
    MikroDbContext mikroDbContext,
    TrendyolGoBatchFollowUpQueue batchFollowUpQueue,
    TrendyolGoPriceStockSnapshotCoordinator snapshotCoordinator,
    ITrendyolGoCatalogWorkbench catalogWorkbench,
    IOptionsMonitor<TrendyolGoOptions> options) :
    ITrendyolGoPriceStockWorkbench,
    ITrendyolGoPriceStockSnapshotRefresher
{
    private readonly SemaphoreSlim mikroReadGate = new(1, 1);

    public Task<TrendyolGoPriceStockPreview> PreviewAsync(
        long storeId, string view, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedView = NormalizeView(view);
        var store = GetConfiguredStore(storeId);
        var stockReserveQuantity = ResolveStockReserveQuantity(options.CurrentValue, store);
        var state = snapshotCoordinator.GetState(storeId);
        var isStale = IsStale(state.Snapshot);
        if ((state.Snapshot is null || isStale) && CanAutomaticallyRetry(state))
        {
            snapshotCoordinator.RequestRefresh(storeId);
            state = snapshotCoordinator.GetState(storeId);
        }

        var snapshot = state.Snapshot;
        if (snapshot is null)
        {
            return Task.FromResult(new TrendyolGoPriceStockPreview(
                storeId, store.WarehouseNo, store.StoreName, stockReserveQuantity,
                0, 0, string.Empty, 0, 0, 0,
                normalizedView, 0, [], GetSnapshotStatus(state), true,
                null, state.RefreshStartedAtUtc, state.RefreshCompletedAtUtc,
                state.RefreshError));
        }

        var visibleRows = FilterRows(snapshot.Rows, normalizedView);
        return Task.FromResult(new TrendyolGoPriceStockPreview(
            snapshot.StoreId, snapshot.WarehouseNo, snapshot.StoreName, snapshot.StockReserveQuantity,
            snapshot.TotalPages, snapshot.TotalElements, snapshot.PreviewHash,
            snapshot.ReadyCount, snapshot.UnchangedCount, snapshot.SkippedCount,
            normalizedView, visibleRows.Length, visibleRows,
            GetSnapshotStatus(state), isStale, snapshot.GeneratedAtUtc,
            state.RefreshStartedAtUtc, state.RefreshCompletedAtUtc,
            state.RefreshError));
    }

    public TrendyolGoPriceStockRefreshStatus RequestRefresh(long storeId)
    {
        var store = GetConfiguredStore(storeId);
        var accepted = snapshotCoordinator.RequestRefresh(storeId);
        var state = snapshotCoordinator.GetState(storeId);
        return new TrendyolGoPriceStockRefreshStatus(
            storeId, store.WarehouseNo, store.StoreName,
            GetSnapshotStatus(state), accepted, state.Snapshot is not null,
            IsStale(state.Snapshot), state.Snapshot?.GeneratedAtUtc,
            state.RefreshStartedAtUtc, state.RefreshCompletedAtUtc,
            state.RefreshError);
    }

    public async Task RefreshSnapshotAsync(long storeId, CancellationToken cancellationToken)
    {
        var snapshot = await BuildFullPreviewSnapshotAsync(storeId, cancellationToken);
        snapshotCoordinator.Publish(snapshot);
    }

    public async Task<TrendyolGoPriceStockDispatch> DispatchAsync(
        long storeId, string previewHash, TrendyolGoPriceStockDispatchMode mode,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previewHash) || barcodes.Count > 10000 ||
            barcodes.Any(string.IsNullOrWhiteSpace) ||
            barcodes.Count != barcodes.Distinct(StringComparer.Ordinal).Count() ||
            (mode == TrendyolGoPriceStockDispatchMode.Selected && barcodes.Count == 0))
        {
            throw new ArgumentException(
                "A preview hash and either a bulk dispatch mode or distinct barcodes are required.");
        }

        var store = GetConfiguredStore(storeId);
        var stockReserveQuantity = ResolveStockReserveQuantity(options.CurrentValue, store);
        var snapshot = snapshotCoordinator.GetState(storeId).Snapshot
            ?? throw new InvalidOperationException(
                "Price-stock preview is still preparing. Wait for the snapshot before sending.");
        if (!string.Equals(snapshot.PreviewHash, previewHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Price, stock or Trendyol product data changed. Refresh the preview before sending.");
        }

        var selected = SelectRows(snapshot.Rows, mode, barcodes);
        if (selected.Length == 0)
        {
            throw new ArgumentException("There are no eligible price-stock rows to send.");
        }

        var config = options.CurrentValue;
        var responses = new List<JsonElement?>();
        foreach (var batch in selected.Chunk(1000))
        {
            var mikro = await ReadMikroProductsAsync(
                snapshot.WarehouseNo, batch.Select(item => item.Barcode).ToArray(),
                config.PriceListNo, config.PaymentPlanNo, cancellationToken);
            EnsureSelectedValuesAreCurrent(batch, snapshot.Products, mikro, stockReserveQuantity);
            var payload = JsonSerializer.SerializeToElement(new
            {
                items = batch.Select(item => new
                {
                    barcode = item.Barcode,
                    sellingPrice = item.MikroPrice!.Value,
                    quantity = item.MikroQuantity!.Value,
                    storeId
                }).ToArray()
            });
            var response = await trendyol.UpdatePriceAndInventoryAsync(payload, cancellationToken);
            responses.Add(response);
            var batchRequestId = TrendyolGoBatchFollowUpProcessor.RequireBatchRequestId(
                response,
                "price-stock dispatch");
            var deferredItems = batch
                .Select(item => mikro.GetValueOrDefault(item.Barcode))
                .Zip(batch, (mikroItem, previewItem) => (mikroItem, previewItem))
                .Where(pair => pair.mikroItem?.StockCode is not null && pair.mikroItem.Price is not null)
                .Select(pair => ToDeferredItem(
                    pair.previewItem.Barcode,
                    pair.mikroItem!,
                    pair.previewItem.MikroQuantity!.Value,
                    config.PriceListNo))
                .ToArray();
            await batchFollowUpQueue.EnqueuePriceStockAsync(
                batchRequestId,
                storeId,
                snapshot.WarehouseNo,
                deferredItems,
                cancellationToken);
        }

        snapshotCoordinator.Invalidate(storeId);
        return new TrendyolGoPriceStockDispatch(
            storeId, snapshot.WarehouseNo, ToWireValue(mode),
            selected.Length, responses.Count, responses);
    }

    public async Task<TrendyolGoDirectPriceStockPreview> DirectPreviewAsync(
        TrendyolGoDirectPriceStockQueryRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var store = GetConfiguredStore(request.StoreId);
        var config = options.CurrentValue;
        var stockReserveQuantity = ResolveStockReserveQuantity(config, store);

        var items = await QueryDirectMikroProductsAsync(
            store.WarehouseNo,
            request.Mode,
            request.SinceDate,
            request.OnlyFlaggedChanged,
            request.Search,
            request.Take,
            barcodes: null,
            config.PriceListNo,
            config.PaymentPlanNo,
            stockReserveQuantity,
            cancellationToken);

        return new TrendyolGoDirectPriceStockPreview(
            store.StoreId,
            store.WarehouseNo,
            store.StoreName,
            stockReserveQuantity,
            request.Mode.Trim().ToLowerInvariant(),
            request.SinceDate,
            items.Count,
            items);
    }

    public async Task<TrendyolGoDirectPriceStockDispatchResult> DirectDispatchAsync(
        TrendyolGoDirectPriceStockDispatchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var config = options.CurrentValue;
        var configuredStores = config.Stores;

        var targetStores = ResolveDirectTargetStores(configuredStores, request);

        var storeResults = new List<TrendyolGoDirectStoreDispatchResult>();
        var totalItemsDispatched = 0;

        foreach (var store in targetStores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dispatchedItemCount = 0;
            var completedBatchCount = 0;
            var catalogCreatedCount = 0;
            var batchRequestIds = new List<string>();
            var catalogBatchRequestIds = new List<string>();
            var followUpTaskIds = new List<Guid>();
            var warnings = new List<string>();

            try
            {
                var stockReserveQuantity = ResolveStockReserveQuantity(config, store);
                var items = await QueryDirectMikroProductsAsync(
                    store.WarehouseNo,
                    request.Mode,
                    request.SinceDate,
                    request.OnlyFlaggedChanged,
                    request.Search,
                    take: 0,
                    request.Barcodes,
                    config.PriceListNo,
                    config.PaymentPlanNo,
                    stockReserveQuantity,
                    cancellationToken);

                if (items.Count == 0)
                {
                    storeResults.Add(new TrendyolGoDirectStoreDispatchResult(
                        store.StoreId, store.WarehouseNo, store.StoreName,
                        0, 0, true, "Gonderilecek uygun fiyat/stok kalemi bulunamadi.", []));
                    continue;
                }

                var distinctItems = items.DistinctBy(i => i.Barcode, StringComparer.Ordinal).ToArray();
                foreach (var chunk in distinctItems.Chunk(1000))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var payload = JsonSerializer.SerializeToElement(new
                    {
                        items = chunk.Select(item => new
                        {
                            barcode = item.Barcode,
                            sellingPrice = item.SellingPrice,
                            quantity = item.Quantity,
                            storeId = store.StoreId
                        }).ToArray()
                    });

                    JsonElement? response = null;
                    try
                    {
                        response = await trendyol.UpdatePriceAndInventoryAsync(payload, cancellationToken);
                    }
                    catch (Exception ex) when (ShouldAutoCreateMissingCatalog(request.AutoCreateMissingCatalog, ex))
                    {
                        var chunkBarcodes = chunk.Select(item => item.Barcode).ToArray();
                        await RefreshSnapshotAsync(store.StoreId, cancellationToken);
                        var autoResult = await catalogWorkbench.AutoCreateProductsAsync(store.StoreId, chunkBarcodes, cancellationToken);
                        if (autoResult.CreatedCount <= 0)
                        {
                            throw new InvalidOperationException(
                                autoResult.ErrorMessage ?? "Eksik Trendyol katalog urunleri olusturulamadi.",
                                ex);
                        }

                        catalogCreatedCount += autoResult.CreatedCount;
                        catalogBatchRequestIds.AddRange(autoResult.BatchRequestIds);
                        var deferredChunk = chunk.Select(item => ToDeferredItem(item, config.PriceListNo)).ToArray();
                        var submissions = autoResult.BatchSubmissions ?? [];
                        if (submissions.Count == 0)
                        {
                            throw new InvalidOperationException(
                                "Katalog istegi kabul edildi ancak otomatik takip icin batch bilgisi donmedi.",
                                ex);
                        }

                        foreach (var submission in submissions)
                        {
                            var submissionBarcodeSet = submission.Barcodes.ToHashSet(StringComparer.Ordinal);
                            var submissionItems = deferredChunk
                                .Where(item => submissionBarcodeSet.Contains(item.Barcode))
                                .ToArray();
                            followUpTaskIds.Add(await batchFollowUpQueue.EnqueueCatalogThenPriceStockAsync(
                                submission.BatchRequestId,
                                store.StoreId,
                                store.WarehouseNo,
                                submissionItems,
                                submission.Barcodes,
                                cancellationToken));
                        }

                        dispatchedItemCount += autoResult.CreatedCount;
                        if (autoResult.CreatedCount < chunk.Length)
                        {
                            warnings.Add(
                                $"{chunk.Length - autoResult.CreatedCount} urun katalog kurallarini gecemedi; bu urunler gonderilmedi.");
                        }

                        continue;
                    }

                    dispatchedItemCount += chunk.Length;
                    completedBatchCount++;
                    var batchId = TrendyolGoBatchFollowUpProcessor.RequireBatchRequestId(
                        response,
                        "direct price-stock dispatch");
                    batchRequestIds.Add(batchId);
                    followUpTaskIds.Add(await batchFollowUpQueue.EnqueuePriceStockAsync(
                        batchId,
                        store.StoreId,
                        store.WarehouseNo,
                        chunk.Select(item => ToDeferredItem(item, config.PriceListNo)).ToArray(),
                        cancellationToken,
                        request.AutoCreateMissingCatalog));
                }

                totalItemsDispatched += dispatchedItemCount;
                var warningMessage = warnings.Count == 0 ? null : string.Join(" | ", warnings);
                storeResults.Add(new TrendyolGoDirectStoreDispatchResult(
                    store.StoreId, store.WarehouseNo, store.StoreName,
                    dispatchedItemCount, completedBatchCount, warnings.Count == 0, warningMessage, batchRequestIds,
                    catalogCreatedCount, catalogBatchRequestIds, followUpTaskIds,
                    warnings.Count > 0
                        ? "QueuedWithWarnings"
                        : catalogCreatedCount > 0 ? "CatalogAndPriceStockQueued" : "PriceStockQueued"));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                totalItemsDispatched += dispatchedItemCount;
                storeResults.Add(new TrendyolGoDirectStoreDispatchResult(
                    store.StoreId, store.WarehouseNo, store.StoreName,
                    dispatchedItemCount, completedBatchCount, false, ex.Message, batchRequestIds,
                    catalogCreatedCount, catalogBatchRequestIds, followUpTaskIds, "Failed"));
            }
        }

        var totalCatalogCreated = storeResults.Sum(s => s.CatalogCreatedCount);
        return new TrendyolGoDirectPriceStockDispatchResult(
            targetStores.Count,
            totalItemsDispatched,
            storeResults,
            totalCatalogCreated);
    }

    internal static IReadOnlyList<TrendyolGoStoreMappingOptions> ResolveDirectTargetStores(
        IReadOnlyList<TrendyolGoStoreMappingOptions> configuredStores,
        TrendyolGoDirectPriceStockDispatchRequest request)
    {
        if (request.AllStores)
        {
            if (configuredStores.Count == 0)
            {
                throw new InvalidOperationException("No Trendyol Go stores are configured.");
            }

            return configuredStores;
        }

        if (request.StoreIds is not { Count: > 0 } storeIds)
        {
            throw new ArgumentException(
                "Either AllStores must be true or at least one StoreId must be provided.");
        }

        var requestedStoreIds = storeIds.Distinct().ToHashSet();
        var targetStores = configuredStores
            .Where(store => requestedStoreIds.Contains(store.StoreId))
            .ToArray();
        if (targetStores.Length != requestedStoreIds.Count)
        {
            var configuredStoreIds = targetStores.Select(store => store.StoreId).ToHashSet();
            var missingStoreIds = requestedStoreIds.Where(id => !configuredStoreIds.Contains(id));
            throw new ArgumentException(
                $"Unknown Trendyol Go store IDs: {string.Join(", ", missingStoreIds)}.");
        }

        return targetStores;
    }

    private async Task<IReadOnlyList<TrendyolGoDirectPriceStockItem>> QueryDirectMikroProductsAsync(
        int warehouseNo,
        string mode,
        DateTime? sinceDate,
        bool? onlyFlaggedChanged,
        string? search,
        int take,
        IReadOnlyCollection<string>? barcodes,
        int priceListNo,
        int paymentPlanNo,
        int stockReserveQuantity,
        CancellationToken cancellationToken)
    {
        var isAllMode = string.Equals(mode?.Trim(), "all", StringComparison.OrdinalIgnoreCase);
        var flaggedOnly = onlyFlaggedChanged == true;

        await mikroReadGate.WaitAsync(cancellationToken);
        try
        {
            var connection = mikroDbContext.Database.GetDbConnection();
            var closeConnection = connection.State == ConnectionState.Closed;
            if (closeConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandType = CommandType.Text;
                command.CommandTimeout = 300;

                var whereClauses = new List<string>
                {
                    "COALESCE(stock.sto_iptal, 0) = 0",
                    "COALESCE(stock.sto_pasif_fl, 0) = 0",
                    "COALESCE(stock.sto_satis_dursun, 0) = 0",
                    "COALESCE(detail.sdp_Pasif_fl, 0) = 0",
                    "COALESCE(detail.sdp_satisdursun, 0) = 0",
                    "COALESCE(stock.sto_webe_gonderilecek_fl, 0) = 1",
                    "COALESCE(priceRow.sfiyat_iptal, 0) = 0",
                    "priceRow.sfiyat_fiyati > 0"
                };

                if (!isAllMode)
                {
                    if (flaggedOnly)
                    {
                        whereClauses.Add("COALESCE(priceRow.sfiyat_degisti, 0) = 1");
                    }
                    else if (sinceDate.HasValue)
                    {
                        whereClauses.Add("(priceRow.sfiyat_lastup_date >= @sinceDate OR priceRow.sfiyat_create_date >= @sinceDate OR COALESCE(priceRow.sfiyat_degisti, 0) = 1)");
                        AddParameter(command, "@sinceDate", sinceDate.Value);
                    }
                    else
                    {
                        whereClauses.Add("(COALESCE(priceRow.sfiyat_degisti, 0) = 1 OR priceRow.sfiyat_lastup_date >= CAST(GETDATE() AS date))");
                    }
                }

                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereClauses.Add("(barcode.Barcode LIKE @searchPattern OR stock.sto_kod LIKE @searchPattern OR stock.sto_isim LIKE @searchPattern)");
                    AddParameter(command, "@searchPattern", $"%{search.Trim()}%");
                }

                if (barcodes is { Count: > 0 })
                {
                    var barcodeList = barcodes
                        .Where(b => !string.IsNullOrWhiteSpace(b))
                        .Select(b => b.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (barcodeList.Length > 0)
                    {
                        whereClauses.Add("barcode.Barcode IN (SELECT CONVERT(nvarchar(100), [value]) FROM OPENJSON(@barcodesJson))");
                        AddParameter(command, "@barcodesJson", JsonSerializer.Serialize(barcodeList));
                    }
                }

                var topClause = take > 0 ? $"TOP ({take})" : "";

                command.CommandText = $"""
                    SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                    WITH RankedBarcodes AS
                    (
                        SELECT bar_stokkodu AS StockCode,
                               bar_kodu AS Barcode,
                               bar_birimpntr AS UnitPointer,
                               ROW_NUMBER() OVER
                               (
                                   PARTITION BY bar_stokkodu
                                   ORDER BY COALESCE(bar_master, 0) DESC,
                                            COALESCE(bar_lastup_date, bar_create_date) DESC,
                                            bar_kodu ASC
                               ) AS RowNo
                        FROM dbo.BARKOD_TANIMLARI WITH (NOLOCK)
                        WHERE COALESCE(bar_iptal, 0) = 0
                          AND NULLIF(LTRIM(RTRIM(bar_kodu)), '') IS NOT NULL
                    )
                    SELECT {topClause}
                        barcode.Barcode AS Barcode,
                        stock.sto_kod AS StockCode,
                        stock.sto_isim AS ProductName,
                        COALESCE(unitDefinition.sto_birim_ad, stock.sto_birim1_ad, '') AS UnitName,
                        COALESCE(barcode.UnitPointer, 1) AS UnitPointer,
                        priceRow.sfiyat_fiyati AS SellingPrice,
                        COALESCE(priceRow.sfiyat_degisti, 0) AS PriceChanged,
                        COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) AS PriceLastUpDate,
                        COALESCE(detail.sdp_satisdursun, 0) AS SalesBlocked,
                        COALESCE(detail.sdp_sipdursun, 0) AS OrderBlocked,
                        COALESCE(detail.sdp_malkabuldursun, 0) AS GoodsAcceptanceBlocked,
                        detail.sdp_lastup_date AS WarehouseLastUpDate,
                        CASE WHEN stock.sto_kod IS NULL THEN 0
                             ELSE dbo.fn_DepodakiMiktar(stock.sto_kod, @warehouseNo, CONVERT(date, GETDATE())) END AS Quantity
                    FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                    INNER JOIN RankedBarcodes AS barcode
                        ON barcode.StockCode = stock.sto_kod AND barcode.RowNo = 1
                    INNER JOIN dbo.STOK_SATIS_FIYAT_LISTELERI AS priceRow WITH (NOLOCK)
                        ON priceRow.sfiyat_stokkod = stock.sto_kod
                       AND priceRow.sfiyat_deposirano = @warehouseNo
                       AND priceRow.sfiyat_listesirano = @priceListNo
                       AND priceRow.sfiyat_odemeplan = @paymentPlanNo
                       AND priceRow.sfiyat_birim_pntr = COALESCE(barcode.UnitPointer, 1)
                    LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                        ON detail.sdp_depo_kod = stock.sto_kod
                       AND detail.sdp_depo_no = @warehouseNo
                    LEFT JOIN dbo.STOK_BIRIM_TANIMLARI_DIKEY AS unitDefinition WITH (NOLOCK)
                        ON unitDefinition.sto_kod = stock.sto_kod
                       AND unitDefinition.sto_birimID = COALESCE(barcode.UnitPointer, 1)
                    WHERE {string.Join(" AND ", whereClauses)}
                    ORDER BY COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) DESC, stock.sto_kod ASC;
                    """;

                AddParameter(command, "@warehouseNo", warehouseNo);
                AddParameter(command, "@priceListNo", priceListNo);
                AddParameter(command, "@paymentPlanNo", paymentPlanNo);

                var items = new List<TrendyolGoDirectPriceStockItem>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var rawQuantity = reader["Quantity"] is DBNull ? 0m : Convert.ToDecimal(reader["Quantity"], CultureInfo.InvariantCulture);
                    var warehouseQuantity = NormalizeWarehouseQuantity(rawQuantity);
                    var sellableQuantity = CalculateSellableQuantity(rawQuantity, stockReserveQuantity);

                    items.Add(new TrendyolGoDirectPriceStockItem(
                        Barcode: Convert.ToString(reader["Barcode"], CultureInfo.InvariantCulture) ?? string.Empty,
                        StockCode: Convert.ToString(reader["StockCode"], CultureInfo.InvariantCulture) ?? string.Empty,
                        ProductName: Convert.ToString(reader["ProductName"], CultureInfo.InvariantCulture) ?? string.Empty,
                        SellingPrice: Convert.ToDecimal(reader["SellingPrice"], CultureInfo.InvariantCulture),
                        Quantity: sellableQuantity,
                        PriceChangedFlag: Convert.ToInt32(reader["PriceChanged"], CultureInfo.InvariantCulture) != 0,
                        PriceLastUpDate: reader["PriceLastUpDate"] is DBNull ? null : Convert.ToDateTime(reader["PriceLastUpDate"], CultureInfo.InvariantCulture),
                        WarehouseNo: warehouseNo,
                        UnitPointer: Convert.ToInt32(reader["UnitPointer"], CultureInfo.InvariantCulture),
                        UnitName: Convert.ToString(reader["UnitName"], CultureInfo.InvariantCulture) ?? string.Empty,
                        SalesBlocked: Convert.ToInt32(reader["SalesBlocked"], CultureInfo.InvariantCulture),
                        OrderBlocked: Convert.ToInt32(reader["OrderBlocked"], CultureInfo.InvariantCulture),
                        GoodsAcceptanceBlocked: Convert.ToInt32(reader["GoodsAcceptanceBlocked"], CultureInfo.InvariantCulture),
                        WarehouseLastUpDate: reader["WarehouseLastUpDate"] is DBNull
                            ? null
                            : Convert.ToDateTime(reader["WarehouseLastUpDate"], CultureInfo.InvariantCulture),
                        WarehouseQuantity: warehouseQuantity,
                        StockReserveQuantity: stockReserveQuantity));
                }

                return items;
            }
            finally
            {
                if (closeConnection)
                {
                    await connection.CloseAsync();
                }
            }
        }
        finally
        {
            mikroReadGate.Release();
        }
    }
    private async Task<CatalogPage> LoadCatalogPageAsync(
        long storeId, int page, int size, CancellationToken cancellationToken)
    {
        var catalog = await trendyol.ListProductsAsync(
            new TrendyolGoProductListRequest(storeId, "ALL_PRODUCT", null, null, null, null, [], page, size),
            cancellationToken);
        if (!catalog.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Trendyol Go product response has no content array.");
        }

        var products = content.EnumerateArray().Select(ReadProduct).ToArray();
        return new CatalogPage(
            ReadInt(catalog, "totalPages") ?? 0,
            ReadLong(catalog, "totalElements") ?? 0,
            products);
    }

    private async Task<TrendyolGoPriceStockSnapshot> BuildFullPreviewSnapshotAsync(
        long storeId, CancellationToken cancellationToken)
    {
        var config = options.CurrentValue;
        if (config.PriceListNo < 1 || config.PaymentPlanNo < 0 ||
            config.PreviewRefreshIntervalSeconds is < 60 or > 3600)
        {
            throw new InvalidOperationException("Trendyol Go price-stock configuration is invalid.");
        }

        var store = GetConfiguredStore(storeId);
        var stockReserveQuantity = ResolveStockReserveQuantity(config, store);
        var fetchPageSize = Math.Clamp(config.PreviewFetchPageSize, 1, 100);
        var firstPage = await LoadCatalogPageAsync(storeId, 0, fetchPageSize, cancellationToken);
        var pageParallelism = Math.Clamp(config.PreviewPageParallelism, 1, 5);
        using var pageGate = new SemaphoreSlim(pageParallelism, pageParallelism);
        var otherPageTasks = Enumerable.Range(1, Math.Max(firstPage.TotalPages - 1, 0))
            .Select(async page =>
            {
                await pageGate.WaitAsync(cancellationToken);
                try
                {
                    return await LoadCatalogPageAsync(storeId, page, fetchPageSize, cancellationToken);
                }
                finally
                {
                    pageGate.Release();
                }
            })
            .ToArray();

        var pages = new List<CatalogPage> { firstPage };
        if (otherPageTasks.Length > 0)
        {
            pages.AddRange(await Task.WhenAll(otherPageTasks));
        }

        var products = pages.SelectMany(item => item.Products).ToArray();
        var barcodes = products.Select(item => item.Barcode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var mikro = await ReadMikroProductsInBatchesAsync(
            store.WarehouseNo, barcodes, config.PriceListNo, config.PaymentPlanNo, cancellationToken);
        var duplicateBarcodes = products.GroupBy(item => item.Barcode, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var rows = products.Select(product =>
        {
            var row = BuildRow(product, mikro.GetValueOrDefault(product.Barcode), stockReserveQuantity);
            return duplicateBarcodes.Contains(product.Barcode)
                ? row with { Status = "Skipped", Reason = "Trendyol katalogunda barkod tekrari var." }
                : row;
        }).ToArray();
        var hashData = JsonSerializer.SerializeToUtf8Bytes(new
        {
            storeId,
            fetchPageSize,
            firstPage.TotalPages,
            firstPage.TotalElements,
            rows
        });
        return new TrendyolGoPriceStockSnapshot(
            storeId, store.WarehouseNo, store.StoreName, stockReserveQuantity,
            firstPage.TotalPages, firstPage.TotalElements,
            Convert.ToHexString(SHA256.HashData(hashData)), DateTime.UtcNow,
            products, rows);
    }

    private async Task<Dictionary<string, MikroProduct>> ReadMikroProductsInBatchesAsync(
        int warehouseNo,
        IReadOnlyCollection<string> barcodes,
        int priceListNo,
        int paymentPlanNo,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, MikroProduct>(StringComparer.Ordinal);
        foreach (var batch in barcodes.Chunk(1000))
        {
            var items = await ReadMikroProductsAsync(
                warehouseNo, batch, priceListNo, paymentPlanNo, cancellationToken);
            foreach (var item in items)
            {
                result[item.Key] = item.Value;
            }
        }

        return result;
    }
    private static void EnsureSelectedValuesAreCurrent(
        IReadOnlyCollection<TrendyolGoPriceStockRow> selected,
        IReadOnlyCollection<TrendyolProduct> products,
        IReadOnlyDictionary<string, MikroProduct> mikro,
        int stockReserveQuantity)
    {
        foreach (var selectedRow in selected)
        {
            var product = products.Single(item => item.Barcode == selectedRow.Barcode);
            var currentRow = BuildRow(
                product,
                mikro.GetValueOrDefault(selectedRow.Barcode),
                stockReserveQuantity);
            if (currentRow.Status == "Skipped" || currentRow.MikroPrice != selectedRow.MikroPrice ||
                currentRow.MikroQuantity != selectedRow.MikroQuantity)
            {
                throw new InvalidOperationException(
                    $"Mikro price or stock changed for barcode {selectedRow.Barcode}. Refresh the preview before sending.");
            }
        }
    }

    private static TrendyolGoPriceStockRow[] ResolveSelectedRows(
        IReadOnlyCollection<TrendyolGoPriceStockRow> rows, IReadOnlyCollection<string> barcodes)
    {
        var selected = barcodes.Select(barcode => rows.SingleOrDefault(item => item.Barcode == barcode)
            ?? throw new ArgumentException($"Barcode {barcode} was not found in the preview.")).ToArray();
        if (selected.Any(item => item.Status != "Ready"))
        {
            throw new ArgumentException("Only ready preview rows can be sent.");
        }

        return selected;
    }

    internal static TrendyolGoPriceStockRow[] SelectRows(
        IReadOnlyCollection<TrendyolGoPriceStockRow> rows,
        TrendyolGoPriceStockDispatchMode mode,
        IReadOnlyCollection<string> barcodes) =>
        mode switch
        {
            TrendyolGoPriceStockDispatchMode.Selected => ResolveSelectedRows(rows, barcodes),
            TrendyolGoPriceStockDispatchMode.Changed =>
                rows.Where(item => item.Status == "Ready").ToArray(),
            TrendyolGoPriceStockDispatchMode.AllExisting =>
                rows.Where(item => item.Status is "Ready" or "Unchanged").ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported dispatch mode.")
        };

    private static string ToWireValue(TrendyolGoPriceStockDispatchMode mode) => mode switch
    {
        TrendyolGoPriceStockDispatchMode.Selected => "selected",
        TrendyolGoPriceStockDispatchMode.Changed => "changed",
        TrendyolGoPriceStockDispatchMode.AllExisting => "all-existing",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported dispatch mode.")
    };

    private static TrendyolGoPriceStockRow[] FilterRows(
        IReadOnlyCollection<TrendyolGoPriceStockRow> rows, string view) =>
        view switch
        {
            "all" => rows.ToArray(),
            "issues" => rows.Where(item => item.Status == "Skipped").ToArray(),
            _ => rows.Where(item => item.Status == "Ready").ToArray()
        };

    private static string NormalizeView(string view) => view.Trim().ToLowerInvariant() switch
    {
        "actionable" or "all" or "issues" => view.Trim().ToLowerInvariant(),
        _ => throw new ArgumentException("view must be actionable, all or issues.")
    };

    private TrendyolGoStoreMappingOptions GetConfiguredStore(long storeId) =>
        options.CurrentValue.Stores.SingleOrDefault(item => item.StoreId == storeId)
        ?? throw new ArgumentException($"storeId {storeId} is not configured.");

    private bool IsStale(TrendyolGoPriceStockSnapshot? snapshot) =>
        snapshot is null || DateTime.UtcNow - snapshot.GeneratedAtUtc >=
        TrendyolGoPriceStockSnapshotWorker.GetRefreshInterval(options.CurrentValue);

    private bool CanAutomaticallyRetry(
        TrendyolGoPriceStockSnapshotCoordinator.SnapshotState state) =>
        state.RefreshState != SnapshotRefreshState.Failed ||
        state.RefreshCompletedAtUtc is null ||
        DateTime.UtcNow - state.RefreshCompletedAtUtc.Value >=
        TrendyolGoPriceStockSnapshotWorker.GetRefreshInterval(options.CurrentValue);

    private static string GetSnapshotStatus(
        TrendyolGoPriceStockSnapshotCoordinator.SnapshotState state)
    {
        if (state.Snapshot is null)
        {
            return state.RefreshState == SnapshotRefreshState.Failed ? "Failed" : "Preparing";
        }

        return state.RefreshState switch
        {
            SnapshotRefreshState.Queued or SnapshotRefreshState.Refreshing => "Refreshing",
            SnapshotRefreshState.Failed => "Failed",
            _ => "Ready"
        };
    }


    private async Task<Dictionary<string, MikroProduct>> ReadMikroProductsAsync(
        int warehouseNo,
        IReadOnlyCollection<string> barcodes,
        int priceListNo,
        int paymentPlanNo,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, MikroProduct>(StringComparer.Ordinal);
        if (barcodes.Count == 0)
        {
            return result;
        }

        await mikroReadGate.WaitAsync(cancellationToken);
        try
        {
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            var valueRows = barcodes.Select((_, index) => $"(@barcode{index})");
            command.CommandText = $"""
                SELECT requested.Barcode, stock.sto_kod AS StockCode, stock.sto_isim AS StockName,
                       price.Price, COALESCE(price.UnitPointer, barcode.bar_birimpntr, 1) AS UnitPointer,
                       COALESCE(unitDefinition.sto_birim_ad, '') AS UnitName,
                       COALESCE(detail.sdp_satisdursun, 0) AS SalesBlocked,
                       COALESCE(detail.sdp_sipdursun, 0) AS OrderBlocked,
                       COALESCE(detail.sdp_malkabuldursun, 0) AS GoodsAcceptanceBlocked,
                       price.PriceUpdatedAt, detail.sdp_lastup_date AS WarehouseUpdatedAt,
                       CASE WHEN stock.sto_kod IS NULL THEN NULL
                            ELSE dbo.fn_DepodakiMiktar(stock.sto_kod, @warehouseNo, CONVERT(date, GETDATE())) END AS Quantity,
                       CASE WHEN COALESCE(stock.sto_iptal, 0) = 1
                                      OR COALESCE(detail.sdp_Pasif_fl, stock.sto_pasif_fl, 0) = 1
                                      OR COALESCE(detail.sdp_satisdursun, stock.sto_satis_dursun, 0) <> 0
                                      OR COALESCE(stock.sto_webe_gonderilecek_fl, 0) = 0
                            THEN 1 ELSE 0 END AS Blocked
                FROM (VALUES {string.Join(",", valueRows)}) AS requested(Barcode)
                LEFT JOIN dbo.BARKOD_TANIMLARI AS barcode
                  ON barcode.bar_kodu = requested.Barcode AND COALESCE(barcode.bar_iptal, 0) = 0
                LEFT JOIN dbo.STOKLAR AS stock ON stock.sto_kod = barcode.bar_stokkodu
                LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail
                  ON detail.sdp_depo_kod = stock.sto_kod AND detail.sdp_depo_no = @warehouseNo
                LEFT JOIN dbo.STOK_BIRIM_TANIMLARI_DIKEY AS unitDefinition
                  ON unitDefinition.sto_kod = stock.sto_kod
                 AND unitDefinition.sto_birimID = COALESCE(barcode.bar_birimpntr, 1)
                OUTER APPLY
                (
                    SELECT TOP (1) priceRow.sfiyat_fiyati AS Price,
                           priceRow.sfiyat_birim_pntr AS UnitPointer,
                           priceRow.sfiyat_lastup_date AS PriceUpdatedAt
                    FROM dbo.STOK_SATIS_FIYAT_LISTELERI AS priceRow
                    WHERE priceRow.sfiyat_stokkod = stock.sto_kod
                        AND priceRow.sfiyat_deposirano = @warehouseNo
                        AND priceRow.sfiyat_listesirano = @priceListNo
                        AND priceRow.sfiyat_odemeplan = @paymentPlanNo
                        AND priceRow.sfiyat_birim_pntr = COALESCE(barcode.bar_birimpntr, 1)
                        AND COALESCE(priceRow.sfiyat_iptal, 0) = 0
                        AND priceRow.sfiyat_fiyati IS NOT NULL
                    ORDER BY COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) DESC
                ) AS price;
                """;
            AddParameter(command, "@warehouseNo", warehouseNo);
            AddParameter(command, "@priceListNo", priceListNo);
            AddParameter(command, "@paymentPlanNo", paymentPlanNo);
            var index = 0;
            foreach (var barcode in barcodes)
            {
                AddParameter(command, $"@barcode{index++}", barcode);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var barcode = Convert.ToString(reader["Barcode"], CultureInfo.InvariantCulture) ?? string.Empty;
                result[barcode] = new MikroProduct(
                    reader["StockCode"] is DBNull ? null : Convert.ToString(reader["StockCode"], CultureInfo.InvariantCulture),
                    reader["StockName"] is DBNull ? string.Empty : Convert.ToString(reader["StockName"], CultureInfo.InvariantCulture) ?? string.Empty,
                    reader["Price"] is DBNull ? null : Convert.ToDecimal(reader["Price"], CultureInfo.InvariantCulture),
                    reader["Quantity"] is DBNull ? null : Convert.ToDecimal(reader["Quantity"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["Blocked"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["UnitPointer"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["UnitName"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["SalesBlocked"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["OrderBlocked"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["GoodsAcceptanceBlocked"], CultureInfo.InvariantCulture),
                    reader["PriceUpdatedAt"] is DBNull ? null : Convert.ToDateTime(reader["PriceUpdatedAt"], CultureInfo.InvariantCulture),
                    reader["WarehouseUpdatedAt"] is DBNull ? null : Convert.ToDateTime(reader["WarehouseUpdatedAt"], CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return result;
        }
        finally
        {
            mikroReadGate.Release();
        }
    }

    internal static TrendyolGoPriceStockRow BuildRow(
        TrendyolProduct product,
        MikroProduct? mikro,
        int stockReserveQuantity = 0)
    {
        var price = mikro?.Price;
        int? warehouseQuantity = mikro?.Quantity is decimal rawQuantity
            ? NormalizeWarehouseQuantity(rawQuantity)
            : null;
        int? quantity = mikro?.Quantity is decimal availableQuantity
            ? CalculateSellableQuantity(availableQuantity, stockReserveQuantity)
            : null;
        var reason = string.IsNullOrWhiteSpace(product.Barcode) ? "Trendyol barkodu bos."
            : mikro?.StockCode is null ? "Mikro barkod eslesmesi yok."
            : mikro.Blocked ? "Urun Mikro'da pasif veya satisa kapali."
            : price is null or <= 0 ? "Mikro satis fiyati yok veya sifir."
            : quantity is null ? "Mikro stok miktari okunamadi."
            : null;
        var changed = reason is null && (product.Price != price || (product.Quantity ?? 0) != quantity);
        return new TrendyolGoPriceStockRow(
            product.Barcode, mikro?.StockCode, mikro?.StockName ?? product.Title,
            product.Price, product.Quantity, price, quantity,
            reason is not null ? "Skipped" : changed ? "Ready" : "Unchanged", reason,
            warehouseQuantity, stockReserveQuantity);
    }

    internal static int ResolveStockReserveQuantity(
        TrendyolGoOptions config,
        TrendyolGoStoreMappingOptions store)
    {
        var quantity = store.StockReserveQuantity ?? config.StockReserveQuantity;
        if (quantity is < 0 or > 100000)
        {
            throw new InvalidOperationException(
                $"Trendyol Go stock reserve for warehouse {store.WarehouseNo} must be between 0 and 100000.");
        }

        return quantity;
    }

    internal static int CalculateSellableQuantity(decimal rawQuantity, int stockReserveQuantity)
    {
        if (stockReserveQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stockReserveQuantity),
                "Stock reserve quantity cannot be negative.");
        }

        return Math.Max(0, NormalizeWarehouseQuantity(rawQuantity) - stockReserveQuantity);
    }

    private static int NormalizeWarehouseQuantity(decimal rawQuantity) =>
        (int)Math.Clamp(decimal.Floor(rawQuantity), 0, int.MaxValue);

    internal static TrendyolProduct ReadProduct(JsonElement product)
    {
        if (product.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Trendyol Go product row is invalid.");
        }

        return new TrendyolProduct(
            ReadString(product, "barcode") ?? string.Empty,
            ReadString(product, "title") ?? string.Empty,
            ReadDecimal(product, "sellingPrice"),
            ReadInt(product, "quantity"),
            ReadNestedLong(product, "brand", "id"),
            ReadNestedLong(product, "category", "id"),
            ReadInt(product, "vatRate"),
            ReadString(product, "stockCode"));
    }

    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private static decimal? ReadDecimal(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDecimal() : null;

    private static int? ReadInt(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32() : null;

    private static long? ReadLong(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt64() : null;

    private static long? ReadNestedLong(JsonElement value, string parentName, string name) =>
        value.TryGetProperty(parentName, out var parent) && parent.ValueKind == JsonValueKind.Object
            ? ReadLong(parent, name)
            : null;

    internal static bool ShouldAutoCreateMissingCatalog(bool enabled, Exception exception) =>
        enabled && IsMissingCatalogError(exception);

    internal static bool IsMissingCatalogError(Exception ex)
    {
        if (ex is not HttpRequestException)
        {
            return false;
        }

        var message = ex.Message;
        var referencesProduct = message.Contains("catalog", StringComparison.OrdinalIgnoreCase)
            || message.Contains("product", StringComparison.OrdinalIgnoreCase)
            || message.Contains("urun", StringComparison.OrdinalIgnoreCase)
            || message.Contains("ürün", StringComparison.OrdinalIgnoreCase);
        var reportsMissing = message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("bulunamadi", StringComparison.OrdinalIgnoreCase)
            || message.Contains("bulunamadı", StringComparison.OrdinalIgnoreCase)
            || message.Contains("bulunmuyor", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tanimli degil", StringComparison.OrdinalIgnoreCase)
            || message.Contains("tanımlı değil", StringComparison.OrdinalIgnoreCase)
            || message.Contains("mevcut degil", StringComparison.OrdinalIgnoreCase)
            || message.Contains("mevcut değil", StringComparison.OrdinalIgnoreCase);
        return referencesProduct && reportsMissing;
    }

    private static TrendyolGoDeferredPriceStockItem ToDeferredItem(
        string barcode,
        MikroProduct item,
        int quantity,
        int priceListNo) => new(
        barcode,
        item.StockCode!,
        item.Price!.Value,
        quantity,
        item.UnitPointer,
        item.UnitName,
        item.SalesBlocked,
        item.OrderBlocked,
        item.GoodsAcceptanceBlocked,
        item.PriceUpdatedAtUtc,
        item.WarehouseUpdatedAtUtc,
        priceListNo);

    private static TrendyolGoDeferredPriceStockItem ToDeferredItem(
        TrendyolGoDirectPriceStockItem item,
        int priceListNo) => new(
        item.Barcode,
        item.StockCode,
        item.SellingPrice,
        item.Quantity,
        item.UnitPointer,
        item.UnitName,
        item.SalesBlocked,
        item.OrderBlocked,
        item.GoodsAcceptanceBlocked,
        item.PriceLastUpDate,
        item.WarehouseLastUpDate,
        priceListNo);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    internal sealed record TrendyolProduct(
        string Barcode, string Title, decimal? Price, int? Quantity,
        long? BrandId = null, long? CategoryId = null, int? VatRate = null,
        string? StockCode = null);
    internal sealed record MikroProduct(
        string? StockCode, string StockName, decimal? Price, decimal? Quantity, bool Blocked,
        int UnitPointer = 1, string UnitName = "", int SalesBlocked = 0, int OrderBlocked = 0,
        int GoodsAcceptanceBlocked = 0, DateTime? PriceUpdatedAtUtc = null,
        DateTime? WarehouseUpdatedAtUtc = null);

    private sealed record CatalogPage(
        int TotalPages, long TotalElements, IReadOnlyCollection<TrendyolProduct> Products);
}
