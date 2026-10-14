using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.WebApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.HealthChecks;

public sealed class BackgroundWorkersHealthCheckTests
{
    [Theory]
    [InlineData("healthy", HealthStatus.Healthy)]
    [InlineData("warning", HealthStatus.Degraded)]
    [InlineData("critical", HealthStatus.Unhealthy)]
    public async Task CheckHealthAsync_MapsMonitoringStatus(
        string overallStatus,
        HealthStatus expectedStatus)
    {
        var snapshot = new WorkerMonitoringSnapshotDto(
            DateTime.UtcNow,
            overallStatus,
            Array.Empty<WorkerMonitoringItemDto>());
        var healthCheck = new BackgroundWorkersHealthCheck(
            new StubWorkerMonitoringService(snapshot));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(overallStatus, result.Data["overallStatus"]);
    }

    private sealed class StubWorkerMonitoringService(WorkerMonitoringSnapshotDto snapshot)
        : IWorkerMonitoringService
    {
        public WorkerMonitoringSnapshotDto GetSnapshot() => snapshot;
    }
}
