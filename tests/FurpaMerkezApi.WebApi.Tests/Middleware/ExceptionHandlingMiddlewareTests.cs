using System.Text.Json;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.WebApi.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData(OperationConflictErrorCodes.MikroWriteInProgress, true, OperationConflictMessages.MikroWriteInProgress)]
    [InlineData(OperationConflictErrorCodes.MikroWriteQueueBusy, true, OperationConflictMessages.MikroWriteQueueBusy)]
    [InlineData(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, true, OperationConflictMessages.MikroWriteOutcomeUnconfirmed)]
    [InlineData(OperationConflictErrorCodes.MikroDocumentContentMismatch, false, OperationConflictMessages.MikroDocumentContentMismatch)]
    [InlineData(OperationConflictErrorCodes.ClientRequestPayloadMismatch, false, OperationConflictMessages.ClientRequestPayloadMismatch)]
    public async Task InvokeAsync_WritesLocalizedClassifiedConflictExtensions(
        string errorCode,
        bool retryable,
        string expectedDetail)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new OperationConflictException(
                errorCode,
                "Raw technical error message.",
                retryable),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var root = body.RootElement;
        Assert.Equal(errorCode, root.GetProperty("errorCode").GetString());
        Assert.Equal(retryable, root.GetProperty("retryable").GetBoolean());
        Assert.Equal(expectedDetail, root.GetProperty("detail").GetString());
    }
}
