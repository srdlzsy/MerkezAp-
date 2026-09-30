using System.Text.Json;

namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

public interface ITrendyolGoPriceStockWorkbench
{
    Task<TrendyolGoPriceStockPreview> PreviewAsync(
        long storeId, string view, CancellationToken cancellationToken);

    TrendyolGoPriceStockRefreshStatus RequestRefresh(long storeId);

    Task<TrendyolGoPriceStockDispatch> DispatchAsync(
        long storeId, string previewHash, bool sendAll,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);
}

public sealed record TrendyolGoPriceStockPreview(
    long StoreId, int WarehouseNo, string StoreName,
    int TotalPages, long TotalElements, string PreviewHash,
    int ReadyCount, int UnchangedCount, int SkippedCount,
    string View, int VisibleCount, IReadOnlyCollection<TrendyolGoPriceStockRow> Items,
    string SnapshotStatus, bool IsStale, DateTime? GeneratedAtUtc,
    DateTime? RefreshStartedAtUtc, DateTime? RefreshCompletedAtUtc,
    string? RefreshError);

public sealed record TrendyolGoPriceStockRefreshStatus(
    long StoreId, int WarehouseNo, string StoreName,
    string SnapshotStatus, bool RefreshAccepted, bool HasSnapshot,
    bool IsStale, DateTime? GeneratedAtUtc, DateTime? RefreshStartedAtUtc,
    DateTime? RefreshCompletedAtUtc, string? RefreshError);

public sealed record TrendyolGoPriceStockRow(
    string Barcode, string? StockCode, string ProductName,
    decimal? TrendyolPrice, int? TrendyolQuantity,
    decimal? MikroPrice, int? MikroQuantity,
    string Status, string? Reason);

public sealed record TrendyolGoPriceStockDispatch(
    long StoreId, int WarehouseNo, int SentCount, int BatchCount,
    IReadOnlyCollection<JsonElement?> UpstreamResponses);
