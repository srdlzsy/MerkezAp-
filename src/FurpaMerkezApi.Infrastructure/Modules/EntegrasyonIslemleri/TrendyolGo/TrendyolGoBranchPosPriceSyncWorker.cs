using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoBranchPosPriceSyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<TrendyolGoOptions> options,
    ILogger<TrendyolGoBranchPosPriceSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<TrendyolGoBranchPosPriceSyncService>()
                    .ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Branch POS price synchronization worker failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.CurrentValue.BranchPosPriceSync.WorkerIntervalSeconds, 10, 3600)), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
