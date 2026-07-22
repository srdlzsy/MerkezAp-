using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

internal sealed class InvoiceViewingAutomaticSynchronizationScheduler(
    InvoiceViewingSynchronizationJobQueue queue,
    IClock clock,
    IOptionsMonitor<InvoiceViewingAutomaticSynchronizationOptions> options,
    ILogger<InvoiceViewingAutomaticSynchronizationScheduler> logger) : BackgroundService
{
    private DateOnly? lastAttemptedDate;
    private TimeSpan? lastAttemptedSlot;
    private string? lastInvalidScheduleReason;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var currentOptions = options.CurrentValue;

            try
            {
                ScheduleEligibleJob(currentOptions);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Invoice viewing automatic synchronization scheduler cycle failed.");
            }

            await Task.Delay(ResolvePollInterval(currentOptions), stoppingToken);
        }
    }

    private void ScheduleEligibleJob(InvoiceViewingAutomaticSynchronizationOptions currentOptions)
    {
        if (!currentOptions.Enabled)
        {
            return;
        }

        var localNow = ResolveLocalNow();

        if (!InvoiceViewingAutomaticSynchronizationSchedule.TryGetDueSlot(
                localNow,
                currentOptions,
                out var dueSlot,
                out var invalidReason))
        {
            ReportInvalidSchedule(invalidReason);
            return;
        }

        lastInvalidScheduleReason = null;

        var currentDate = DateOnly.FromDateTime(localNow);

        if (lastAttemptedDate == currentDate && lastAttemptedSlot == dueSlot)
        {
            return;
        }

        lastAttemptedDate = currentDate;
        lastAttemptedSlot = dueSlot;

        var today = localNow.Date;
        var request = new InvoiceViewingSynchronizationRequest(
            today,
            today,
            currentOptions.IncludeStatuses);

        var queued = queue.TryEnqueue(request, out var progress);

        if (queued)
        {
            logger.LogInformation(
                "Automatic invoice viewing synchronization queued for {SyncDate} at local schedule slot {ScheduleSlot}.",
                today,
                FormatTime(dueSlot));
            return;
        }

        logger.LogInformation(
            "Automatic invoice viewing synchronization skipped for {SyncDate} at local schedule slot {ScheduleSlot} because another synchronization is active. CurrentStatus={CurrentStatus}.",
            today,
            FormatTime(dueSlot),
            progress.Status);
    }

    private void ReportInvalidSchedule(string? invalidReason)
    {
        if (string.IsNullOrWhiteSpace(invalidReason) ||
            string.Equals(lastInvalidScheduleReason, invalidReason, StringComparison.Ordinal))
        {
            return;
        }

        lastInvalidScheduleReason = invalidReason;

        logger.LogWarning(
            "Invoice viewing automatic synchronization schedule is invalid: {Reason}",
            invalidReason);
    }

    private DateTime ResolveLocalNow()
    {
        var utcNow = clock.UtcNow.Kind == DateTimeKind.Utc
            ? clock.UtcNow
            : DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);

        return utcNow.ToLocalTime();
    }

    private static TimeSpan ResolvePollInterval(InvoiceViewingAutomaticSynchronizationOptions currentOptions) =>
        TimeSpan.FromSeconds(Math.Clamp(currentOptions.PollIntervalSeconds, 10, 3600));

    private static string FormatTime(TimeSpan time) =>
        time.ToString(@"hh\:mm");
}
