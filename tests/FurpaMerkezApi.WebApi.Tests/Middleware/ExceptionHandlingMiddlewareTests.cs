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
    public async Task InvokeAsync_WritesClearCompanyProductCompatibilityWarning()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new CompanyProductCompatibilityException(
                "Firma mal kabul",
                143,
                "32000257",
                new DateTime(2026, 11, 6),
                ["024257", "024254"]),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/mal-kabul-islemleri/firma-mal-kabulleri";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var root = body.RootElement;
        var detail = root.GetProperty("detail").GetString();
        Assert.Equal("Gecersiz istek", root.GetProperty("title").GetString());
        Assert.Contains("Firma mal kabul durduruldu", detail);
        Assert.Contains("cari 32000257", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("depo 143", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("06.11.2026", detail);
        Assert.Contains("024254, 024257", detail);
        Assert.Contains("Mikro'ya evrak yazilmadi", detail);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotTurnSqlFailureAfterClientAbortIntoConnectivityError()
    {
        using var requestCancellation = new CancellationTokenSource();
        requestCancellation.Cancel();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("Operation cancelled by user."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.RequestAborted = requestCancellation.Token;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

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
        Assert.Equal("Islem cakismasi", root.GetProperty("title").GetString());
        Assert.Equal(errorCode, root.GetProperty("errorCode").GetString());
        Assert.Equal(retryable, root.GetProperty("retryable").GetBoolean());
        Assert.Equal(expectedDetail, root.GetProperty("detail").GetString());
        Assert.Equal(expectedRecoveryState, root.GetProperty("recoveryState").GetString());
        Assert.Equal(expectedRecommendedAction, root.GetProperty("recommendedAction").GetString());
    }

    [Fact]
    public async Task InvokeAsync_HidesTechnicalConflictBehindTurkishMessage()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException(
                "The configured execution strategy does not support user-initiated transactions."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var root = body.RootElement;
        Assert.Equal("Islem cakismasi", root.GetProperty("title").GetString());
        Assert.Equal(
            "Islem mevcut bir kayitla veya devam eden baska bir islemle cakisti. Ekrani yenileyip tekrar kontrol edin.",
            root.GetProperty("detail").GetString());
        Assert.DoesNotContain("execution strategy", root.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokeAsync_PreservesPosSequenceConflictDetails()
    {
        const string detail =
            "Mikro POS evrak siralari uyumsuz: sube 111, stok PS111/11850 (04.11.2026), cari PS111/11849 (29.10.2026).";
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new OperationConflictException(
                OperationConflictErrorCodes.MikroPosDocumentSequenceConflict,
                detail,
                retryable: false),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/kasa-islemleri/kasa-hareket-aktarimi/mikro/aktar";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        var root = body.RootElement;
        Assert.Equal(detail, root.GetProperty("detail").GetString());
        Assert.Equal(
            OperationConflictErrorCodes.MikroPosDocumentSequenceConflict,
            root.GetProperty("errorCode").GetString());
        Assert.Equal("SequenceMismatch", root.GetProperty("recoveryState").GetString());
        Assert.Equal("NeedsReview", root.GetProperty("recommendedAction").GetString());
    }
}
