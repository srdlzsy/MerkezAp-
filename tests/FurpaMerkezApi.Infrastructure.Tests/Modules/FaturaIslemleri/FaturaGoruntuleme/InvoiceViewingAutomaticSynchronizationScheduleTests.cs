using FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class InvoiceViewingAutomaticSynchronizationScheduleTests
{
    [Fact]
    public void BuildSlots_AddsEndTimeWhenItIsNotOnInterval()
    {
        var options = new InvoiceViewingAutomaticSynchronizationOptions
        {
            StartTime = "08:30",
            EndTime = "17:30",
            IntervalMinutes = 120,
            RunAtEndTime = true
        };

        var slots = InvoiceViewingAutomaticSynchronizationSchedule.BuildSlots(options, out var invalidReason);

        Assert.Null(invalidReason);
        Assert.Equal(
            [
                new TimeSpan(8, 30, 0),
                new TimeSpan(10, 30, 0),
                new TimeSpan(12, 30, 0),
                new TimeSpan(14, 30, 0),
                new TimeSpan(16, 30, 0),
                new TimeSpan(17, 30, 0)
            ],
            slots);
    }

    [Fact]
    public void TryGetDueSlot_ReturnsCurrentWindowSlot()
    {
        var options = new InvoiceViewingAutomaticSynchronizationOptions
        {
            StartTime = "08:30",
            EndTime = "17:30",
            IntervalMinutes = 120,
            TriggerWindowMinutes = 5
        };

        var isDue = InvoiceViewingAutomaticSynchronizationSchedule.TryGetDueSlot(
            new DateTime(2026, 7, 21, 10, 32, 0),
            options,
            out var dueSlot,
            out var invalidReason);

        Assert.True(isDue);
        Assert.Null(invalidReason);
        Assert.Equal(new TimeSpan(10, 30, 0), dueSlot);
    }

    [Fact]
    public void TryGetDueSlot_DoesNotCatchUpMissedSlots()
    {
        var options = new InvoiceViewingAutomaticSynchronizationOptions
        {
            StartTime = "08:30",
            EndTime = "17:30",
            IntervalMinutes = 120,
            TriggerWindowMinutes = 5
        };

        var isDue = InvoiceViewingAutomaticSynchronizationSchedule.TryGetDueSlot(
            new DateTime(2026, 7, 21, 9, 0, 0),
            options,
            out _,
            out var invalidReason);

        Assert.False(isDue);
        Assert.Null(invalidReason);
    }

    [Fact]
    public void BuildSlots_AcceptsDotSeparatedTimes()
    {
        var options = new InvoiceViewingAutomaticSynchronizationOptions
        {
            StartTime = "08.00",
            EndTime = "18.00",
            IntervalMinutes = 180,
            RunAtEndTime = true
        };

        var slots = InvoiceViewingAutomaticSynchronizationSchedule.BuildSlots(options, out var invalidReason);

        Assert.Null(invalidReason);
        Assert.Equal(
            [
                new TimeSpan(8, 0, 0),
                new TimeSpan(11, 0, 0),
                new TimeSpan(14, 0, 0),
                new TimeSpan(17, 0, 0),
                new TimeSpan(18, 0, 0)
            ],
            slots);
    }
}
