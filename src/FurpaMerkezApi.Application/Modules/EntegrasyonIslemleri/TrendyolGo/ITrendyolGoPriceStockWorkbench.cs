using System.Text.Json;

namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

public interface ITrendyolGoPriceStockWorkbench
{
    Task<TrendyolGoPriceStockPreview> PreviewAsync(
        long storeId, int page, int size, string view, CancellationToken cancellationToken);

    Task<TrendyolGoPriceStockDispatch> DispatchAsync(
        long storeId, int page, int size, string previewHash,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);
}

public sealed record TrendyolGoPriceStockPreview(
    long StoreId, int WarehouseNo, string StoreName, int Page, int Size,
    int TotalPages, long TotalElements, string PreviewHash,
    int ReadyCount, int UnchangedCount, int SkippedCount,
    string View, int VisibleCount, IReadOnlyCollection<TrendyolGoPriceStockRow> Items);

public sealed record TrendyolGoPriceStockRow(
    string Barcode, string? StockCode, string ProductName,
    decimal? TrendyolPrice, int? TrendyolQuantity,
    decimal? MikroPrice, int? MikroQuantity,
    string Status, string? Reason);

public sealed record TrendyolGoPriceStockDispatch(
    long StoreId, int WarehouseNo, int SentCount, JsonElement? UpstreamResponse);
