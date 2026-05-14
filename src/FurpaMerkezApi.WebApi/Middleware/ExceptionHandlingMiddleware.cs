using Microsoft.AspNetCore.Mvc;

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
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status404NotFound => "Not Found",
            _ => "Internal Server Error"
        };
}
