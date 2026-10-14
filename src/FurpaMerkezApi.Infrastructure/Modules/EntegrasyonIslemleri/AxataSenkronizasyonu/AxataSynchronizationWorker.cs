using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationWorker(
    AxataSynchronizationQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    WorkerRuntimeMonitor runtimeMonitor,
    ILogger<AxataSynchronizationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        runtimeMonitor.Register(
            WorkerRuntimeMonitor.AxataWorkerCode,
            "AXATA Synchronization Worker",
            isCritical: true,
            staleAfter: TimeSpan.FromSeconds(60));

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!options.CurrentValue.Enabled || !options.CurrentValue.WorkerEnabled)
            {
                runtimeMonitor.ReportDisabled(
                    WorkerRuntimeMonitor.AxataWorkerCode,
                    "Disabled by configuration",
                    queue.GetBacklogSnapshot());
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            runtimeMonitor.ReportHeartbeat(
                WorkerRuntimeMonitor.AxataWorkerCode,
                "Waiting for work",
                queue.GetBacklogSnapshot());

            AxataSynchronizationJobWorkItem? workItem;

            try
            {
                using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                heartbeatCancellation.CancelAfter(HeartbeatInterval);
                workItem = await queue.DequeueAsync(heartbeatCancellation.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            queue.MarkRunning(workItem.JobId);
            runtimeMonitor.ReportHeartbeat(
                WorkerRuntimeMonitor.AxataWorkerCode,
                $"Running {workItem.TaskCode}",
                queue.GetBacklogSnapshot());

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

                var executionTask = coordinator.ExecuteAsync(context, stoppingToken);
                while (!executionTask.IsCompleted)
                {
                    var heartbeatDelay = Task.Delay(HeartbeatInterval, stoppingToken);
                    if (await Task.WhenAny(executionTask, heartbeatDelay) == executionTask)
                    {
                        break;
                    }

                    stoppingToken.ThrowIfCancellationRequested();
                    runtimeMonitor.ReportHeartbeat(
                        WorkerRuntimeMonitor.AxataWorkerCode,
                        $"Running {workItem.TaskCode}",
                        queue.GetBacklogSnapshot());
                }

                var result = await executionTask;
                queue.MarkSucceeded(workItem.JobId, result);
                runtimeMonitor.ReportSuccess(
                    WorkerRuntimeMonitor.AxataWorkerCode,
                    $"Completed {workItem.TaskCode}",
                    queue.GetBacklogSnapshot());
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "AXATA synchronization job {JobId} failed for task {TaskCode}.",
                    workItem.JobId,
                    workItem.TaskCode);
                queue.MarkFailed(workItem.JobId, exception.Message);
                runtimeMonitor.ReportFailure(
                    WorkerRuntimeMonitor.AxataWorkerCode,
                    $"Failed {workItem.TaskCode}",
                    exception,
                    queue.GetBacklogSnapshot());
            }
        }
    }
}
