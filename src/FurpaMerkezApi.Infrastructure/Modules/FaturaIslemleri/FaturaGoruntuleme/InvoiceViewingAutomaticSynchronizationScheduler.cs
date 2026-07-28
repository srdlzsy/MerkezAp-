using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

internal sealed class InvoiceViewingAutomaticSynchronizationScheduler(
    InvoiceViewingSynchronizationJobQueue queue,
    InvoiceViewingSynchronizationProgressStore progressStore,
    IClock clock,
    IOptionsMonitor<InvoiceViewingAutomaticSynchronizationOptions> options,
    ILogger<InvoiceViewingAutomaticSynchronizationScheduler> logger) : BackgroundService
{
    private DateOnly? lastAttemptedDate;
    private TimeSpan? lastAttemptedSlot;
    private DateOnly? lastReportedMissedDate;
    private TimeSpan? lastReportedMissedSlot;
    private string? lastInvalidScheduleReason;
    private bool disabledStateReported;

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
        var utcNow = ResolveUtcNow();
        var localNow = utcNow.ToLocalTime();

        if (!currentOptions.Enabled)
        {
            progressStore.ReportSchedulerCheck(
                enabled: false,
                checkedAtUtc: utcNow,
                checkedAtLocal: localNow,
                status: "disabled",
                message: "Otomatik fatura goruntuleme senkronizasyonu kapali.");
            ReportDisabledOnce();
            return;
        }

        disabledStateReported = false;

        if (!InvoiceViewingAutomaticSynchronizationSchedule.TryGetDueSlot(
                localNow,
                currentOptions,
                out var dueSlot,
                out var invalidReason))
        {
            if (!string.IsNullOrWhiteSpace(invalidReason))
            {
                progressStore.ReportSchedulerCheck(
                    enabled: true,
                    checkedAtUtc: utcNow,
                    checkedAtLocal: localNow,
                    status: "invalid-schedule",
                    message: invalidReason);
                ReportInvalidSchedule(invalidReason);
                return;
            }

            lastInvalidScheduleReason = null;
            ReportNoDueSlot(currentOptions, utcNow, localNow);
            return;
        }

        lastInvalidScheduleReason = null;

        var currentDate = DateOnly.FromDateTime(localNow);

        if (lastAttemptedDate == currentDate && lastAttemptedSlot == dueSlot)
        {
            progressStore.ReportSchedulerCheck(
                enabled: true,
                checkedAtUtc: utcNow,
                checkedAtLocal: localNow,
                status: "already-attempted",
                message: "Bu schedule slotu icin otomatik senkronizasyon daha once denendi.",
                currentSlot: FormatTime(dueSlot),
                nextSlot: ResolveNextSlotText(currentOptions, localNow));
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
            progressStore.ReportSchedulerCheck(
                enabled: true,
                checkedAtUtc: utcNow,
                checkedAtLocal: localNow,
                status: "queued",
                message: "Otomatik senkronizasyon siraya alindi.",
                currentSlot: FormatTime(dueSlot),
                nextSlot: ResolveNextSlotText(currentOptions, localNow),
                lastQueuedSlot: FormatTime(dueSlot),
                lastQueuedAtUtc: utcNow);
            logger.LogInformation(
                "Automatic invoice viewing synchronization queued for {SyncDate} at local schedule slot {ScheduleSlot}.",
                today,
                FormatTime(dueSlot));
            return;
        }

        progressStore.ReportSchedulerCheck(
            enabled: true,
            checkedAtUtc: utcNow,
            checkedAtLocal: localNow,
            status: "active-synchronization",
            message: "Baska bir senkronizasyon aktif oldugu icin otomatik slot atlandi.",
            currentSlot: FormatTime(dueSlot),
            nextSlot: ResolveNextSlotText(currentOptions, localNow),
            lastSkippedSlot: FormatTime(dueSlot),
            lastSkippedAtUtc: utcNow);
        logger.LogInformation(
            "Automatic invoice viewing synchronization skipped for {SyncDate} at local schedule slot {ScheduleSlot} because another synchronization is active. CurrentStatus={CurrentStatus}.",
            today,
            FormatTime(dueSlot),
            progress.Status);
    }

    private void ReportNoDueSlot(
        InvoiceViewingAutomaticSynchronizationOptions currentOptions,
        DateTime utcNow,
        DateTime localNow)
    {
        var currentDate = DateOnly.FromDateTime(localNow);

        if (InvoiceViewingAutomaticSynchronizationSchedule.TryGetMissedSlot(
                localNow,
                currentOptions,
                out var missedSlot,
                out _) &&
            (lastAttemptedDate != currentDate || lastAttemptedSlot != missedSlot))
        {
            var missedSlotText = FormatTime(missedSlot);
            var isNewMissedSlot =
                lastReportedMissedDate != currentDate ||
                lastReportedMissedSlot != missedSlot;

            progressStore.ReportSchedulerCheck(
                enabled: true,
                checkedAtUtc: utcNow,
                checkedAtLocal: localNow,
                status: "missed-slot",
                message: $"Schedule slotu kacirildi: {missedSlotText}.",
                nextSlot: ResolveNextSlotText(currentOptions, localNow),
                lastMissedSlot: isNewMissedSlot ? missedSlotText : null,
                lastMissedAtUtc: isNewMissedSlot ? utcNow : null);

            ReportMissedSlotOnce(currentOptions, currentDate, missedSlot, localNow);
            return;
        }

        progressStore.ReportSchedulerCheck(
            enabled: true,
            checkedAtUtc: utcNow,
            checkedAtLocal: localNow,
            status: "waiting",
            message: "Schedule slot penceresi bekleniyor.",
            nextSlot: ResolveNextSlotText(currentOptions, localNow));
    }

    private void ReportDisabledOnce()
    {
        if (disabledStateReported)
        {
            return;
        }

        disabledStateReported = true;
        logger.LogInformation("Invoice viewing automatic synchronization is disabled.");
    }

    private void ReportMissedSlotOnce(
        InvoiceViewingAutomaticSynchronizationOptions currentOptions,
        DateOnly currentDate,
        TimeSpan missedSlot,
        DateTime localNow)
    {
        if (lastReportedMissedDate == currentDate && lastReportedMissedSlot == missedSlot)
        {
            return;
        }

        lastReportedMissedDate = currentDate;
        lastReportedMissedSlot = missedSlot;

        logger.LogWarning(
            "Automatic invoice viewing synchronization missed local schedule slot {ScheduleSlot} for {SyncDate}. CheckedAtLocal={CheckedAtLocal}, TriggerWindowMinutes={TriggerWindowMinutes}.",
            FormatTime(missedSlot),
            currentDate,
            localNow,
            Math.Clamp(currentOptions.TriggerWindowMinutes, 1, 60));
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

    private DateTime ResolveUtcNow() =>
        clock.UtcNow.Kind == DateTimeKind.Utc
            ? clock.UtcNow
            : DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc);

    private static string? ResolveNextSlotText(
        InvoiceViewingAutomaticSynchronizationOptions currentOptions,
        DateTime localNow) =>
        InvoiceViewingAutomaticSynchronizationSchedule.TryGetNextSlot(
            localNow,
            currentOptions,
            out var nextSlot,
            out _)
            ? FormatTime(nextSlot)
            : null;

    private static TimeSpan ResolvePollInterval(InvoiceViewingAutomaticSynchronizationOptions currentOptions) =>
        TimeSpan.FromSeconds(Math.Clamp(currentOptions.PollIntervalSeconds, 10, 3600));

    private static string FormatTime(TimeSpan time) =>
        time.ToString(@"hh\:mm");
}
