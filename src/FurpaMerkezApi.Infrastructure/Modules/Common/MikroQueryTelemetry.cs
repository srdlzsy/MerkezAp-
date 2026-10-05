using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.Common;

internal static class MikroQueryTelemetry
{
    private const double SlowQueryThresholdMilliseconds = 1_000d;

    public static async Task<List<T>> ToMeasuredListAsync<T>(
        this IQueryable<T> query,
        ILogger logger,
        string queryName,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var rows = await query
                .TagWith($"Furpa:{queryName}")
                .ToListAsync(cancellationToken);

            LogCompleted(logger, queryName, stopwatch.Elapsed.TotalMilliseconds, rows.Count);
            return rows;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Critical Mikro query {QueryName} failed after {ElapsedMs} ms.",
                queryName,
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    public static async Task<int> CountMeasuredAsync<T>(
        this IQueryable<T> query,
        ILogger logger,
        string queryName,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var count = await query
                .TagWith($"Furpa:{queryName}")
                .CountAsync(cancellationToken);

            LogCompleted(logger, queryName, stopwatch.Elapsed.TotalMilliseconds, count);
            return count;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Critical Mikro query {QueryName} failed after {ElapsedMs} ms.",
                queryName,
                stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    private static void LogCompleted(
        ILogger logger,
        string queryName,
        double elapsedMilliseconds,
        int resultCount)
    {
        if (elapsedMilliseconds >= SlowQueryThresholdMilliseconds)
        {
            logger.LogWarning(
                "Slow Mikro query {QueryName} completed in {ElapsedMs} ms with {ResultCount} result rows.",
                queryName,
                elapsedMilliseconds,
                resultCount);
            return;
        }

        logger.LogInformation(
            "Mikro query {QueryName} completed in {ElapsedMs} ms with {ResultCount} result rows.",
            queryName,
            elapsedMilliseconds,
            resultCount);
    }
}
