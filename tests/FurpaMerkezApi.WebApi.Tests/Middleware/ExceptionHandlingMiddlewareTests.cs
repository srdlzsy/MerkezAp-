using System.Text.Json;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.WebApi.Middleware;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_DoesNotTurnClientConnectionResetIntoServerError()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new ConnectionResetException("The client has disconnected."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Theory]
    [InlineData(OperationConflictErrorCodes.MikroWriteInProgress, true, OperationConflictMessages.MikroWriteInProgress, "Checking", "WaitAndCheck")]
    [InlineData(OperationConflictErrorCodes.MikroWriteQueueBusy, true, OperationConflictMessages.MikroWriteQueueBusy, "NotStarted", "RetrySameRequest")]
    [InlineData(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, true, OperationConflictMessages.MikroWriteOutcomeUnconfirmed, "Unknown", "WaitAndCheck")]
    [InlineData(OperationConflictErrorCodes.MikroDocumentIncomplete, true, OperationConflictMessages.MikroDocumentIncomplete, "Incomplete", "WaitAndCheck")]
    [InlineData(OperationConflictErrorCodes.MikroDocumentContentMismatch, false, OperationConflictMessages.MikroDocumentContentMismatch, "Mismatch", "RecheckExisting")]
    [InlineData(OperationConflictErrorCodes.ClientRequestPayloadMismatch, false, OperationConflictMessages.ClientRequestPayloadMismatch, "RequestChanged", "RestoreOriginalRequest")]
    [InlineData(OperationConflictErrorCodes.SafeWriteRetryAllowed, true, OperationConflictMessages.SafeWriteRetryAllowed, "Missing", "RetrySameRequest")]
    public async Task InvokeAsync_WritesLocalizedClassifiedConflictExtensions(
        string errorCode,
        bool retryable,
        string expectedDetail,
        string expectedRecoveryState,
        string expectedRecommendedAction)
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
        Assert.Equal(expectedRecoveryState, root.GetProperty("recoveryState").GetString());
        Assert.Equal(expectedRecommendedAction, root.GetProperty("recommendedAction").GetString());
    }
}
