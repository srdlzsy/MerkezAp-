using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoBatchFollowUpProcessor(
    AuthDbContext authDbContext,
    ITrendyolGoIntegrationService trendyol,
    ITrendyolGoPriceStockSnapshotRefresher snapshotRefresher,
    TrendyolGoPriceStockSnapshotCoordinator snapshotCoordinator,
    ITrendyolGoCatalogWorkbench catalogWorkbench,
    TrendyolGoBranchPosPriceSyncService branchPosPriceSync,
    TrendyolGoBatchFollowUpQueue queue,
    IClock clock,
    ILogger<TrendyolGoBatchFollowUpProcessor> logger)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MaximumTrackingAge = TimeSpan.FromHours(3.75);

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var candidate = await authDbContext.TrendyolGoBatchFollowUpTasks
            .AsNoTracking()
            .Where(item =>
                (item.Status == TrendyolGoBatchFollowUpStatus.Pending ||
                 item.Status == TrendyolGoBatchFollowUpStatus.Processing) &&
                item.NextAttemptAtUtc <= now)
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
        {
            return 0;
        }

        var claimed = await authDbContext.TrendyolGoBatchFollowUpTasks
            .Where(item => item.Id == candidate.Id &&
                           item.Status == candidate.Status &&
                           item.NextAttemptAtUtc <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, TrendyolGoBatchFollowUpStatus.Processing)
                .SetProperty(item => item.NextAttemptAtUtc, now.AddMinutes(2)), cancellationToken);
        if (claimed == 0)
        {
            return 0;
        }

        var task = await authDbContext.TrendyolGoBatchFollowUpTasks
            .SingleAsync(item => item.Id == candidate.Id, cancellationToken);

        try
        {
            if (now - task.CreatedAtUtc >= MaximumTrackingAge)
            {
                task.MarkFailed(
                    "Trendyol Go batch result could not be finalized inside the four-hour result window.",
                    now);
                await authDbContext.SaveChangesAsync(cancellationToken);
                return 1;
            }

            var response = await trendyol.GetProductBatchResultAsync(task.BatchRequestId, cancellationToken);
            var parsed = TrendyolGoBatchResultParser.Parse(response);
            if (parsed.IsFailed)
            {
                task.MarkFailed(BuildFailureMessage(parsed), now);
            }
            else if (!parsed.IsCompleted)
            {
                task.ScheduleRetry(
                    string.IsNullOrWhiteSpace(parsed.Status)
                        ? "Trendyol Go batch status is not available yet."
                        : $"Trendyol Go batch status: {parsed.Status}.",
                    now.Add(PollInterval));
            }
            else if (task.Kind == TrendyolGoBatchFollowUpKind.PriceStock)
            {
                await CompletePriceStockAsync(task, parsed, now, cancellationToken);
            }
            else
            {
                await CompleteCatalogAndQueuePriceStockAsync(task, parsed, now, cancellationToken);
            }

            await authDbContext.SaveChangesAsync(cancellationToken);
            return 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failureTime = clock.UtcNow;
            if (failureTime - task.CreatedAtUtc >= MaximumTrackingAge)
            {
                task.MarkFailed(exception.Message, failureTime);
            }
            else
            {
                task.ScheduleRetry(exception.Message, failureTime.Add(PollInterval));
            }

            await authDbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Trendyol Go batch follow-up deferred. BatchRequestId={BatchRequestId}, Kind={Kind}",
                task.BatchRequestId,
                task.Kind);
            return 1;
        }
    }

    private async Task CompletePriceStockAsync(
        TrendyolGoBatchFollowUpTask task,
        TrendyolGoParsedBatchResult parsed,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload(task);
        var successfulItems = SelectSuccessfulItems(payload.Items, parsed);
        var recoveredCatalogItems = await QueueMissingCatalogRecoveryAsync(
            task,
            payload,
            parsed,
            cancellationToken);
        if (successfulItems.Length > 0)
        {
            await branchPosPriceSync.EnqueueRequiredAsync(
                task.StoreId,
                task.WarehouseNo,
                successfulItems.Select(item => item.ToBranchPosItem()).ToArray(),
                cancellationToken);
        }

        var summary =
            $"TGO fiyat/stok batch tamamlandi. Basarili={successfulItems.Length}, Hatali={parsed.FailedItemCount}. " +
            (successfulItems.Length > 0 ? "Basarili urunler sube POS kuyruguna yazildi." : "Sube POS kuyruguna urun yazilmadi.") +
            (recoveredCatalogItems > 0
                ? $" Katalogda olmayan {recoveredCatalogItems} urun otomatik katalog akisina alindi."
                : string.Empty);

        if (parsed.FailedItemCount > 0)
        {
            var error = BuildFailureMessage(parsed);
            if (successfulItems.Length == 0 && recoveredCatalogItems == 0)
            {
                task.MarkFailed(error, now);
            }
            else
            {
                task.MarkPartiallySucceeded(summary, error, now);
            }
            return;
        }

        task.MarkSucceeded(summary, now);
    }

    private async Task<int> QueueMissingCatalogRecoveryAsync(
        TrendyolGoBatchFollowUpTask task,
        TrendyolGoBatchFollowUpPayload payload,
        TrendyolGoParsedBatchResult parsed,
        CancellationToken cancellationToken)
    {
        if (!payload.AutoCreateMissingCatalog ||
            parsed.FailedBarcodes.Count == 0 ||
            !ReportsMissingCatalog(parsed.FailureReasons))
        {
            return 0;
        }

        var failedItems = payload.Items
            .Where(item => parsed.FailedBarcodes.Contains(item.Barcode))
            .DistinctBy(item => item.Barcode, StringComparer.Ordinal)
            .ToArray();
        if (failedItems.Length == 0)
        {
            return 0;
        }

        await snapshotRefresher.RefreshSnapshotAsync(task.StoreId, cancellationToken);
        var autoResult = await catalogWorkbench.AutoCreateProductsAsync(
            task.StoreId,
            failedItems.Select(item => item.Barcode).ToArray(),
            cancellationToken);
        if (autoResult.CreatedCount <= 0)
        {
            return 0;
        }

        var deferredByBarcode = failedItems.ToDictionary(item => item.Barcode, StringComparer.Ordinal);
        var submissions = autoResult.BatchSubmissions ?? [];
        if (submissions.Count == 0)
        {
            throw new InvalidOperationException(
                "Katalog istegi kabul edildi ancak otomatik takip icin batch bilgisi donmedi.");
        }

        foreach (var submission in submissions)
        {
            var submissionItems = submission.Barcodes
                .Where(deferredByBarcode.ContainsKey)
                .Select(barcode => deferredByBarcode[barcode])
                .ToArray();
            if (submissionItems.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Katalog batch {submission.BatchRequestId} icin takip edilecek urun bulunamadi.");
            }

            await queue.EnqueueCatalogThenPriceStockAsync(
                submission.BatchRequestId,
                task.StoreId,
                task.WarehouseNo,
                submissionItems,
                submission.Barcodes,
                cancellationToken);
        }

        return autoResult.CreatedCount;
    }

    internal static bool ReportsMissingCatalog(IReadOnlyCollection<string> failureReasons)
    {
        var normalized = string.Join(' ', failureReasons).ToLowerInvariant();
        var referencesProduct = normalized.Contains("product", StringComparison.Ordinal) ||
            normalized.Contains("catalog", StringComparison.Ordinal) ||
            normalized.Contains("katalog", StringComparison.Ordinal) ||
            normalized.Contains("urun", StringComparison.Ordinal) ||
            normalized.Contains("ürün", StringComparison.Ordinal);
        var reportsMissing = normalized.Contains("not found", StringComparison.Ordinal) ||
            normalized.Contains("not_found", StringComparison.Ordinal) ||
            normalized.Contains("notfound", StringComparison.Ordinal) ||
            normalized.Contains("missing", StringComparison.Ordinal) ||
            normalized.Contains("bulunamad", StringComparison.Ordinal) ||
            normalized.Contains("mevcut degil", StringComparison.Ordinal) ||
            normalized.Contains("mevcut değil", StringComparison.Ordinal);
        return referencesProduct && reportsMissing;
    }

    private async Task CompleteCatalogAndQueuePriceStockAsync(
        TrendyolGoBatchFollowUpTask task,
        TrendyolGoParsedBatchResult parsed,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var payload = DeserializePayload(task);
        var expectedCatalogBarcodes = parsed.FailedItemCount == 0
            ? payload.CatalogBarcodes.ToHashSet(StringComparer.Ordinal)
            : payload.CatalogBarcodes
                .Where(parsed.SuccessfulBarcodes.Contains)
                .ToHashSet(StringComparer.Ordinal);

        if (expectedCatalogBarcodes.Count == 0)
        {
            task.MarkFailed(BuildFailureMessage(parsed), now);
            return;
        }

        await snapshotRefresher.RefreshSnapshotAsync(task.StoreId, cancellationToken);
        var snapshot = snapshotCoordinator.GetState(task.StoreId).Snapshot
            ?? throw new InvalidOperationException("Refreshed Trendyol Go catalog snapshot is unavailable.");
        var existingBarcodes = snapshot.Products
            .Select(item => item.Barcode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);

        var notVisibleYet = expectedCatalogBarcodes.Where(barcode => !existingBarcodes.Contains(barcode)).ToArray();
        if (notVisibleYet.Length > 0)
        {
            task.ScheduleRetry(
                $"Katalog batch tamamlandi; {notVisibleYet.Length} urun magazada gorunur olmayi bekliyor.",
                now.Add(PollInterval));
            return;
        }

        var eligibleItems = payload.Items
            .Where(item => expectedCatalogBarcodes.Contains(item.Barcode) && existingBarcodes.Contains(item.Barcode))
            .DistinctBy(item => item.Barcode, StringComparer.Ordinal)
            .ToArray();
        if (eligibleItems.Length == 0)
        {
            task.MarkFailed("Katalog batch tamamlandi ancak fiyat/stok gonderilecek urun bulunamadi.", now);
            return;
        }

        var queuedPriceBatches = 0;
        foreach (var chunk in eligibleItems.Chunk(1000))
        {
            var response = await trendyol.UpdatePriceAndInventoryAsync(
                JsonSerializer.SerializeToElement(new
                {
                    items = chunk.Select(item => new
                    {
                        barcode = item.Barcode,
                        sellingPrice = item.SellingPrice,
                        quantity = item.Quantity,
                        storeId = task.StoreId
                    }).ToArray()
                }),
                cancellationToken);
            var batchRequestId = RequireBatchRequestId(response, "automatic price-stock dispatch");
            await queue.EnqueuePriceStockAsync(
                batchRequestId,
                task.StoreId,
                task.WarehouseNo,
                chunk,
                cancellationToken);
            queuedPriceBatches++;
        }

        var summary =
            $"Katalog batch tamamlandi. {eligibleItems.Length} urun icin {queuedPriceBatches} fiyat/stok batch'i otomatik kuyruga alindi.";
        if (parsed.FailedItemCount > 0)
        {
            task.MarkPartiallySucceeded(summary, BuildFailureMessage(parsed), now);
        }
        else
        {
            task.MarkSucceeded(summary, now);
        }
    }

    internal static TrendyolGoDeferredPriceStockItem[] SelectSuccessfulItems(
        IReadOnlyCollection<TrendyolGoDeferredPriceStockItem> items,
        TrendyolGoParsedBatchResult parsed)
    {
        if (parsed.FailedItemCount == 0)
        {
            return items.ToArray();
        }

        if (parsed.SuccessfulBarcodes.Count == 0)
        {
            return [];
        }

        return items.Where(item => parsed.SuccessfulBarcodes.Contains(item.Barcode)).ToArray();
    }

    private static TrendyolGoBatchFollowUpPayload DeserializePayload(TrendyolGoBatchFollowUpTask task) =>
        JsonSerializer.Deserialize<TrendyolGoBatchFollowUpPayload>(task.PayloadJson)
        ?? throw new InvalidOperationException("Trendyol Go follow-up payload is invalid.");

    internal static string RequireBatchRequestId(JsonElement? response, string operation)
    {
        if (response.HasValue &&
            response.Value.TryGetProperty("batchRequestId", out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            return property.GetString()!.Trim();
        }

        throw new InvalidOperationException($"Trendyol Go {operation} was accepted without a batchRequestId.");
    }

    private static string BuildFailureMessage(TrendyolGoParsedBatchResult parsed)
    {
        var reasons = parsed.FailureReasons.Count == 0
            ? "TGO hata ayrintisi dondurmedi."
            : string.Join(" | ", parsed.FailureReasons.Take(5));
        return $"Trendyol Go batch basarisiz. Hatali={parsed.FailedItemCount}. {reasons}";
    }
}
