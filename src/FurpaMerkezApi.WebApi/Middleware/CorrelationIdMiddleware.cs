namespace FurpaMerkezApi.WebApi.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationId = ResolveCorrelationId(
            httpContext.Request.Headers[RequestCorrelation.HeaderName].FirstOrDefault());

        httpContext.Items[RequestCorrelation.ItemKey] = correlationId;
        httpContext.TraceIdentifier = correlationId;
        httpContext.Response.Headers[RequestCorrelation.HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(httpContext);
        }
    }

    private static string ResolveCorrelationId(string? requestedCorrelationId)
    {
        if (!string.IsNullOrWhiteSpace(requestedCorrelationId))
        {
            var normalized = requestedCorrelationId.Trim();

            if (normalized.Length <= MaxCorrelationIdLength &&
                !normalized.Contains('\r') &&
                !normalized.Contains('\n'))
            {
                return normalized;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
