namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

public interface ITrendyolGoAdministrationService
{
    Task<TrendyolGoAdministrationOverview> GetOverviewAsync(string? search, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TrendyolGoBranchPosSyncTaskDto>> ListBranchPosSyncTasksAsync(
        int take, CancellationToken cancellationToken);
    Task<TrendyolGoRuntimeSettingsDto> UpdateRuntimeSettingsAsync(
        TrendyolGoRuntimeSettingsUpdate request, string actor, CancellationToken cancellationToken);
    Task<TrendyolGoStoreSettingDto> UpsertStoreAsync(
        TrendyolGoStoreSettingUpdate request, string actor, CancellationToken cancellationToken);
    Task DeleteStoreAsync(long storeId, string actor, CancellationToken cancellationToken);
    Task<TrendyolGoCategorySyncResult> RefreshCategoriesAsync(CancellationToken cancellationToken);
    Task<TrendyolGoCategoryMappingDto> UpsertCategoryMappingAsync(
        TrendyolGoCategoryMappingUpdate request, string actor, CancellationToken cancellationToken);
    Task DeleteCategoryMappingAsync(Guid id, string actor, CancellationToken cancellationToken);
    Task<TrendyolGoProductCategoryOverrideDto> UpsertProductOverrideAsync(
        TrendyolGoProductCategoryOverrideUpdate request, string actor, CancellationToken cancellationToken);
    Task DeleteProductOverrideAsync(Guid id, string actor, CancellationToken cancellationToken);
}

public sealed record TrendyolGoAdministrationOverview(
    TrendyolGoRuntimeSettingsDto RuntimeSettings,
    IReadOnlyCollection<TrendyolGoStoreSettingDto> Stores,
    IReadOnlyCollection<TrendyolGoCategoryDto> Categories,
    IReadOnlyCollection<TrendyolGoCategoryMappingDto> CategoryMappings,
    IReadOnlyCollection<TrendyolGoProductCategoryOverrideDto> ProductOverrides,
    IReadOnlyCollection<TrendyolGoBranchPosSyncTaskDto> BranchPosSyncTasks,
    int ActiveCategoryCount,
    int ActiveLeafCategoryCount);

public sealed record TrendyolGoRuntimeSettingsDto(
    int PriceListNo,
    int PaymentPlanNo,
    int StockReserveQuantity,
    bool BranchPosPriceSyncEnabled,
    long DefaultBrandId,
    long DefaultCategoryId,
    string Source,
    string? UpdatedBy,
    DateTime? UpdatedAtUtc);

public sealed record TrendyolGoRuntimeSettingsUpdate(
    int PriceListNo,
    int PaymentPlanNo,
    int StockReserveQuantity,
    bool BranchPosPriceSyncEnabled);

public sealed record TrendyolGoStoreSettingDto(
    long StoreId,
    int WarehouseNo,
    string StoreName,
    int? StockReserveQuantity,
    bool IsActive,
    string Source,
    string? UpdatedBy,
    DateTime? UpdatedAtUtc);

public sealed record TrendyolGoStoreSettingUpdate(
    long StoreId,
    int WarehouseNo,
    string StoreName,
    int? StockReserveQuantity,
    bool IsActive = true);

public sealed record TrendyolGoCategoryDto(
    long CategoryId,
    string Name,
    long? ParentCategoryId,
    string Path,
    bool IsLeaf,
    bool IsActive,
    DateTime SyncedAtUtc);

public sealed record TrendyolGoCategorySyncResult(int ReadCount, int ActiveCount, DateTime SyncedAtUtc);

public sealed record TrendyolGoCategoryMappingDto(
    Guid Id,
    string MikroCategoryCode,
    string? MikroMainGroupCode,
    long CategoryId,
    string? CategoryName,
    string? CategoryPath,
    bool IsActive,
    string UpdatedBy,
    DateTime UpdatedAtUtc);

public sealed record TrendyolGoCategoryMappingUpdate(
    Guid? Id,
    string MikroCategoryCode,
    string? MikroMainGroupCode,
    long CategoryId);

public sealed record TrendyolGoProductCategoryOverrideDto(
    Guid Id,
    string StockCode,
    long CategoryId,
    string? CategoryName,
    string? CategoryPath,
    bool IsActive,
    string UpdatedBy,
    DateTime UpdatedAtUtc);

public sealed record TrendyolGoProductCategoryOverrideUpdate(Guid? Id, string StockCode, long CategoryId);

public sealed record TrendyolGoBranchPosSyncTaskDto(
    Guid Id,
    long StoreId,
    int WarehouseNo,
    string StoreName,
    string Status,
    int AttemptCount,
    DateTime NextAttemptAtUtc,
    int ItemCount,
    int BlockedItemCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record TrendyolGoWorkflowStatusDto(
    IReadOnlyCollection<TrendyolGoBatchWorkflowDto> BatchWorkflows,
    IReadOnlyCollection<TrendyolGoBranchPosSyncTaskDto> BranchPosSyncTasks);
