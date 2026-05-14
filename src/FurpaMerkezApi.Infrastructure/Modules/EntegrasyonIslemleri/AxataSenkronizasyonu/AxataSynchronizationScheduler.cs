using System.Collections.Concurrent;
using FurpaMerkezApi.Application.Abstractions.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationScheduler(
    AxataSynchronizationQueue queue,
    IClock clock,
    IOptionsMonitor<AxataSynchronizationOptions> options,
    ILogger<AxataSynchronizationScheduler> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, DateTime> nextRunByTask = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ScheduleEligibleJobs();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "AXATA synchronization scheduler cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
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

        foreach (var definition in AxataSynchronizationCatalog.Definitions)
        {
            var taskOptions = ResolveTaskOptions(currentOptions, definition.Code);

            if (!taskOptions.Enabled || !taskOptions.ScheduleEnabled)
            {
                continue;
            }

            if (definition.RequiresWarehouseNo && taskOptions.DefaultWarehouseNo is null or <= 0)
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

            if (queue.HasActiveJob(definition.Code, taskOptions.DefaultWarehouseNo))
            {
                nextRunByTask[definition.Code] = now.AddMinutes(Math.Max(1, taskOptions.IntervalMinutes));
                continue;
            }

            queue.Enqueue(
                definition,
                AxataSynchronizationJobExecutionMode.Outbox,
                AxataSynchronizationJobTriggerSource.Scheduled,
                taskOptions.DefaultWarehouseNo,
                Guid.Empty);

            logger.LogInformation(
                "Scheduled AXATA synchronization job queued for task {TaskCode} and warehouse {WarehouseNo}.",
                definition.Code,
                taskOptions.DefaultWarehouseNo);

            nextRunByTask[definition.Code] = now.AddMinutes(Math.Max(1, taskOptions.IntervalMinutes));
        }
    }

    private static AxataSynchronizationTaskOptions ResolveTaskOptions(
        AxataSynchronizationOptions options,
        string taskCode) =>
        options.Tasks.TryGetValue(taskCode, out var taskOptions)
            ? taskOptions
            : new AxataSynchronizationTaskOptions();
}
