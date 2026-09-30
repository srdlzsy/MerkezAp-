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
    TrendyolGoBranchPosPriceSyncService branchPosPriceSync,
    TrendyolGoPriceStockSnapshotCoordinator snapshotCoordinator,
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
                storeId, store.WarehouseNo, store.StoreName,
                0, 0, string.Empty, 0, 0, 0,
                normalizedView, 0, [], GetSnapshotStatus(state), true,
                null, state.RefreshStartedAtUtc, state.RefreshCompletedAtUtc,
                state.RefreshError));
        }

        var visibleRows = FilterRows(snapshot.Rows, normalizedView);
        return Task.FromResult(new TrendyolGoPriceStockPreview(
            snapshot.StoreId, snapshot.WarehouseNo, snapshot.StoreName,
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
        long storeId, string previewHash, bool sendAll,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previewHash) || barcodes.Count > 10000 ||
            barcodes.Any(string.IsNullOrWhiteSpace) ||
            barcodes.Count != barcodes.Distinct(StringComparer.Ordinal).Count() ||
            (!sendAll && barcodes.Count == 0))
        {
            throw new ArgumentException("A preview hash and either sendAll or distinct barcodes are required.");
        }

        GetConfiguredStore(storeId);
        var snapshot = snapshotCoordinator.GetState(storeId).Snapshot
            ?? throw new InvalidOperationException(
                "Price-stock preview is still preparing. Wait for the snapshot before sending.");
        if (!string.Equals(snapshot.PreviewHash, previewHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Price, stock or Trendyol product data changed. Refresh the preview before sending.");
        }

        var selected = sendAll
            ? snapshot.Rows.Where(item => item.Status == "Ready").ToArray()
            : ResolveSelectedRows(snapshot.Rows, barcodes);
        if (selected.Length == 0)
        {
            throw new ArgumentException("There are no ready price-stock rows to send.");
        }

        var config = options.CurrentValue;
        var responses = new List<JsonElement?>();
        foreach (var batch in selected.Chunk(1000))
        {
            var mikro = await ReadMikroProductsAsync(
                snapshot.WarehouseNo, batch.Select(item => item.Barcode).ToArray(),
                config.PriceListNo, config.PaymentPlanNo, cancellationToken);
            EnsureSelectedValuesAreCurrent(batch, snapshot.Products, mikro);
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
            responses.Add(await trendyol.UpdatePriceAndInventoryAsync(payload, cancellationToken));
            var branchPosItems = batch
                .Select(item => mikro.GetValueOrDefault(item.Barcode))
                .Where(item => item is not null)
                .Select(item => item!)
                .Where(item => item.StockCode is not null && item.Price is not null)
                .Select(item => new BranchPosPriceItem(
                    item.StockCode!, item.Price!.Value, item.UnitPointer, item.UnitName,
                    item.SalesBlocked, item.OrderBlocked, item.GoodsAcceptanceBlocked,
                    item.PriceUpdatedAtUtc, item.WarehouseUpdatedAtUtc, config.PriceListNo))
                .ToArray();
            await branchPosPriceSync.EnqueueAsync(storeId, snapshot.WarehouseNo, branchPosItems, cancellationToken);
        }

        snapshotCoordinator.Invalidate(storeId);
        return new TrendyolGoPriceStockDispatch(
            storeId, snapshot.WarehouseNo, selected.Length, responses.Count, responses);
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
            var row = BuildRow(product, mikro.GetValueOrDefault(product.Barcode));
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
            storeId, store.WarehouseNo, store.StoreName,
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
        IReadOnlyDictionary<string, MikroProduct> mikro)
    {
        foreach (var selectedRow in selected)
        {
            var product = products.Single(item => item.Barcode == selectedRow.Barcode);
            var currentRow = BuildRow(product, mikro.GetValueOrDefault(selectedRow.Barcode));
            if (currentRow.Status != "Ready" || currentRow.MikroPrice != selectedRow.MikroPrice ||
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

    internal static TrendyolGoPriceStockRow BuildRow(TrendyolProduct product, MikroProduct? mikro)
    {
        var price = mikro?.Price;
        var quantity = mikro?.Quantity is decimal rawQuantity
            ? (int?)Math.Clamp(decimal.Floor(rawQuantity), 0, int.MaxValue)
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
            reason is not null ? "Skipped" : changed ? "Ready" : "Unchanged", reason);
    }

    private static TrendyolProduct ReadProduct(JsonElement product)
    {
        if (product.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Trendyol Go product row is invalid.");
        }

        return new TrendyolProduct(
            ReadString(product, "barcode") ?? string.Empty,
            ReadString(product, "title") ?? string.Empty,
            ReadDecimal(product, "sellingPrice"),
            ReadInt(product, "quantity"));
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

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    internal sealed record TrendyolProduct(string Barcode, string Title, decimal? Price, int? Quantity);
    internal sealed record MikroProduct(
        string? StockCode, string StockName, decimal? Price, decimal? Quantity, bool Blocked,
        int UnitPointer = 1, string UnitName = "", int SalesBlocked = 0, int OrderBlocked = 0,
        int GoodsAcceptanceBlocked = 0, DateTime? PriceUpdatedAtUtc = null,
        DateTime? WarehouseUpdatedAtUtc = null);

    private sealed record CatalogPage(
        int TotalPages, long TotalElements, IReadOnlyCollection<TrendyolProduct> Products);
}
