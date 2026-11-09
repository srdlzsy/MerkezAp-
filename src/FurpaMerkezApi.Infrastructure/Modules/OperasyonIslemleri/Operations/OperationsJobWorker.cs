using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;

internal sealed class OperationsJobWorker(
    OperationsJobQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<OperationsJobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            OperationsJobWorkItem workItem;

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
                var generationService = scope.ServiceProvider.GetRequiredService<OperationsFileGenerationService>();
                var result = await generationService.GenerateAsync(
                    workItem.Kind,
                    workItem.WarehouseNo,
                    workItem.JobId,
                    stoppingToken);

                queue.MarkSucceeded(workItem.JobId, result.Message, result.Files);
            }
            catch (OperationCanceledException exception) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation(
                    exception,
                    "Operations job {JobId} for warehouse {WarehouseNo} was canceled because the application is stopping.",
                    workItem.JobId,
                    workItem.WarehouseNo);
                queue.MarkFailed(workItem.JobId, "Uygulama yeniden baslatildigi icin islem iptal edildi.");
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Operations job {JobId} failed for warehouse {WarehouseNo}.",
                    workItem.JobId,
                    workItem.WarehouseNo);
                queue.MarkFailed(workItem.JobId, exception.Message);
            }
        }
    }
}
