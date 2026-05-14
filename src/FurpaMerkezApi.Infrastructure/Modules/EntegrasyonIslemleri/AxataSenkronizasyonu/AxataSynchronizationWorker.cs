using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationWorker(
    AxataSynchronizationQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    ILogger<AxataSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!options.CurrentValue.Enabled || !options.CurrentValue.WorkerEnabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            AxataSynchronizationJobWorkItem workItem;

            try
            {
                workItem = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            queue.MarkRunning(workItem.JobId);

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<AxataSynchronizationExecutionCoordinator>();
                var definition = AxataSynchronizationCatalog.GetRequired(workItem.TaskCode);
                var context = new AxataSynchronizationTaskExecutionContext(
                    workItem.JobId,
                    definition,
                    workItem.ExecutionMode,
                    workItem.WarehouseNo,
                    workItem.RequestedByUserId,
                    workItem.CreatedAtUtc);

                var result = await coordinator.ExecuteAsync(context, stoppingToken);
                queue.MarkSucceeded(workItem.JobId, result);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "AXATA synchronization job {JobId} failed for task {TaskCode}.",
                    workItem.JobId,
                    workItem.TaskCode);
                queue.MarkFailed(workItem.JobId, exception.Message);
            }
        }
    }
}
