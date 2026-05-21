using System.Diagnostics;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationConnectionProbeService(
    MikroDbContext mikroDbContext,
    FurpaDbContext furpaDbContext,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IOptionsMonitor<AxataSynchronizationOptions> options)
{
    public async Task<AxataSynchronizationConnectionTestDto> ProbeAsync(CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        var endpointTimeout = TimeSpan.FromSeconds(Math.Clamp(currentOptions.EndpointProbeTimeoutSeconds, 1, 60));
        var probes = await Task.WhenAll(
            ProbeDatabaseAsync("Mikro SQL", mikroDbContext.Database, cancellationToken),
            ProbeDatabaseAsync("Furpa SQL", furpaDbContext.Database, cancellationToken),
            ProbeEndpointAsync("AXATA Main Endpoint", currentOptions.MainEndpointUrl, endpointTimeout, cancellationToken),
            ProbeEndpointAsync("AXATA EXT Endpoint", currentOptions.ExtendedEndpointUrl, endpointTimeout, cancellationToken));

        return new AxataSynchronizationConnectionTestDto(
            DateTime.UtcNow,
            configuration["MikroDatabase:Profile"] ?? "Split",
            probes);
    }

    private static async Task<AxataSynchronizationProbeDto> ProbeDatabaseAsync(
        string name,
        DatabaseFacade database,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var canConnect = await database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();

            return new AxataSynchronizationProbeDto(
                name,
                canConnect ? "Healthy" : "Unhealthy",
                stopwatch.ElapsedMilliseconds,
                canConnect ? "Database connection succeeded." : "Database connection could not be established.");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            return new AxataSynchronizationProbeDto(
                name,
                "Unhealthy",
                stopwatch.ElapsedMilliseconds,
                exception.Message);
        }
    }

    private async Task<AxataSynchronizationProbeDto> ProbeEndpointAsync(
        string name,
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new AxataSynchronizationProbeDto(
                name,
                "NotConfigured",
                null,
                "Endpoint URL is empty.");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            using var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            stopwatch.Stop();

            return new AxataSynchronizationProbeDto(
                name,
                response.IsSuccessStatusCode ? "Healthy" : "Warning",
                stopwatch.ElapsedMilliseconds,
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            return new AxataSynchronizationProbeDto(
                name,
                "Unhealthy",
                stopwatch.ElapsedMilliseconds,
                $"Endpoint did not respond within {timeout.TotalSeconds:0} seconds. {exception.Message}");
        }
        catch (Exception exception)
        {
            stopwatch.Stop();

            return new AxataSynchronizationProbeDto(
                name,
                "Unhealthy",
                stopwatch.ElapsedMilliseconds,
                exception.Message);
        }
    }
}
