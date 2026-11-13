using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoAdministrationService(
    AuthDbContext authDbContext,
    ITrendyolGoIntegrationService integrationService,
    TrendyolGoRuntimeSettingsProvider runtimeSettingsProvider,
    IOptionsMonitor<TrendyolGoOptions> options,
    IClock clock) : ITrendyolGoAdministrationService
{
    public async Task<IReadOnlyCollection<TrendyolGoBranchPosSyncTaskDto>> ListBranchPosSyncTasksAsync(
        int take,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 500);
        var configuredStores = options.CurrentValue.Stores
            .Where(item => item.StoreId > 0)
            .ToDictionary(item => item.StoreId, item => item.StoreName);
        var databaseStores = await authDbContext.TrendyolGoStoreSettings
            .AsNoTracking()
            .Select(item => new { item.StoreId, item.StoreName })
            .ToDictionaryAsync(item => item.StoreId, item => item.StoreName, cancellationToken);
        var tasks = await authDbContext.TrendyolGoBranchPosPriceSyncTasks
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .ToArrayAsync(cancellationToken);

        return tasks.Select(item =>
        {
            var payloadSummary = ReadBranchPosPayloadSummary(item.PayloadJson);
            var storeName = databaseStores.GetValueOrDefault(item.StoreId)
                ?? configuredStores.GetValueOrDefault(item.StoreId)
                ?? $"Magaza {item.StoreId}";
            return new TrendyolGoBranchPosSyncTaskDto(
                item.Id,
                item.StoreId,
                item.WarehouseNo,
                storeName,
                item.Status.ToString(),
                item.AttemptCount,
                item.NextAttemptAtUtc,
                payloadSummary.ItemCount,
                payloadSummary.BlockedItemCount,
                item.LastError,
                item.CreatedAtUtc,
                item.CompletedAtUtc);
        }).ToArray();
    }

    public async Task<TrendyolGoAdministrationOverview> GetOverviewAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        var configured = options.CurrentValue;
        var databaseStores = await authDbContext.TrendyolGoStoreSettings
            .AsNoTracking()
            .ToDictionaryAsync(item => item.StoreId, cancellationToken);
        var configuredStores = configured.Stores
            .Where(item => item.StoreId > 0 && item.WarehouseNo > 0)
            .ToDictionary(item => item.StoreId);
        var categoriesQuery = authDbContext.TrendyolGoCategories.AsNoTracking().Where(item => item.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            categoriesQuery = categoriesQuery.Where(item =>
                item.Name.Contains(term) || item.Path.Contains(term) || item.CategoryId.ToString().Contains(term));
        }

        var categories = await categoriesQuery
            .OrderBy(item => item.Path)
            .Take(2000)
            .Select(item => new TrendyolGoCategoryDto(
                item.CategoryId, item.Name, item.ParentCategoryId, item.Path,
                item.IsLeaf, item.IsActive, item.SyncedAtUtc))
            .ToArrayAsync(cancellationToken);
        var categoryNames = await authDbContext.TrendyolGoCategories
            .AsNoTracking()
            .ToDictionaryAsync(item => item.CategoryId, cancellationToken);
        var mappings = await authDbContext.TrendyolGoCategoryMappings
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.MikroCategoryCode)
            .ThenBy(item => item.MikroMainGroupCode)
            .ToArrayAsync(cancellationToken);
        var productOverrides = await authDbContext.TrendyolGoProductCategoryOverrides
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.StockCode)
            .ToArrayAsync(cancellationToken);
        var branchPosTaskEntities = await authDbContext.TrendyolGoBranchPosPriceSyncTasks
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        var runtimeStoreNames = runtime.Stores.ToDictionary(item => item.StoreId, item => item.StoreName);
        var branchPosTasks = branchPosTaskEntities
            .Select(item =>
            {
                var payloadSummary = ReadBranchPosPayloadSummary(item.PayloadJson);
                var storeName = runtimeStoreNames.GetValueOrDefault(item.StoreId)
                    ?? databaseStores.GetValueOrDefault(item.StoreId)?.StoreName
                    ?? configuredStores.GetValueOrDefault(item.StoreId)?.StoreName
                    ?? $"Magaza {item.StoreId}";
                return new TrendyolGoBranchPosSyncTaskDto(
                    item.Id, item.StoreId, item.WarehouseNo, storeName, item.Status.ToString(),
                    item.AttemptCount, item.NextAttemptAtUtc, payloadSummary.ItemCount,
                    payloadSummary.BlockedItemCount, item.LastError, item.CreatedAtUtc, item.CompletedAtUtc);
            })
            .ToArray();
        var activeCategoryCount = await authDbContext.TrendyolGoCategories.CountAsync(item => item.IsActive, cancellationToken);
        var activeLeafCategoryCount = await authDbContext.TrendyolGoCategories.CountAsync(
            item => item.IsActive && item.IsLeaf, cancellationToken);

        return new TrendyolGoAdministrationOverview(
            new TrendyolGoRuntimeSettingsDto(
                runtime.PriceListNo,
                runtime.PaymentPlanNo,
                runtime.StockReserveQuantity,
                runtime.BranchPosPriceSyncEnabled,
                configured.Catalog.DefaultBrandId,
                configured.Catalog.DefaultCategoryId,
                runtime.Source,
                runtime.UpdatedBy,
                runtime.UpdatedAtUtc),
            configuredStores.Keys
                .Concat(databaseStores.Keys)
                .Distinct()
                .Select(storeId =>
                {
                    if (databaseStores.TryGetValue(storeId, out var databaseStore))
                    {
                        return new TrendyolGoStoreSettingDto(
                            databaseStore.StoreId,
                            databaseStore.WarehouseNo,
                            databaseStore.StoreName,
                            databaseStore.StockReserveQuantity,
                            databaseStore.IsActive,
                            "Database",
                            databaseStore.UpdatedBy,
                            databaseStore.UpdatedAtUtc);
                    }

                    var configuredStore = configuredStores[storeId];
                    return new TrendyolGoStoreSettingDto(
                        configuredStore.StoreId,
                        configuredStore.WarehouseNo,
                        configuredStore.StoreName,
                        configuredStore.StockReserveQuantity,
                        true,
                        "Configuration",
                        null,
                        null);
                })
                .OrderBy(item => item.WarehouseNo)
                .ToArray(),
            categories,
            mappings.Select(item => ToDto(item, categoryNames)).ToArray(),
            productOverrides.Select(item => ToDto(item, categoryNames)).ToArray(),
            branchPosTasks,
            activeCategoryCount,
            activeLeafCategoryCount);
    }

    public async Task<TrendyolGoRuntimeSettingsDto> UpdateRuntimeSettingsAsync(
        TrendyolGoRuntimeSettingsUpdate request,
        string actor,
        CancellationToken cancellationToken)
    {
        var entity = await authDbContext.TrendyolGoRuntimeSettings
            .SingleOrDefaultAsync(item => item.Id == TrendyolGoRuntimeSetting.SingletonId, cancellationToken);
        if (entity is null)
        {
            entity = new TrendyolGoRuntimeSetting(
                request.PriceListNo, request.PaymentPlanNo, request.StockReserveQuantity,
                request.BranchPosPriceSyncEnabled, actor, clock.UtcNow);
            authDbContext.TrendyolGoRuntimeSettings.Add(entity);
        }
        else
        {
            entity.Update(
                request.PriceListNo, request.PaymentPlanNo, request.StockReserveQuantity,
                request.BranchPosPriceSyncEnabled, actor, clock.UtcNow);
        }

        await authDbContext.SaveChangesAsync(cancellationToken);
        return new TrendyolGoRuntimeSettingsDto(
            entity.PriceListNo, entity.PaymentPlanNo, entity.StockReserveQuantity,
            entity.BranchPosPriceSyncEnabled, options.CurrentValue.Catalog.DefaultBrandId,
            options.CurrentValue.Catalog.DefaultCategoryId, "Database", entity.UpdatedBy, entity.UpdatedAtUtc);
    }

    public async Task<TrendyolGoStoreSettingDto> UpsertStoreAsync(
        TrendyolGoStoreSettingUpdate request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (request.StoreId <= 0) throw new ArgumentOutOfRangeException(nameof(request.StoreId));
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        if (request.IsActive && runtime.Stores.Any(item =>
                item.StoreId != request.StoreId && item.WarehouseNo == request.WarehouseNo))
        {
            throw new InvalidOperationException($"Depo {request.WarehouseNo} baska bir Trendyol Go magazasina bagli.");
        }

        var entity = await authDbContext.TrendyolGoStoreSettings
            .SingleOrDefaultAsync(item => item.StoreId == request.StoreId, cancellationToken);
        if (entity is null)
        {
            entity = new TrendyolGoStoreSetting(
                request.StoreId, request.WarehouseNo, request.StoreName,
                request.StockReserveQuantity, actor, clock.UtcNow);
            if (!request.IsActive)
            {
                entity.Update(request.WarehouseNo, request.StoreName, request.StockReserveQuantity, false, actor, clock.UtcNow);
            }
            authDbContext.TrendyolGoStoreSettings.Add(entity);
        }
        else
        {
            entity.Update(
                request.WarehouseNo, request.StoreName, request.StockReserveQuantity,
                request.IsActive, actor, clock.UtcNow);
        }

        await authDbContext.SaveChangesAsync(cancellationToken);
        return new TrendyolGoStoreSettingDto(
            entity.StoreId, entity.WarehouseNo, entity.StoreName, entity.StockReserveQuantity,
            entity.IsActive, "Database", entity.UpdatedBy, entity.UpdatedAtUtc);
    }

    public async Task DeleteStoreAsync(long storeId, string actor, CancellationToken cancellationToken)
    {
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        var current = runtime.Stores.SingleOrDefault(item => item.StoreId == storeId)
            ?? throw new KeyNotFoundException($"Trendyol Go magazasi {storeId} bulunamadi.");
        var entity = await authDbContext.TrendyolGoStoreSettings
            .SingleOrDefaultAsync(item => item.StoreId == storeId, cancellationToken);
        if (entity is null)
        {
            entity = new TrendyolGoStoreSetting(
                current.StoreId, current.WarehouseNo, current.StoreName,
                current.StockReserveQuantity, actor, clock.UtcNow);
            authDbContext.TrendyolGoStoreSettings.Add(entity);
        }

        entity.Update(current.WarehouseNo, current.StoreName, current.StockReserveQuantity, false, actor, clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TrendyolGoCategorySyncResult> RefreshCategoriesAsync(CancellationToken cancellationToken)
    {
        var response = await integrationService.ListCategoriesAsync(cancellationToken);
        var parsed = ParseCategories(response);
        if (parsed.Count == 0)
        {
            throw new InvalidOperationException("Trendyol Go kategori servisi okunabilir kategori dondurmedi.");
        }

        var now = clock.UtcNow;
        var existing = await authDbContext.TrendyolGoCategories.ToDictionaryAsync(item => item.CategoryId, cancellationToken);
        var incomingIds = parsed.Select(item => item.CategoryId).ToHashSet();
        foreach (var item in parsed)
        {
            if (existing.TryGetValue(item.CategoryId, out var entity))
            {
                entity.Update(item.Name, item.ParentCategoryId, item.Path, item.IsLeaf, item.IsActive, now);
            }
            else
            {
                authDbContext.TrendyolGoCategories.Add(new TrendyolGoCategory(
                    item.CategoryId, item.Name, item.ParentCategoryId, item.Path,
                    item.IsLeaf, item.IsActive, now));
            }
        }

        foreach (var stale in existing.Values.Where(item => !incomingIds.Contains(item.CategoryId)))
        {
            stale.Update(stale.Name, stale.ParentCategoryId, stale.Path, stale.IsLeaf, false, now);
        }

        await authDbContext.SaveChangesAsync(cancellationToken);
        return new TrendyolGoCategorySyncResult(parsed.Count, parsed.Count(item => item.IsActive), now);
    }

    public async Task<TrendyolGoCategoryMappingDto> UpsertCategoryMappingAsync(
        TrendyolGoCategoryMappingUpdate request,
        string actor,
        CancellationToken cancellationToken)
    {
        var category = await RequireLeafCategoryAsync(request.CategoryId, cancellationToken);
        var key = TrendyolGoCategoryMapping.BuildKey(request.MikroCategoryCode, request.MikroMainGroupCode);
        TrendyolGoCategoryMapping? entity = null;
        if (request.Id is Guid id && id != Guid.Empty)
        {
            entity = await authDbContext.TrendyolGoCategoryMappings.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null)
            {
                throw new KeyNotFoundException($"Kategori eslemesi {id} bulunamadi.");
            }

            var keyOwnedByAnotherRule = await authDbContext.TrendyolGoCategoryMappings
                .AnyAsync(item => item.Id != id && item.MappingKey == key, cancellationToken);
            if (keyOwnedByAnotherRule)
            {
                throw new InvalidOperationException(
                    "Ayni Mikro kategori ve ana grup icin baska bir Trendyol Go kategori eslemesi zaten var.");
            }
        }
        else
        {
            entity = await authDbContext.TrendyolGoCategoryMappings
                .SingleOrDefaultAsync(item => item.MappingKey == key, cancellationToken);
        }

        if (entity is null)
        {
            entity = new TrendyolGoCategoryMapping(
                Guid.NewGuid(), request.MikroCategoryCode, request.MikroMainGroupCode,
                request.CategoryId, actor, clock.UtcNow);
            authDbContext.TrendyolGoCategoryMappings.Add(entity);
        }
        else
        {
            entity.Update(
                request.MikroCategoryCode, request.MikroMainGroupCode,
                request.CategoryId, actor, clock.UtcNow);
        }

        await authDbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity, new Dictionary<long, TrendyolGoCategory> { [category.CategoryId] = category });
    }

    public async Task DeleteCategoryMappingAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var entity = await authDbContext.TrendyolGoCategoryMappings.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Kategori eslemesi {id} bulunamadi.");
        entity.Deactivate(actor, clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TrendyolGoProductCategoryOverrideDto> UpsertProductOverrideAsync(
        TrendyolGoProductCategoryOverrideUpdate request,
        string actor,
        CancellationToken cancellationToken)
    {
        var category = await RequireLeafCategoryAsync(request.CategoryId, cancellationToken);
        var stockCode = request.StockCode?.Trim();
        if (string.IsNullOrWhiteSpace(stockCode)) throw new ArgumentException("Stok kodu zorunludur.");
        TrendyolGoProductCategoryOverride? entity = null;
        if (request.Id is Guid id && id != Guid.Empty)
        {
            entity = await authDbContext.TrendyolGoProductCategoryOverrides.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null)
            {
                throw new KeyNotFoundException($"Urun kategori istisnasi {id} bulunamadi.");
            }

            var stockCodeOwnedByAnotherOverride = await authDbContext.TrendyolGoProductCategoryOverrides
                .AnyAsync(item => item.Id != id && item.StockCode == stockCode, cancellationToken);
            if (stockCodeOwnedByAnotherOverride)
            {
                throw new InvalidOperationException(
                    $"Stok kodu {stockCode} icin baska bir Trendyol Go kategori istisnasi zaten var.");
            }
        }
        else
        {
            entity = await authDbContext.TrendyolGoProductCategoryOverrides
                .SingleOrDefaultAsync(item => item.StockCode == stockCode, cancellationToken);
        }

        if (entity is null)
        {
            entity = new TrendyolGoProductCategoryOverride(Guid.NewGuid(), stockCode, request.CategoryId, actor, clock.UtcNow);
            authDbContext.TrendyolGoProductCategoryOverrides.Add(entity);
        }
        else
        {
            entity.Update(stockCode, request.CategoryId, actor, clock.UtcNow);
        }

        await authDbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity, new Dictionary<long, TrendyolGoCategory> { [category.CategoryId] = category });
    }

    public async Task DeleteProductOverrideAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var entity = await authDbContext.TrendyolGoProductCategoryOverrides.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Urun kategori istisnasi {id} bulunamadi.");
        entity.Deactivate(actor, clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TrendyolGoCategory> RequireLeafCategoryAsync(long categoryId, CancellationToken cancellationToken)
    {
        var category = await authDbContext.TrendyolGoCategories
            .SingleOrDefaultAsync(item => item.CategoryId == categoryId && item.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException($"Aktif Trendyol Go kategorisi {categoryId} bulunamadi. Kategori agacini yenileyin.");
        if (!category.IsLeaf)
        {
            throw new InvalidOperationException($"Kategori {categoryId} secilebilir son kategori (leaf) degil.");
        }

        return category;
    }

    private static TrendyolGoCategoryMappingDto ToDto(
        TrendyolGoCategoryMapping item,
        IReadOnlyDictionary<long, TrendyolGoCategory> categories)
    {
        categories.TryGetValue(item.CategoryId, out var category);
        return new TrendyolGoCategoryMappingDto(
            item.Id, item.MikroCategoryCode, item.MikroMainGroupCode,
            item.CategoryId, category?.Name, category?.Path, item.IsActive,
            item.UpdatedBy, item.UpdatedAtUtc);
    }

    private static TrendyolGoProductCategoryOverrideDto ToDto(
        TrendyolGoProductCategoryOverride item,
        IReadOnlyDictionary<long, TrendyolGoCategory> categories)
    {
        categories.TryGetValue(item.CategoryId, out var category);
        return new TrendyolGoProductCategoryOverrideDto(
            item.Id, item.StockCode, item.CategoryId, category?.Name, category?.Path,
            item.IsActive, item.UpdatedBy, item.UpdatedAtUtc);
    }

    internal static IReadOnlyCollection<ParsedCategory> ParseCategories(JsonElement root)
    {
        var parsed = new Dictionary<long, ParsedCategory>();
        ParseNode(root, null, [], parsed);
        return parsed.Values.OrderBy(item => item.Path).ToArray();
    }

    private static void ParseNode(
        JsonElement node,
        long? parentCategoryId,
        IReadOnlyCollection<string> parentPath,
        IDictionary<long, ParsedCategory> parsed)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                ParseNode(child, parentCategoryId, parentPath, parsed);
            }
            return;
        }

        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var id = ReadLong(node, "id", "categoryId");
        var name = ReadString(node, "name", "categoryName", "title");
        var children = FindChildren(node);
        var currentParentId = parentCategoryId;
        var currentPath = parentPath;
        if (id is > 0 && !string.IsNullOrWhiteSpace(name))
        {
            var pathParts = parentPath.Append(name.Trim()).ToArray();
            var isLeaf = ReadBoolean(node, "leaf", "isLeaf") ?? children.Count == 0;
            var isActive = ReadBoolean(node, "active", "isActive") ?? true;
            parsed[id.Value] = new ParsedCategory(
                id.Value, name.Trim(), parentCategoryId, string.Join(" > ", pathParts), isLeaf, isActive);
            currentParentId = id;
            currentPath = pathParts;
        }

        if (children.Count > 0)
        {
            foreach (var child in children)
            {
                ParseNode(child, currentParentId, currentPath, parsed);
            }
            return;
        }

        if (id is null)
        {
            foreach (var property in node.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                {
                    ParseNode(property.Value, parentCategoryId, parentPath, parsed);
                }
            }
        }
    }

    private static List<JsonElement> FindChildren(JsonElement node)
    {
        foreach (var name in new[] { "subCategories", "children", "categories", "items" })
        {
            if (node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray().ToList();
            }
        }
        return [];
    }

    private static long? ReadLong(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (node.TryGetProperty(name, out var value) && value.TryGetInt64(out var parsed)) return parsed;
        }
        return null;
    }

    private static string? ReadString(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        }
        return null;
    }

    private static bool? ReadBoolean(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (!node.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.True) return true;
            if (value.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }

    private static BranchPosPayloadSummary ReadBranchPosPayloadSummary(string payloadJson)
    {
        try
        {
            var items = JsonSerializer.Deserialize<BranchPosPriceItem[]>(payloadJson) ?? [];
            return new BranchPosPayloadSummary(items.Length, items.Count(item => item.SalesBlocked != 0));
        }
        catch (JsonException)
        {
            return new BranchPosPayloadSummary(0, 0);
        }
    }

    internal sealed record ParsedCategory(
        long CategoryId,
        string Name,
        long? ParentCategoryId,
        string Path,
        bool IsLeaf,
        bool IsActive);

    private sealed record BranchPosPayloadSummary(int ItemCount, int BlockedItemCount);
}
