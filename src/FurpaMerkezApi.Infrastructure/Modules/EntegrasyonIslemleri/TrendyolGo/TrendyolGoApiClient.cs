using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoApiClient(
    HttpClient httpClient,
    IOptionsMonitor<TrendyolGoOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task<TrendyolGoApiResponse> GetAsync(
        string path,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, path, payload: null, cancellationToken);

    public Task<TrendyolGoApiResponse> PutAsync(
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        EnsureActorHeaders();
        return SendAsync(HttpMethod.Put, path, payload, cancellationToken);
    }

    public Task<TrendyolGoApiResponse> PostAsync(
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        EnsureActorHeaders();
        return SendAsync(HttpMethod.Post, path, payload, cancellationToken);
    }

    public Task<TrendyolGoApiResponse> DeleteAsync(
        string path,
        CancellationToken cancellationToken)
    {
        EnsureActorHeaders();
        return SendAsync(HttpMethod.Delete, path, payload: null, cancellationToken);
    }

    private async Task<TrendyolGoApiResponse> SendAsync(
        HttpMethod method,
        string path,
        object? payload,
        CancellationToken cancellationToken)
    {
        var currentOptions = ValidateConfiguration();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", ResolveAuthorizationToken(currentOptions));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(currentOptions.AgentName))
        {
            request.Headers.TryAddWithoutValidation("x-agentname", currentOptions.AgentName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(currentOptions.ExecutorUser))
        {
            request.Headers.TryAddWithoutValidation("x-executor-user", currentOptions.ExecutorUser.Trim());
        }

        if (payload is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonOptions),
                Encoding.UTF8,
                "application/json");
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var rawBody = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        return new TrendyolGoApiResponse((int)response.StatusCode, response.IsSuccessStatusCode, rawBody);
    }

    private TrendyolGoOptions ValidateConfiguration()
    {
        var currentOptions = options.CurrentValue;

        if (!currentOptions.Enabled)
        {
            throw new InvalidOperationException("TrendyolGo integration is disabled.");
        }

        if (currentOptions.SupplierId <= 0)
        {
            throw new InvalidOperationException("TrendyolGo:SupplierId must be configured.");
        }

        _ = ResolveAuthorizationToken(currentOptions);
        return currentOptions;
    }

    private void EnsureActorHeaders()
    {
        var currentOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(currentOptions.AgentName) ||
            string.IsNullOrWhiteSpace(currentOptions.ExecutorUser))
        {
            throw new InvalidOperationException(
                "TrendyolGo:AgentName and TrendyolGo:ExecutorUser must be configured for write operations.");
        }
    }

    private static string ResolveAuthorizationToken(TrendyolGoOptions currentOptions)
    {
        if (!string.IsNullOrWhiteSpace(currentOptions.AuthorizationToken))
        {
            return currentOptions.AuthorizationToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(currentOptions.ApiKey) &&
            !string.IsNullOrWhiteSpace(currentOptions.ApiSecret))
        {
            return Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{currentOptions.ApiKey.Trim()}:{currentOptions.ApiSecret.Trim()}"));
        }

        throw new InvalidOperationException(
            "TrendyolGo credentials are not configured. Set AuthorizationToken or ApiKey and ApiSecret.");
    }
}

internal sealed record TrendyolGoApiResponse(int StatusCode, bool IsSuccessStatusCode, string RawBody);
