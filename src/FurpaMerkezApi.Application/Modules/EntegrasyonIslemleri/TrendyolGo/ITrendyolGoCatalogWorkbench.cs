using System.Text.Json;

namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

public interface ITrendyolGoCatalogWorkbench
{
    Task<TrendyolGoCatalogPreview> PreviewAsync(
        long storeId, string view, string? search, int take, CancellationToken cancellationToken);

    Task<TrendyolGoCatalogDispatch> DispatchAsync(
        long storeId, string previewHash, bool sendAll,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);

    Task<TrendyolGoCatalogAutoCreateResult> AutoCreateProductsAsync(
        long storeId,
        IReadOnlyCollection<string> barcodes,
        CancellationToken cancellationToken);
}

public sealed record TrendyolGoCatalogAutoCreateResult(
    int CreatedCount,
    IReadOnlyCollection<string> BatchRequestIds,
    string? ErrorMessage,
    IReadOnlyCollection<TrendyolGoCatalogBatchSubmission>? BatchSubmissions = null);

public sealed record TrendyolGoCatalogBatchSubmission(
    string BatchRequestId,
    IReadOnlyCollection<string> Barcodes);

public sealed record TrendyolGoCatalogPreview(
    long StoreId, int WarehouseNo, string StoreName, string PreviewHash,
    int TotalCount, int ExistingCount, int ReadyCount, int MappingRequiredCount,
    int SkippedCount, string View, int VisibleCount,
    IReadOnlyCollection<TrendyolGoCatalogRow> Items);

public sealed record TrendyolGoCatalogRow(
    string StockCode, string ProductName, string? Barcode,
    string? MikroBrandCode, long? TrendyolBrandId,
    string? MikroCategoryCode, string? MikroMainGroupCode, long? TrendyolCategoryId,
    string? CategoryMappingSource,
    int? VatRate, decimal? MikroPrice, string Status, string? Reason)
{
    public TrendyolGoCatalogRow(
        string stockCode,
        string productName,
        string? barcode,
        string? mikroBrandCode,
        long? trendyolBrandId,
        string? mikroCategoryCode,
        long? trendyolCategoryId,
        int? vatRate,
        decimal? mikroPrice,
        string status,
        string? reason)
        : this(
            stockCode,
            productName,
            barcode,
            mikroBrandCode,
            trendyolBrandId,
            mikroCategoryCode,
            null,
            trendyolCategoryId,
            null,
            vatRate,
            mikroPrice,
            status,
            reason)
    {
    }
}

public sealed record TrendyolGoCatalogDispatch(
    long StoreId, int WarehouseNo, int SentCount, int BatchCount,
    string NextStep, IReadOnlyCollection<JsonElement?> UpstreamResponses);
