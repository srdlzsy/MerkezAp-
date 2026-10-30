using System.Collections.Concurrent;
using FurpaMerkezApi.Application.Abstractions.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationScheduler(
    AxataSynchronizationQueue queue,
    IClock clock,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    WorkerRuntimeMonitor runtimeMonitor,
    ILogger<AxataSynchronizationScheduler> logger) : BackgroundService
{
    // Scheduled writes need a stable actor so their idempotency scope survives restarts.
    private static readonly Guid ScheduledWorkerUserId = new("73c53eb1-67ea-4baa-9951-c5c1a600a1a1");
    private readonly ConcurrentDictionary<string, DateTime> nextRunByTask = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        runtimeMonitor.Register(
            WorkerRuntimeMonitor.AxataSchedulerCode,
            "AXATA Synchronization Scheduler",
            isCritical: false,
            staleAfter: TimeSpan.FromMinutes(3));

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentOptions = options.CurrentValue;
            if (!currentOptions.Enabled || !currentOptions.WorkerEnabled || !currentOptions.SchedulerEnabled)
            {
                runtimeMonitor.ReportDisabled(
                    WorkerRuntimeMonitor.AxataSchedulerCode,
                    "Disabled by configuration");
            }
            else
            {
                runtimeMonitor.ReportHeartbeat(
                    WorkerRuntimeMonitor.AxataSchedulerCode,
                    "Checking schedules");
            }

            try
            {
                ScheduleEligibleJobs();
                if (currentOptions.Enabled && currentOptions.WorkerEnabled && currentOptions.SchedulerEnabled)
                {
                    runtimeMonitor.ReportSuccess(
                        WorkerRuntimeMonitor.AxataSchedulerCode,
                        "Schedule cycle completed");
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "AXATA synchronization scheduler cycle failed.");
                runtimeMonitor.ReportFailure(
                    WorkerRuntimeMonitor.AxataSchedulerCode,
                    "Schedule cycle failed",
                    exception);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private void ScheduleEligibleJobs()
    {
        var currentOptions = options.CurrentValue;

        if (!currentOptions.Enabled || !currentOptions.WorkerEnabled || !currentOptions.SchedulerEnabled)
        {
            return;
        }

        var now = clock.UtcNow;

        foreach (var definition in AxataSynchronizationCatalog.Definitions
                     .OrderBy(definition => AxataSynchronizationCatalog.GetSchedulingPriority(definition.Code)))
        {
            var taskOptions = ResolveTaskOptions(currentOptions, definition.Code);
            var warehouseNo = AxataSynchronizationCatalog.ResolveScheduledWarehouseNo(
                definition,
                taskOptions.DefaultWarehouseNo);

            if (!taskOptions.Enabled || !taskOptions.ScheduleEnabled)
            {
                continue;
            }

            if (AxataSynchronizationCatalog.RequiresScheduledWarehouseNo(definition) &&
                taskOptions.DefaultWarehouseNo is null or <= 0)
            {
                logger.LogWarning(
                    "Scheduled AXATA synchronization task {TaskCode} is enabled but DefaultWarehouseNo is missing.",
                    definition.Code);
                continue;
            }

            var nextRunUtc = nextRunByTask.GetOrAdd(definition.Code, now);

            if (nextRunUtc > now)
            {
                continue;
            }

            if (queue.HasActiveJob(definition.Code, warehouseNo))
            {
                nextRunByTask[definition.Code] = now.AddMinutes(Math.Max(1, taskOptions.IntervalMinutes));
                continue;
            }

            try
            {
                queue.Enqueue(
                    definition,
                    ResolveScheduledExecutionMode(taskOptions),
                    AxataSynchronizationJobTriggerSource.Scheduled,
                    warehouseNo,
                    ScheduledWorkerUserId);
            }
            catch (InvalidOperationException) when (queue.HasActiveJob(definition.Code, warehouseNo))
            {
                nextRunByTask[definition.Code] = now.AddMinutes(Math.Max(1, taskOptions.IntervalMinutes));
                continue;
            }

            logger.LogInformation(
                "Scheduled AXATA synchronization job queued for task {TaskCode} and warehouse {WarehouseNo}.",
                definition.Code,
                warehouseNo);

            nextRunByTask[definition.Code] = now.AddMinutes(Math.Max(1, taskOptions.IntervalMinutes));
        }
    }

    private static AxataSynchronizationTaskOptions ResolveTaskOptions(
        AxataSynchronizationOptions options,
        string taskCode) =>
        options.Tasks.TryGetValue(taskCode, out var taskOptions)
            ? taskOptions
            : new AxataSynchronizationTaskOptions();

    private static AxataSynchronizationJobExecutionMode ResolveScheduledExecutionMode(
        AxataSynchronizationTaskOptions taskOptions) =>
        !string.IsNullOrWhiteSpace(taskOptions.LiveOperationName)
            ? AxataSynchronizationJobExecutionMode.Live
            : AxataSynchronizationJobExecutionMode.Outbox;
}
