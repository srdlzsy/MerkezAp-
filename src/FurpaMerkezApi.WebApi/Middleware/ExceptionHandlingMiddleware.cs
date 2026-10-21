using System.ServiceModel;
using System.Text.Json;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc;
using FurpaMerkezApi.WebApi.Security;

namespace FurpaMerkezApi.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions ProblemDetailsJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (Exception exception) when (SqlServerExceptionClassifier.IsConnectivityFailure(exception))
        {
            logger.LogError(exception, "A SQL Server connectivity error occurred.");
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                "Veritabani servisine gecici olarak ulasilamiyor. Lutfen kisa bir sure sonra tekrar deneyin.");
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(exception, "A validation error occurred.");
            await WriteProblemDetailsAsync(httpContext, StatusCodes.Status400BadRequest, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogInformation(exception, "An authentication error occurred.");
            await WriteProblemDetailsAsync(httpContext, StatusCodes.Status401Unauthorized, exception.Message);
        }
        catch (ForbiddenAccessException exception)
        {
            logger.LogInformation(exception, "An authorization error occurred.");
            await WriteProblemDetailsAsync(httpContext, StatusCodes.Status403Forbidden, exception.Message);
        }
        catch (OperationConflictException exception)
        {
            logger.LogInformation(
                exception,
                "A classified operation conflict occurred. ErrorCode={ErrorCode}, Retryable={Retryable}",
                exception.ErrorCode,
                exception.Retryable);
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status409Conflict,
                OperationConflictMessages.Resolve(exception.ErrorCode, exception.Message)
                    ?? exception.Message,
                exception.ErrorCode,
                exception.Retryable,
                exception.RelatedClientRequestId);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogInformation(exception, "A conflict error occurred.");
            await WriteProblemDetailsAsync(httpContext, StatusCodes.Status409Conflict, exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            logger.LogInformation(exception, "A requested resource was not found.");
            await WriteProblemDetailsAsync(httpContext, StatusCodes.Status404NotFound, exception.Message);
        }
        catch (CommunicationException exception)
        {
            logger.LogWarning(exception, "An upstream communication error occurred.");
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status502BadGateway,
                BuildUpstreamErrorDetail("Dis servis baglantisi kurulamadi.", exception));
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "An upstream HTTP error occurred.");
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status502BadGateway,
                BuildUpstreamErrorDetail("Dis servis HTTP istegi basarisiz oldu.", exception));
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, "An upstream timeout occurred.");
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status504GatewayTimeout,
                "Islem zamaninda tamamlanamadi. Lutfen kisa bir sure sonra tekrar deneyin.");
        }
        catch (TaskCanceledException exception) when (!httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning(exception, "An upstream request was canceled or timed out.");
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status504GatewayTimeout,
                BuildUpstreamErrorDetail("Dis servis istegi zaman asimina ugradi.", exception));
        }
        catch (OperationCanceledException exception) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation(exception, "The client aborted the request.");
        }
        catch (ConnectionResetException exception)
        {
            logger.LogInformation(
                exception,
                "The client connection was reset before the request could be completed.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unexpected error occurred.");
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "Islem sirasinda beklenmeyen bir hata olustu. Lutfen tekrar deneyin; sorun surerse destek kodunu yetkiliye iletin.");
        }
    }

    private static Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        int statusCode,
        string detail,
        string? errorCode = null,
        bool? retryable = null,
        Guid? relatedClientRequestId = null)
    {
        if (httpContext.RequestAborted.IsCancellationRequested || httpContext.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = GetUserFacingDetail(statusCode, detail),
            Instance = httpContext.Request.Path
        };

        var correlationId = RequestCorrelation.GetCurrent(httpContext);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            problemDetails.Extensions["errorCode"] = errorCode;
            var clientDecision = SafeWriteClientContract.Resolve("Failed", errorCode, retryable);
            problemDetails.Extensions["recoveryState"] = clientDecision.RecoveryState;
            problemDetails.Extensions["recommendedAction"] = clientDecision.RecommendedAction;
        }

        if (retryable.HasValue)
        {
            problemDetails.Extensions["retryable"] = retryable.Value;
        }

        if (relatedClientRequestId.HasValue)
        {
            problemDetails.Extensions["relatedClientRequestId"] = relatedClientRequestId.Value;
        }

        return httpContext.Response.WriteAsJsonAsync(problemDetails, ProblemDetailsJsonOptions);
    }

    private static string GetTitle(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "Gecersiz istek",
            StatusCodes.Status401Unauthorized => "Oturum gecersiz",
            StatusCodes.Status403Forbidden => "Yetki yetersiz",
            StatusCodes.Status404NotFound => "Kayit bulunamadi",
            StatusCodes.Status409Conflict => "Islem cakismasi",
            StatusCodes.Status502BadGateway => "Dis servis hatasi",
            StatusCodes.Status503ServiceUnavailable => "Servis gecici olarak kullanilamiyor",
            StatusCodes.Status504GatewayTimeout => "Islem zaman asimina ugradi",
            _ => "Sunucu hatasi"
        };

    private static string GetUserFacingDetail(int statusCode, string detail)
    {
        if (!LooksTechnicalOrEnglish(detail))
        {
            return detail;
        }

        return statusCode switch
        {
            StatusCodes.Status400BadRequest =>
                "Gonderilen bilgiler gecersiz. Eksik veya hatali alanlari kontrol edin.",
            StatusCodes.Status401Unauthorized =>
                "Oturumunuz gecersiz veya suresi dolmus. Lutfen tekrar giris yapin.",
            StatusCodes.Status403Forbidden =>
                "Bu islemi yapmak icin yetkiniz bulunmuyor.",
            StatusCodes.Status404NotFound =>
                "Aradiginiz kayit bulunamadi. Listeyi yenileyip tekrar kontrol edin.",
            StatusCodes.Status409Conflict =>
                "Islem mevcut bir kayitla veya devam eden baska bir islemle cakisti. Ekrani yenileyip tekrar kontrol edin.",
            StatusCodes.Status502BadGateway =>
                "Bagli dis servisten gecerli yanit alinamadi. Lutfen kisa bir sure sonra tekrar deneyin.",
            StatusCodes.Status503ServiceUnavailable =>
                "Sunucu veya veritabani gecici olarak kullanilamiyor. Lutfen kisa bir sure sonra tekrar deneyin.",
            StatusCodes.Status504GatewayTimeout =>
                "Islem zamaninda tamamlanamadi. Lutfen kisa bir sure sonra tekrar deneyin.",
            _ =>
                "Islem sirasinda beklenmeyen bir hata olustu. Lutfen tekrar deneyin; sorun surerse destek kodunu yetkiliye iletin."
        };
    }

    private static bool LooksTechnicalOrEnglish(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return true;
        }

        var normalized = detail.ToLowerInvariant();
        string[] markers =
        [
            " is required",
            " was not found",
            " must be ",
            " can not ",
            " cannot ",
            " could not ",
            " does not ",
            " did not ",
            " already exists",
            " at least ",
            " invalid ",
            " is invalid",
            "invalid or expired",
            " failed",
            " unexpected error",
            "execution strategy",
            "user-initiated transaction",
            "null reference",
            "null value",
            "stack trace"
        ];

        return markers.Any(normalized.Contains);
    }

    private static string BuildUpstreamErrorDetail(string prefix, Exception exception)
    {
        var rootCause = exception.GetBaseException();
        var message = string.IsNullOrWhiteSpace(rootCause.Message)
            ? exception.Message
            : rootCause.Message;

        return string.IsNullOrWhiteSpace(message)
            ? prefix
            : $"{prefix} {message}";
    }
}
