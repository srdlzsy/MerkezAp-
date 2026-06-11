using System.Net;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroApiException : Exception
{
    public MikroApiException(string message)
        : base(message)
    {
    }

    public MikroApiException(
        string message,
        string? requestPath,
        HttpStatusCode? httpStatusCode,
        int? statusCode,
        string? rawResponse,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RequestPath = requestPath;
        HttpStatusCode = httpStatusCode;
        StatusCode = statusCode;
        RawResponse = rawResponse;
    }

    public string? RequestPath { get; }

    public HttpStatusCode? HttpStatusCode { get; }

    public int? StatusCode { get; }

    public string? RawResponse { get; }
}
