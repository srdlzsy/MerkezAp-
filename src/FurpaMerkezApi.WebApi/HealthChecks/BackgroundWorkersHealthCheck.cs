using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FurpaMerkezApi.WebApi.HealthChecks;

public sealed class BackgroundWorkersHealthCheck(
    IWorkerMonitoringService monitoringService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var snapshot = monitoringService.GetSnapshot();
        var data = new Dictionary<string, object>
        {
            ["overallStatus"] = snapshot.OverallStatus,
            ["workerCount"] = snapshot.Workers.Count,
            ["workers"] = snapshot.Workers
        };

        var result = snapshot.OverallStatus switch
        {
            "critical" => HealthCheckResult.Unhealthy(
                "One or more critical background workers are stale or have a critical backlog.",
                data: data),
            "warning" => HealthCheckResult.Degraded(
                "One or more background workers require attention.",
                data: data),
            _ => HealthCheckResult.Healthy("Background workers are operating normally.", data)
        };

        return Task.FromResult(result);
    }
}
