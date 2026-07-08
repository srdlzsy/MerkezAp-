using System.Net;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed record MikroApiResult<TResponse>(
    bool IsError,
    int StatusCode,
    HttpStatusCode HttpStatusCode,
    string? ErrorMessage,
    string RawResponse,
    TResponse? Data,
    string RequestPath,
    int AttemptCount,
    TimeSpan Elapsed,
    Guid? AuditId = null,
    Guid? RequestId = null)
{
    public bool IsSuccess => !IsError;

    public TResponse EnsureSuccess()
    {
        if (IsError)
        {
            throw new MikroApiException(
                ErrorMessage ?? "Mikro API request failed.",
                RequestPath,
                HttpStatusCode,
                StatusCode,
                RawResponse);
        }

        return Data ?? throw new MikroApiException(
            "Mikro API response did not contain a deserializable body.",
            RequestPath,
            HttpStatusCode,
            StatusCode,
            RawResponse);
    }
}
