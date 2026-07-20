using System.ServiceModel;
using Microsoft.AspNetCore.Mvc;
using FurpaMerkezApi.WebApi.Security;

namespace FurpaMerkezApi.WebApi.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
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
            await WriteProblemDetailsAsync(httpContext, StatusCodes.Status504GatewayTimeout, exception.Message);
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
        catch (Exception exception)
        {
            logger.LogError(exception, "An unexpected error occurred.");
            await WriteProblemDetailsAsync(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred while processing the request.");
        }
    }

    private static Task WriteProblemDetailsAsync(HttpContext httpContext, int statusCode, string detail)
    {
        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(statusCode),
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        var correlationId = RequestCorrelation.GetCurrent(httpContext);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problemDetails.Extensions["correlationId"] = correlationId;
        }

        return httpContext.Response.WriteAsJsonAsync(problemDetails);
    }

    private static string GetTitle(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status401Unauthorized => "Unauthorized",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status502BadGateway => "Bad Gateway",
            StatusCodes.Status504GatewayTimeout => "Gateway Timeout",
            _ => "Internal Server Error"
        };

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
