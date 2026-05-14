using System.Diagnostics;

namespace FurpaMerkezApi.WebApi.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(httpContext);
        }
        finally
        {
            stopwatch.Stop();

            var logLevel = ResolveLogLevel(httpContext.Response.StatusCode);
            var queryString = httpContext.Request.QueryString.HasValue
                ? httpContext.Request.QueryString.Value
                : string.Empty;

            logger.Log(
                logLevel,
                "HTTP {Method} {Path}{QueryString} responded {StatusCode} in {ElapsedMilliseconds} ms",
                httpContext.Request.Method,
                httpContext.Request.Path,
                queryString,
                httpContext.Response.StatusCode,
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2));
        }
    }

    private static LogLevel ResolveLogLevel(int statusCode) =>
        statusCode >= 500
            ? LogLevel.Error
            : statusCode >= 400
                ? LogLevel.Warning
                : LogLevel.Information;
}
