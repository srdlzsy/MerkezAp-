using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.WebApi.HealthChecks;

public sealed class OperationsExportWritableHealthCheck(
    IOptions<OperationsExportOptions> options,
    ILogger<OperationsExportWritableHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var basePath = OperationsExportPathResolver.ResolveBasePath(options.Value);
        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["basePath"] = basePath
        };

        try
        {
            Directory.CreateDirectory(basePath);

            var testFilePath = Path.Combine(basePath, $".write-test-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(testFilePath, "ok", cancellationToken);
            File.Delete(testFilePath);

            return HealthCheckResult.Healthy("Operations export directory is writable.", data);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            logger.LogError(exception, "Operations export directory is not writable: {BasePath}.", basePath);
            data["error"] = exception.Message;

            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Operations export directory is not writable.",
                exception,
                data);
        }
    }
}
