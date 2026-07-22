using System.Globalization;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

internal static class InvoiceViewingAutomaticSynchronizationSchedule
{
    private static readonly string[] TimeFormats =
    [
        @"h\:mm",
        @"hh\:mm",
        @"h\:mm\:ss",
        @"hh\:mm\:ss"
    ];

    public static bool TryGetDueSlot(
        DateTime localNow,
        InvoiceViewingAutomaticSynchronizationOptions options,
        out TimeSpan dueSlot,
        out string? invalidReason)
    {
        dueSlot = default;

        var slots = BuildSlots(options, out invalidReason);

        if (slots.Count == 0)
        {
            return false;
        }

        var currentTime = localNow.TimeOfDay;
        var triggerWindow = ResolveTriggerWindow(options);

        foreach (var slot in slots)
        {
            var slotWindowEnd = slot + triggerWindow;

            if (currentTime >= slot && currentTime < slotWindowEnd)
            {
                dueSlot = slot;
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<TimeSpan> BuildSlots(
        InvoiceViewingAutomaticSynchronizationOptions options,
        out string? invalidReason)
    {
        invalidReason = null;

        if (!TryParseClockTime(options.StartTime, out var startTime))
        {
            invalidReason = "FaturaGoruntuleme:AutomaticSynchronization:StartTime gecersiz. Ornek: 08:30";
            return [];
        }

        if (!TryParseClockTime(options.EndTime, out var endTime))
        {
            invalidReason = "FaturaGoruntuleme:AutomaticSynchronization:EndTime gecersiz. Ornek: 17:30";
            return [];
        }

        if (endTime < startTime)
        {
            invalidReason = "FaturaGoruntuleme:AutomaticSynchronization:EndTime, StartTime degerinden erken olamaz.";
            return [];
        }

        var interval = TimeSpan.FromMinutes(Math.Clamp(options.IntervalMinutes, 1, 24 * 60));
        var slots = new List<TimeSpan>();

        for (var slot = startTime; slot <= endTime; slot = slot.Add(interval))
        {
            slots.Add(slot);
        }

        if (options.RunAtEndTime && slots[^1] != endTime)
        {
            slots.Add(endTime);
        }

        return slots
            .Distinct()
            .Order()
            .ToArray();
    }

    private static TimeSpan ResolveTriggerWindow(InvoiceViewingAutomaticSynchronizationOptions options) =>
        TimeSpan.FromMinutes(Math.Clamp(options.TriggerWindowMinutes, 1, 60));

    private static bool TryParseClockTime(string? value, out TimeSpan time)
    {
        time = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace('.', ':');

        if (!TimeSpan.TryParseExact(
                normalized,
                TimeFormats,
                CultureInfo.InvariantCulture,
                out time) &&
            !TimeSpan.TryParse(
                normalized,
                CultureInfo.InvariantCulture,
                out time))
        {
            return false;
        }

        return time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);
    }
}
