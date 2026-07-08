using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroApiClient(
    HttpClient httpClient,
    MikroApiAuthBlockFactory authBlockFactory,
    MikroApiWriteAuditService writeAuditService,
    IOptionsMonitor<MikroApiOptions> options,
    ILogger<MikroApiClient> logger)
{
    public const string ClientName = "MikroApi";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    public Task<MikroApiResult<TResponse>> GetAsync<TResponse>(
        string path,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(HttpMethod.Get, path, payload: null, auditPayload: null, auditWrite: false, cancellationToken);

    public Task<MikroApiResult<TResponse>> PostAsync<TResponse>(
        string path,
        object? payload,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(HttpMethod.Post, path, payload, payload, auditWrite: true, cancellationToken);

    public Task<MikroApiResult<TResponse>> PostWithMikroEnvelopeAsync<TResponse>(
        string path,
        object? payload = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(
            HttpMethod.Post,
            path,
            authBlockFactory.CreateEnvelopePayload(payload),
            payload,
            auditWrite: true,
            cancellationToken);

    public Task<MikroApiResult<TResponse>> PostWithMikroPayloadAsync<TResponse>(
        string path,
        object? mikroPayload = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(
            HttpMethod.Post,
            path,
            authBlockFactory.CreateMikroPayload(mikroPayload),
            mikroPayload,
            auditWrite: true,
            cancellationToken);

    public Task<MikroApiResult<TResponse>> PostLoginAsync<TResponse>(
        string path = "/Api/APIMethods/APILogin",
        CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(
            HttpMethod.Post,
            path,
            authBlockFactory.CreateLoginPayload(),
            auditPayload: null,
            auditWrite: false,
            cancellationToken);

    private async Task<MikroApiResult<TResponse>> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        object? payload,
        object? auditPayload,
        bool auditWrite,
        CancellationToken cancellationToken)
    {
        EnsureBaseUrlConfigured();

        var normalizedPath = NormalizePath(path);
        var requestJson = payload is null
            ? null
            : JsonSerializer.Serialize(payload, JsonOptions);
        var auditPayloadJson = auditPayload is null
            ? null
            : JsonSerializer.Serialize(auditPayload, JsonOptions);
        var auditHandle = auditWrite
            ? await writeAuditService.BeginAsync(normalizedPath, auditPayloadJson, cancellationToken)
            : new MikroApiWriteAuditHandle(null, Guid.NewGuid());
        var currentOptions = options.CurrentValue;
        var maxAttempts = ResolveMaxAttempts(method, currentOptions);
        var stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = CreateRequest(method, normalizedPath, requestJson);

            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                var rawResponse = response.Content is null
                    ? string.Empty
                    : await response.Content.ReadAsStringAsync(cancellationToken);
                var elapsed = stopwatch.Elapsed;

                LogResponse(method, normalizedPath, response, rawResponse, attempt, maxAttempts, elapsed);

                if (ShouldRetry(response.StatusCode) && attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(attempt, currentOptions, cancellationToken);
                    continue;
                }

                var result = CreateResult<TResponse>(
                    normalizedPath,
                    response.StatusCode,
                    rawResponse,
                    attempt,
                    elapsed);

                return await CompleteAuditAsync(
                    result,
                    auditHandle,
                    auditWrite,
                    cancellationToken);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = exception;
                logger.LogWarning(
                    exception,
                    "Mikro API {Method} {Path} timed out on attempt {Attempt}/{MaxAttempts}.",
                    method,
                    normalizedPath,
                    attempt,
                    maxAttempts);

                if (attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(attempt, currentOptions, cancellationToken);
                    continue;
                }
            }
            catch (HttpRequestException exception)
            {
                lastException = exception;
                logger.LogWarning(
                    exception,
                    "Mikro API {Method} {Path} failed on attempt {Attempt}/{MaxAttempts}.",
                    method,
                    normalizedPath,
                    attempt,
                    maxAttempts);

                if (attempt < maxAttempts)
                {
                    await DelayBeforeRetryAsync(attempt, currentOptions, cancellationToken);
                    continue;
                }
            }
        }

        var failureMessage = lastException?.Message ?? "Mikro API request failed.";

        var failureResult = new MikroApiResult<TResponse>(
            true,
            0,
            0,
            failureMessage,
            string.Empty,
            default,
            normalizedPath,
            maxAttempts,
            stopwatch.Elapsed);

        return await CompleteAuditAsync(
            failureResult,
            auditHandle,
            auditWrite,
            cancellationToken);
    }

    public Task MarkRecoveredAsync<TResponse>(
        MikroApiResult<TResponse> result,
        string? documentNo,
        Guid? recoveredGuid = null,
        Guid? documentFlowId = null,
        CancellationToken cancellationToken = default) =>
        writeAuditService.MarkRecoveredAsync(
            result.AuditId,
            documentNo,
            recoveredGuid,
            documentFlowId,
            cancellationToken);

    private async Task<MikroApiResult<TResponse>> CompleteAuditAsync<TResponse>(
        MikroApiResult<TResponse> result,
        MikroApiWriteAuditHandle auditHandle,
        bool auditWrite,
        CancellationToken cancellationToken)
    {
        var enrichedResult = result with
        {
            AuditId = auditHandle.AuditId,
            RequestId = auditHandle.RequestId
        };

        if (auditWrite)
        {
            await writeAuditService.CompleteAsync(auditHandle, enrichedResult, cancellationToken);
        }

        return enrichedResult;
    }

    private void EnsureBaseUrlConfigured()
    {
        if (httpClient.BaseAddress is not null)
        {
            return;
        }

        var currentOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(currentOptions.BaseUrl))
        {
            throw new MikroApiException("MikroApi:BaseUrl is not configured.");
        }

        if (!Uri.TryCreate(currentOptions.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new MikroApiException("MikroApi:BaseUrl must be an absolute HTTP or HTTPS URL.");
        }

        httpClient.BaseAddress = baseUri;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string? requestJson)
    {
        var request = new HttpRequestMessage(method, path);

        if (requestJson is not null)
        {
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static MikroApiResult<TResponse> CreateResult<TResponse>(
        string path,
        HttpStatusCode httpStatusCode,
        string rawResponse,
        int attemptCount,
        TimeSpan elapsed)
    {
        var responseInfo = ParseResponseInfo(rawResponse);
        var statusCode = responseInfo.StatusCode ?? (int)httpStatusCode;
        var isError = !IsHttpSuccess(httpStatusCode) || responseInfo.IsError == true;
        var errorMessage = ResolveErrorMessage(httpStatusCode, responseInfo, isError);
        var data = DeserializeResponse<TResponse>(rawResponse, path, httpStatusCode, statusCode, out var deserializeError);

        if (deserializeError is not null)
        {
            isError = true;
            errorMessage = deserializeError.Message;
        }

        return new MikroApiResult<TResponse>(
            isError,
            statusCode,
            httpStatusCode,
            errorMessage,
            rawResponse,
            data,
            path,
            attemptCount,
            elapsed);
    }

    private static TResponse? DeserializeResponse<TResponse>(
        string rawResponse,
        string path,
        HttpStatusCode httpStatusCode,
        int statusCode,
        out MikroApiException? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return default;
        }

        if (typeof(TResponse) == typeof(string))
        {
            return (TResponse)(object)rawResponse;
        }

        try
        {
            return JsonSerializer.Deserialize<TResponse>(rawResponse, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = new MikroApiException(
                "Mikro API response could not be deserialized.",
                path,
                httpStatusCode,
                statusCode,
                rawResponse,
                exception);

            return default;
        }
    }

    private static MikroApiResponseInfo ParseResponseInfo(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return new MikroApiResponseInfo(null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(rawResponse);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new MikroApiResponseInfo(null, null, null);
            }

            var isError = TryGetProperty(document.RootElement, "IsError", out var isErrorElement)
                ? ReadBoolean(isErrorElement)
                : null;
            var statusCode = TryGetProperty(document.RootElement, "StatusCode", out var statusCodeElement)
                ? ReadInt32(statusCodeElement)
                : null;
            var errorMessage =
                TryGetStringProperty(document.RootElement, "ErrorMessage") ??
                TryGetStringProperty(document.RootElement, "Message") ??
                TryGetStringProperty(document.RootElement, "HataMesaji") ??
                TryGetStringProperty(document.RootElement, "Error") ??
                TryGetStringProperty(document.RootElement, "Aciklama");

            return new MikroApiResponseInfo(isError, statusCode, errorMessage);
        }
        catch (JsonException)
        {
            return new MikroApiResponseInfo(null, null, null);
        }
    }

    private static string? ResolveErrorMessage(
        HttpStatusCode httpStatusCode,
        MikroApiResponseInfo responseInfo,
        bool isError)
    {
        if (!isError)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(responseInfo.ErrorMessage))
        {
            return responseInfo.ErrorMessage;
        }

        if (!IsHttpSuccess(httpStatusCode))
        {
            return $"Mikro API returned HTTP {(int)httpStatusCode} {httpStatusCode}.";
        }

        return "Mikro API returned an error response.";
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) ||
                property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool? ReadBoolean(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(element.GetString(), out var value) => value,
            JsonValueKind.Number when element.TryGetInt32(out var value) => value != 0,
            _ => null
        };
    }

    private static int? ReadInt32(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), out var value) => value,
            _ => null
        };
    }

    private void LogResponse(
        HttpMethod method,
        string path,
        HttpResponseMessage response,
        string rawResponse,
        int attempt,
        int maxAttempts,
        TimeSpan elapsed)
    {
        var sanitizedBody = SanitizeForLog(rawResponse, options.CurrentValue.MaxLoggedBodyLength);

        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "Mikro API {Method} {Path} returned HTTP {StatusCode} in {ElapsedMilliseconds} ms on attempt {Attempt}/{MaxAttempts}. Body: {Body}",
                method,
                path,
                (int)response.StatusCode,
                elapsed.TotalMilliseconds,
                attempt,
                maxAttempts,
                sanitizedBody);

            return;
        }

        logger.LogWarning(
            "Mikro API {Method} {Path} returned HTTP {StatusCode} in {ElapsedMilliseconds} ms on attempt {Attempt}/{MaxAttempts}. Body: {Body}",
            method,
            path,
            (int)response.StatusCode,
            elapsed.TotalMilliseconds,
            attempt,
            maxAttempts,
            sanitizedBody);
    }

    private static string SanitizeForLog(string rawResponse, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return string.Empty;
        }

        var value = rawResponse
            .Replace("\"Sifre\"", "\"Sifre(REDACTED)\"", StringComparison.OrdinalIgnoreCase)
            .Replace("\"ApiKey\"", "\"ApiKey(REDACTED)\"", StringComparison.OrdinalIgnoreCase)
            .Replace("\"Token\"", "\"Token(REDACTED)\"", StringComparison.OrdinalIgnoreCase)
            .Replace("\"Password\"", "\"Password(REDACTED)\"", StringComparison.OrdinalIgnoreCase);

        var safeMaxLength = Math.Clamp(maxLength, 256, 32768);

        return value.Length <= safeMaxLength
            ? value
            : string.Concat(value.AsSpan(0, safeMaxLength), "...[truncated]");
    }

    private static bool IsHttpSuccess(HttpStatusCode statusCode)
    {
        var numericStatusCode = (int)statusCode;
        return numericStatusCode is >= 200 and <= 299;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;

    private static int ResolveMaxAttempts(HttpMethod method, MikroApiOptions currentOptions)
    {
        var retriesEnabledForMethod =
            method == HttpMethod.Get ||
            method == HttpMethod.Head ||
            currentOptions.RetryUnsafeHttpMethods;

        if (!retriesEnabledForMethod)
        {
            return 1;
        }

        return Math.Clamp(currentOptions.RetryCount, 0, 5) + 1;
    }

    private static Task DelayBeforeRetryAsync(
        int attempt,
        MikroApiOptions currentOptions,
        CancellationToken cancellationToken)
    {
        var baseDelayMilliseconds = Math.Clamp(currentOptions.RetryDelayMilliseconds, 50, 5000);
        var delay = TimeSpan.FromMilliseconds(baseDelayMilliseconds * attempt);

        return Task.Delay(delay, cancellationToken);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new MikroApiException("Mikro API request path is empty.");
        }

        return Uri.TryCreate(path, UriKind.Absolute, out _)
            ? path
            : path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
    }

    private sealed record MikroApiResponseInfo(
        bool? IsError,
        int? StatusCode,
        string? ErrorMessage);
}
