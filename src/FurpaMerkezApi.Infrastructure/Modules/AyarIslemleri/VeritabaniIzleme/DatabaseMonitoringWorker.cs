using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

internal sealed class DatabaseMonitoringWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<DatabaseMonitoringOptions> options,
    ILogger<DatabaseMonitoringWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.CurrentValue;
            if (settings.Enabled)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<DatabaseMonitoringService>();
                    await service.CollectIncidentsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Database monitoring incident collection failed.");
                }
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(settings.CollectionIntervalSeconds, 15, 3600)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
