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
        Assert.Equal("Saglikli", worker.StatusName);
        Assert.False(worker.RequiresAttention);
        Assert.False(worker.HasUnresolvedFailure);
        Assert.Equal("Mudahale gerekmiyor.", worker.Recommendation);
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
        var worker = Assert.Single(snapshot.Workers);
        Assert.Equal("Unhealthy", worker.Status);
        Assert.True(worker.RequiresAttention);
        Assert.Contains("Heartbeat", worker.Recommendation);
    }

    [Fact]
    public void LatestFailure_ExplainsThatWorkerIsStillRunning()
    {
        var clock = Clock();
        var monitor = Monitor(clock);
        monitor.ReportSuccess(WorkerRuntimeMonitor.AxataWorkerCode, "Completed");
        clock.UtcNow = clock.UtcNow.AddSeconds(5);
        monitor.ReportFailure(
            WorkerRuntimeMonitor.AxataWorkerCode,
            "Waiting for work",
            new InvalidOperationException("17 documents failed"));

        var worker = Assert.Single(monitor.GetSnapshot().Workers);

        Assert.Equal("Degraded", worker.Status);
        Assert.Equal("Dikkat Gerekiyor", worker.StatusName);
        Assert.True(worker.HasUnresolvedFailure);
        Assert.True(worker.RequiresAttention);
        Assert.Contains("Worker calisiyor", worker.Recommendation);
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

    [Fact]
    public void BacklogSnapshot_ExposesCurrentAndLastJobDetails()
    {
        var clock = Clock();
        var monitor = Monitor(clock);
        var currentJobId = Guid.NewGuid();
        var lastJobId = Guid.NewGuid();
        var currentJob = new WorkerJobRuntimeSnapshot(
            currentJobId,
            "g02-inbound-delivery-import",
            "G02 Depo Mal Kabul Aktarimi",
            "Running",
            "Live",
            "Scheduled",
            null,
            clock.UtcNow.AddSeconds(-20),
            clock.UtcNow.AddSeconds(-15),
            null,
            0,
            "Senkronizasyon gorevi calisiyor.",
            null);
        var lastJob = new WorkerJobRuntimeSnapshot(
            lastJobId,
            "g01-inbound-atf-import",
            "G01 Firma Mal Kabul Aktarimi",
            "Succeeded",
            "Live",
            "Scheduled",
            null,
            clock.UtcNow.AddMinutes(-2),
            clock.UtcNow.AddMinutes(-2),
            clock.UtcNow.AddMinutes(-1),
            12,
            "12 belge aktarildi.",
            null);

        monitor.ReportHeartbeat(
            WorkerRuntimeMonitor.AxataWorkerCode,
            "G02 aktarimi calisiyor",
            new WorkerBacklogSnapshot(0, 1, null, currentJob, lastJob, 1, lastJob with
            {
                Status = "Failed",
                ErrorMessage = "G01 aktarimi tamamlanamadi."
            }));

        var worker = Assert.Single(monitor.GetSnapshot().Workers);

        Assert.Equal(currentJobId, worker.CurrentJob?.JobId);
        Assert.Equal("g02-inbound-delivery-import", worker.CurrentJob?.TaskCode);
        Assert.Equal(15, worker.CurrentJob?.DurationSeconds);
        Assert.Equal(lastJobId, worker.LastJob?.JobId);
        Assert.Equal("Succeeded", worker.LastJob?.Status);
        Assert.Equal(12, worker.LastJob?.AffectedRecordCount);
        Assert.Equal(60, worker.LastJob?.DurationSeconds);
        Assert.True(worker.HasUnresolvedFailure);
        Assert.Equal(1, worker.UnresolvedFailureCount);
        Assert.Equal(lastJobId, worker.LastFailedJob?.JobId);
        Assert.Equal("G01 aktarimi tamamlanamadi.", worker.LastError);
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
