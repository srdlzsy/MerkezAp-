using System.Text.Json;

namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

public interface ITrendyolGoCatalogWorkbench
{
    Task<TrendyolGoCatalogPreview> PreviewAsync(
        long storeId, string view, string? search, int take, CancellationToken cancellationToken);

    Task<TrendyolGoCatalogDispatch> DispatchAsync(
        long storeId, string previewHash, bool sendAll,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);
}

public sealed record TrendyolGoCatalogPreview(
    long StoreId, int WarehouseNo, string StoreName, string PreviewHash,
    int TotalCount, int ExistingCount, int ReadyCount, int MappingRequiredCount,
    int SkippedCount, string View, int VisibleCount,
    IReadOnlyCollection<TrendyolGoCatalogRow> Items);

public sealed record TrendyolGoCatalogRow(
    string StockCode, string ProductName, string? Barcode,
    string? MikroBrandCode, long? TrendyolBrandId,
    string? MikroCategoryCode, long? TrendyolCategoryId,
    int? VatRate, decimal? MikroPrice, string Status, string? Reason);

public sealed record TrendyolGoCatalogDispatch(
    long StoreId, int WarehouseNo, int SentCount, int BatchCount,
    string NextStep, IReadOnlyCollection<JsonElement?> UpstreamResponses);
