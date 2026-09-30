using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal interface ITrendyolGoPriceStockSnapshotRefresher
{
    Task RefreshSnapshotAsync(long storeId, CancellationToken cancellationToken);
}

internal sealed class TrendyolGoPriceStockSnapshotWorker(
    TrendyolGoPriceStockSnapshotCoordinator coordinator,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<TrendyolGoOptions> options,
    ILogger<TrendyolGoPriceStockSnapshotWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ScheduleCheckInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        QueueWarmupStores();

        while (!stoppingToken.IsCancellationRequested)
        {
            QueueDueSnapshots();

            while (coordinator.TryDequeue(out var storeId))
            {
                await RefreshStoreAsync(storeId, stoppingToken);
            }

            try
            {
                using var queueWaitCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var queueWait = coordinator.WaitToReadAsync(queueWaitCancellation.Token).AsTask();
                var scheduleWait = Task.Delay(ScheduleCheckInterval, stoppingToken);
                if (await Task.WhenAny(queueWait, scheduleWait) == scheduleWait)
                {
                    queueWaitCancellation.Cancel();
                    try
                    {
                        await queueWait;
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        // The periodic scheduler won the race; no queue item was consumed.
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void QueueWarmupStores()
    {
        var config = options.CurrentValue;
        if (!config.Enabled)
        {
            return;
        }

        var configuredStoreIds = config.Stores.Select(item => item.StoreId).ToHashSet();
        foreach (var storeId in config.PreviewWarmupStoreIds.Distinct())
        {
            if (configuredStoreIds.Contains(storeId))
            {
                coordinator.RequestRefresh(storeId);
            }
        }
    }

    private void QueueDueSnapshots()
    {
        if (!options.CurrentValue.Enabled)
        {
            return;
        }

        var refreshInterval = GetRefreshInterval(options.CurrentValue);
        var now = DateTime.UtcNow;
        foreach (var storeId in coordinator.GetTrackedStoreIds())
        {
            var state = coordinator.GetState(storeId);
            var snapshotIsDue = state.Snapshot is not null &&
                now - state.Snapshot.GeneratedAtUtc >= refreshInterval;
            var failedRefreshIsDue = state.RefreshState == SnapshotRefreshState.Failed &&
                state.RefreshCompletedAtUtc is DateTime failedAtUtc &&
                now - failedAtUtc >= refreshInterval;
            if ((snapshotIsDue && state.RefreshState != SnapshotRefreshState.Failed) ||
                failedRefreshIsDue)
            {
                coordinator.RequestRefresh(storeId);
            }
        }
    }

    private async Task RefreshStoreAsync(long storeId, CancellationToken stoppingToken)
    {
        coordinator.MarkRefreshing(storeId);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider
                .GetRequiredService<ITrendyolGoPriceStockSnapshotRefresher>()
                .RefreshSnapshotAsync(storeId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            coordinator.MarkFailed(storeId, exception.Message);
            logger.LogError(exception,
                "Trendyol Go price-stock snapshot refresh failed for store {StoreId}.", storeId);
        }
        finally
        {
            coordinator.Complete(storeId);
        }
    }

    internal static TimeSpan GetRefreshInterval(TrendyolGoOptions config) =>
        TimeSpan.FromSeconds(Math.Clamp(config.PreviewRefreshIntervalSeconds, 60, 3600));
}
