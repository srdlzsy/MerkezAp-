using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Axata;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Shopigo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FurpaMerkezApi.WebApi.HealthChecks;

public sealed class CoreDependenciesHealthCheck(
    IServiceProvider serviceProvider,
    ILogger<CoreDependenciesHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var failures = new List<string>();

        await CheckDbContextAsync("authDb", scope.ServiceProvider.GetRequiredService<AuthDbContext>(), data, failures, cancellationToken);
        await CheckDbContextAsync("furpaDb", scope.ServiceProvider.GetRequiredService<FurpaDbContext>(), data, failures, cancellationToken);
        await CheckDbContextAsync("mikroReadDb", scope.ServiceProvider.GetRequiredService<MikroDbContext>(), data, failures, cancellationToken);
        await CheckDbContextAsync("mikroWriteDb", scope.ServiceProvider.GetRequiredService<MikroWriteDbContext>(), data, failures, cancellationToken);

        var axataDbContext = scope.ServiceProvider.GetService<AxataDbContext>();

        if (axataDbContext is not null)
        {
            await CheckDbContextAsync("axataDb", axataDbContext, data, failures, cancellationToken);
        }

        var shopigoDbContext = scope.ServiceProvider.GetService<ShopigoCiroDbContext>();

        if (shopigoDbContext is not null)
        {
            await CheckDbContextAsync("shopigoCiroDb", shopigoDbContext, data, failures, cancellationToken);
        }

        if (failures.Count > 0)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "One or more core dependencies are unavailable.",
                data: data);
        }

        return HealthCheckResult.Healthy("All core dependencies are reachable.", data);
    }

    private async Task CheckDbContextAsync(
        string dependencyName,
        DbContext dbContext,
        IDictionary<string, object> data,
        ICollection<string> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            data[dependencyName] = canConnect ? "Healthy" : "Unavailable";

            if (!canConnect)
            {
                failures.Add(dependencyName);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Readiness check failed for dependency {DependencyName}.", dependencyName);
            data[dependencyName] = "Unavailable";
            failures.Add(dependencyName);
        }
    }
}
