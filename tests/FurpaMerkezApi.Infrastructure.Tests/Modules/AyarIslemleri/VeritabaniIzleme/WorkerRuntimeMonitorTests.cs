using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class WorkerRuntimeMonitorTests
{
    [Fact]
    public void FreshCriticalWorker_IsHealthy()
    {
        var clock = Clock();
        var monitor = Monitor(clock);
        monitor.ReportHeartbeat(
            WorkerRuntimeMonitor.AxataWorkerCode,
            "Waiting for work",
            new WorkerBacklogSnapshot(0, 0, null));

        var snapshot = monitor.GetSnapshot();
        var worker = Assert.Single(snapshot.Workers);

        Assert.Equal("healthy", snapshot.OverallStatus);
        Assert.Equal("Healthy", worker.Status);
        Assert.Equal("Waiting for work", worker.Activity);
    }

    [Fact]
    public void StaleCriticalWorker_MakesHealthCritical()
    {
        var clock = Clock();
        var monitor = Monitor(clock);
        monitor.ReportHeartbeat(WorkerRuntimeMonitor.AxataWorkerCode, "Waiting for work");

        clock.UtcNow = clock.UtcNow.AddSeconds(61);
        var snapshot = monitor.GetSnapshot();

        Assert.Equal("critical", snapshot.OverallStatus);
        Assert.Equal("Unhealthy", Assert.Single(snapshot.Workers).Status);
    }

    [Fact]
    public void OldAxataBacklog_EscalatesFromWarningToCritical()
    {
        var clock = Clock();
        var monitor = Monitor(clock);
        var queuedAtUtc = clock.UtcNow.AddMinutes(-6);
        monitor.ReportHeartbeat(
            WorkerRuntimeMonitor.AxataWorkerCode,
            "Waiting for work",
            new WorkerBacklogSnapshot(2, 0, queuedAtUtc));

        Assert.Equal("Degraded", Assert.Single(monitor.GetSnapshot().Workers).Status);

        monitor.ReportHeartbeat(
            WorkerRuntimeMonitor.AxataWorkerCode,
            "Waiting for work",
            new WorkerBacklogSnapshot(2, 0, clock.UtcNow.AddMinutes(-31)));

        Assert.Equal("Unhealthy", Assert.Single(monitor.GetSnapshot().Workers).Status);
    }

    [Fact]
    public void DisabledWorker_IsVisibleWithoutFailingHealth()
    {
        var clock = Clock();
        var monitor = Monitor(clock);

        monitor.ReportDisabled(WorkerRuntimeMonitor.AxataWorkerCode, "Disabled by configuration");
        clock.UtcNow = clock.UtcNow.AddHours(1);
        var snapshot = monitor.GetSnapshot();

        Assert.Equal("healthy", snapshot.OverallStatus);
        Assert.Equal("Disabled", Assert.Single(snapshot.Workers).Status);
    }

    [Fact]
    public void StaleNonCriticalWorker_IsOnlyDegraded()
    {
        var clock = Clock();
        var monitor = new WorkerRuntimeMonitor(clock);
        monitor.Register("scheduler", "Scheduler", isCritical: false, TimeSpan.FromMinutes(1));
        monitor.ReportHeartbeat("scheduler", "Waiting");

        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        var snapshot = monitor.GetSnapshot();

        Assert.Equal("warning", snapshot.OverallStatus);
        Assert.Equal("Degraded", Assert.Single(snapshot.Workers).Status);
    }

    private static WorkerRuntimeMonitor Monitor(MutableClock clock)
    {
        var monitor = new WorkerRuntimeMonitor(clock);
        monitor.Register(
            WorkerRuntimeMonitor.AxataWorkerCode,
            "AXATA Synchronization Worker",
            isCritical: true,
            TimeSpan.FromSeconds(60));
        return monitor;
    }

    private static MutableClock Clock() =>
        new(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc));

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
