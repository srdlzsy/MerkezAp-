namespace FurpaMerkezApi.WebApi.Middleware;

public static class RequestCorrelation
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public static string? GetCurrent(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(ItemKey, out var value)
            ? value as string
            : null;
}
