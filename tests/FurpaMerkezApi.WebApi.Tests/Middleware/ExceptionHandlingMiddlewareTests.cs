using System.Text.Json;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WritesClassifiedConflictExtensions()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new OperationConflictException(
                OperationConflictErrorCodes.MikroDocumentContentMismatch,
                "Manual review is required.",
                retryable: false),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var root = body.RootElement;
        Assert.Equal(
            OperationConflictErrorCodes.MikroDocumentContentMismatch,
            root.GetProperty("errorCode").GetString());
        Assert.False(root.GetProperty("retryable").GetBoolean());
        Assert.Equal("Manual review is required.", root.GetProperty("detail").GetString());
    }
}
