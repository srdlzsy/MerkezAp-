using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal sealed class MikroApiWriteAuditReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<MikroApiWriteAuditOptions> options,
    ILogger<MikroApiWriteAuditReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reconciliationService = scope.ServiceProvider
                    .GetRequiredService<MikroApiWriteAuditReconciliationService>();
                await reconciliationService.ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Mikro API write audit reconciliation failed.");
            }

            var intervalSeconds = Math.Clamp(
                options.CurrentValue.ReconciliationIntervalSeconds,
                30,
                3600);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
