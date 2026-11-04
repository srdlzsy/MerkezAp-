using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoBatchFollowUpQueue(
    AuthDbContext authDbContext,
    IClock clock) : ITrendyolGoBatchWorkflowQueries
{
    public Task<Guid> EnqueuePriceStockAsync(
        string batchRequestId,
        long storeId,
        int warehouseNo,
        IReadOnlyCollection<TrendyolGoDeferredPriceStockItem> items,
        CancellationToken cancellationToken,
        bool autoCreateMissingCatalog = false) =>
        EnqueueAsync(
            TrendyolGoBatchFollowUpKind.PriceStock,
            batchRequestId,
            storeId,
            warehouseNo,
            new TrendyolGoBatchFollowUpPayload(items, [], autoCreateMissingCatalog),
            cancellationToken);

    public Task<Guid> EnqueueCatalogThenPriceStockAsync(
        string batchRequestId,
        long storeId,
        int warehouseNo,
        IReadOnlyCollection<TrendyolGoDeferredPriceStockItem> items,
        IReadOnlyCollection<string> catalogBarcodes,
        CancellationToken cancellationToken) =>
        EnqueueAsync(
            TrendyolGoBatchFollowUpKind.CatalogThenPriceStock,
            batchRequestId,
            storeId,
            warehouseNo,
            new TrendyolGoBatchFollowUpPayload(items, catalogBarcodes, false),
            cancellationToken);

    public async Task<IReadOnlyCollection<TrendyolGoBatchWorkflowDto>> ListAsync(
        int take,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 500);
        return await authDbContext.TrendyolGoBatchFollowUpTasks
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(take)
            .Select(item => new TrendyolGoBatchWorkflowDto(
                item.Id,
                item.Kind.ToString(),
                item.BatchRequestId,
                item.StoreId,
                item.WarehouseNo,
                item.Status.ToString(),
                item.AttemptCount,
                item.NextAttemptAtUtc,
                item.LastError,
                item.ResultSummary,
                item.CreatedAtUtc,
                item.CompletedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<Guid> EnqueueAsync(
        TrendyolGoBatchFollowUpKind kind,
        string batchRequestId,
        long storeId,
        int warehouseNo,
        TrendyolGoBatchFollowUpPayload payload,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken; // Upstream accepted the batch; persist tracking even if the HTTP client disconnects.
        using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var persistenceToken = persistenceTimeout.Token;
        var normalizedBatchId = batchRequestId.Trim();
        var existingId = await authDbContext.TrendyolGoBatchFollowUpTasks
            .Where(item => item.BatchRequestId == normalizedBatchId)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(persistenceToken);
        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        var task = new TrendyolGoBatchFollowUpTask(
            Guid.NewGuid(),
            kind,
            normalizedBatchId,
            storeId,
            warehouseNo,
            JsonSerializer.Serialize(payload),
            clock.UtcNow);
        authDbContext.TrendyolGoBatchFollowUpTasks.Add(task);
        await authDbContext.SaveChangesAsync(persistenceToken);
        return task.Id;
    }
}

internal sealed record TrendyolGoBatchFollowUpPayload(
    IReadOnlyCollection<TrendyolGoDeferredPriceStockItem> Items,
    IReadOnlyCollection<string> CatalogBarcodes,
    bool AutoCreateMissingCatalog = false);

internal sealed record TrendyolGoDeferredPriceStockItem(
    string Barcode,
    string StockCode,
    decimal SellingPrice,
    int Quantity,
    int UnitPointer,
    string UnitName,
    int SalesBlocked,
    int OrderBlocked,
    int GoodsAcceptanceBlocked,
    DateTime? PriceUpdatedAtUtc,
    DateTime? WarehouseUpdatedAtUtc,
    int PriceListNo)
{
    public BranchPosPriceItem ToBranchPosItem() => new(
        StockCode,
        SellingPrice,
        UnitPointer,
        UnitName,
        SalesBlocked,
        OrderBlocked,
        GoodsAcceptanceBlocked,
        PriceUpdatedAtUtc,
        WarehouseUpdatedAtUtc,
        PriceListNo);
}
