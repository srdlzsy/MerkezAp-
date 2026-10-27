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

        await Task.WhenAll(
            RunLaneAsync(isOrderDispatchLane: true, stoppingToken),
            RunLaneAsync(isOrderDispatchLane: false, stoppingToken));
    }

    private async Task RunLaneAsync(
        bool isOrderDispatchLane,
        CancellationToken stoppingToken)
    {
        var waitingActivity = isOrderDispatchLane
            ? "Siparis gonderimleri bekleniyor"
            : "Aktarim gorevleri bekleniyor";

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
                waitingActivity,
                queue.GetBacklogSnapshot());

            AxataSynchronizationJobWorkItem? workItem;

            try
            {
                using var heartbeatCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                heartbeatCancellation.CancelAfter(HeartbeatInterval);
                workItem = isOrderDispatchLane
                    ? await queue.DequeueOrderDispatchAsync(heartbeatCancellation.Token)
                    : await queue.DequeueGeneralAsync(heartbeatCancellation.Token);
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await ExecuteWorkItemAsync(workItem, stoppingToken);
        }
    }

    private async Task ExecuteWorkItemAsync(
        AxataSynchronizationJobWorkItem workItem,
        CancellationToken stoppingToken)
    {
        queue.MarkRunning(workItem.JobId);
        runtimeMonitor.ReportHeartbeat(
            WorkerRuntimeMonitor.AxataWorkerCode,
            $"{workItem.TaskName} calisiyor (Job: {workItem.JobId})",
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

            using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var timeoutMinutes = Math.Clamp(options.CurrentValue.JobTimeoutMinutes, 1, 60);
            executionCancellation.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));
            var executionTask = coordinator.ExecuteAsync(context, executionCancellation.Token);
            while (!executionTask.IsCompleted)
            {
                // Keep the scoped services alive until the running task observes shutdown
                // and completes. Abandoning the task here disposes its DbContexts mid-query.
                var heartbeatDelay = Task.Delay(HeartbeatInterval, CancellationToken.None);
                if (await Task.WhenAny(executionTask, heartbeatDelay) == executionTask)
                {
                    break;
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    runtimeMonitor.ReportHeartbeat(
                        WorkerRuntimeMonitor.AxataWorkerCode,
                        $"{workItem.TaskName} calisiyor (Job: {workItem.JobId})",
                        queue.GetBacklogSnapshot());
                }
            }

            var result = await executionTask;
            queue.MarkSucceeded(workItem.JobId, result);
            runtimeMonitor.ReportSuccess(
                WorkerRuntimeMonitor.AxataWorkerCode,
                $"{workItem.TaskName} tamamlandi (Job: {workItem.JobId})",
                queue.GetBacklogSnapshot());
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            var timeoutMinutes = Math.Clamp(options.CurrentValue.JobTimeoutMinutes, 1, 60);
            var message = $"AXATA gorevi {timeoutMinutes} dakikalik calisma suresini asti ve siradaki belgelerin beklememesi icin durduruldu.";
            queue.MarkFailed(workItem.JobId, message);
            logger.LogError(
                "AXATA synchronization job {JobId} timed out for task {TaskCode} after {TimeoutMinutes} minutes.",
                workItem.JobId,
                workItem.TaskCode,
                timeoutMinutes);
            runtimeMonitor.ReportFailure(
                WorkerRuntimeMonitor.AxataWorkerCode,
                $"{workItem.TaskName} zaman asimina ugradi (Job: {workItem.JobId})",
                new TimeoutException(message),
                queue.GetBacklogSnapshot());
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            queue.MarkFailed(workItem.JobId, "Uygulama kapandigi icin AXATA gorevi kontrollu olarak durduruldu.");
            runtimeMonitor.ReportDisabled(
                WorkerRuntimeMonitor.AxataWorkerCode,
                "Application is stopping",
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
                $"{workItem.TaskName} basarisiz oldu (Job: {workItem.JobId})",
                exception,
                queue.GetBacklogSnapshot());
        }
    }
}
