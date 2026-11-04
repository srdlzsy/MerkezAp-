using System.Text.Json;

namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

public interface ITrendyolGoPriceStockWorkbench
{
    Task<TrendyolGoPriceStockPreview> PreviewAsync(
        long storeId, string view, CancellationToken cancellationToken);

    TrendyolGoPriceStockRefreshStatus RequestRefresh(long storeId);

    Task<TrendyolGoPriceStockDispatch> DispatchAsync(
        long storeId, string previewHash, TrendyolGoPriceStockDispatchMode mode,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken);

    Task<TrendyolGoDirectPriceStockPreview> DirectPreviewAsync(
        TrendyolGoDirectPriceStockQueryRequest request,
        CancellationToken cancellationToken);

    Task<TrendyolGoDirectPriceStockDispatchResult> DirectDispatchAsync(
        TrendyolGoDirectPriceStockDispatchRequest request,
        CancellationToken cancellationToken);
}

public enum TrendyolGoPriceStockDispatchMode
{
    Selected,
    Changed,
    AllExisting
}

public sealed record TrendyolGoPriceStockPreview(
    long StoreId, int WarehouseNo, string StoreName, int StockReserveQuantity,
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
    string Status, string? Reason,
    int? WarehouseQuantity = null, int StockReserveQuantity = 0);

public sealed record TrendyolGoPriceStockDispatch(
    long StoreId, int WarehouseNo, string Mode, int SentCount, int BatchCount,
    IReadOnlyCollection<JsonElement?> UpstreamResponses);

public sealed record TrendyolGoDirectPriceStockQueryRequest(
    long StoreId,
    string Mode = "changed",
    DateTime? SinceDate = null,
    bool? OnlyFlaggedChanged = null,
    string? Search = null,
    int Take = 500);

public sealed record TrendyolGoDirectPriceStockItem(
    string Barcode,
    string StockCode,
    string ProductName,
    decimal SellingPrice,
    int Quantity,
    bool PriceChangedFlag,
    DateTime? PriceLastUpDate,
    int WarehouseNo,
    int UnitPointer = 1,
    string UnitName = "",
    int SalesBlocked = 0,
    int OrderBlocked = 0,
    int GoodsAcceptanceBlocked = 0,
    DateTime? WarehouseLastUpDate = null,
    int WarehouseQuantity = 0,
    int StockReserveQuantity = 0);

public sealed record TrendyolGoDirectPriceStockPreview(
    long StoreId,
    int WarehouseNo,
    string StoreName,
    int StockReserveQuantity,
    string Mode,
    DateTime? SinceDate,
    int TotalCount,
    IReadOnlyCollection<TrendyolGoDirectPriceStockItem> Items);

public sealed record TrendyolGoDirectPriceStockDispatchRequest(
    IReadOnlyCollection<long>? StoreIds = null,
    bool AllStores = false,
    string Mode = "changed",
    DateTime? SinceDate = null,
    bool? OnlyFlaggedChanged = null,
    string? Search = null,
    IReadOnlyCollection<string>? Barcodes = null,
    bool AutoCreateMissingCatalog = true);

public sealed record TrendyolGoDirectStoreDispatchResult(
    long StoreId,
    int WarehouseNo,
    string StoreName,
    int ItemCount,
    int BatchCount,
    bool Success,
    string? ErrorMessage,
    IReadOnlyCollection<string> BatchRequestIds,
    int CatalogCreatedCount = 0,
    IReadOnlyCollection<string>? CatalogBatchRequestIds = null,
    IReadOnlyCollection<Guid>? FollowUpTaskIds = null,
    string WorkflowStatus = "Queued");

public sealed record TrendyolGoDirectPriceStockDispatchResult(
    int TargetStoresCount,
    int TotalItemsDispatched,
    IReadOnlyCollection<TrendyolGoDirectStoreDispatchResult> StoreResults,
    int TotalCatalogCreated = 0);

public interface ITrendyolGoBatchWorkflowQueries
{
    Task<IReadOnlyCollection<TrendyolGoBatchWorkflowDto>> ListAsync(
        int take,
        CancellationToken cancellationToken);
}

public sealed record TrendyolGoBatchWorkflowDto(
    Guid Id,
    string Kind,
    string BatchRequestId,
    long StoreId,
    int WarehouseNo,
    string Status,
    int AttemptCount,
    DateTime NextAttemptAtUtc,
    string? LastError,
    string? ResultSummary,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);
