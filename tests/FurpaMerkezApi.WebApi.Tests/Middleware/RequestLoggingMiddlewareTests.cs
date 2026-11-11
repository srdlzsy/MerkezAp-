using FurpaMerkezApi.WebApi.Middleware;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    [Theory]
    [InlineData(200, LogLevel.Information)]
    [InlineData(401, LogLevel.Information)]
    [InlineData(404, LogLevel.Information)]
    [InlineData(400, LogLevel.Warning)]
    [InlineData(403, LogLevel.Warning)]
    [InlineData(409, LogLevel.Warning)]
    [InlineData(500, LogLevel.Error)]
    [InlineData(504, LogLevel.Error)]
    public void ResolveLogLevel_SeparatesExpectedControlResponsesFromFailures(
        int statusCode,
        LogLevel expected)
    {
        Assert.Equal(expected, RequestLoggingMiddleware.ResolveLogLevel(statusCode));
    }
}
