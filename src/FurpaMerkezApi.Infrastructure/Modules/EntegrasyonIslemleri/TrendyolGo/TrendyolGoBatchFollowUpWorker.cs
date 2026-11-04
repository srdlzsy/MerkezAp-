using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoBatchFollowUpWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TrendyolGoBatchFollowUpWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var processed = 0; processed < 20; processed++)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var count = await scope.ServiceProvider
                        .GetRequiredService<TrendyolGoBatchFollowUpProcessor>()
                        .ProcessPendingAsync(stoppingToken);
                    if (count == 0)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Trendyol Go batch follow-up worker failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
