using System.Globalization;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationOutboxWriter(IOptionsMonitor<AxataSynchronizationOptions> options)
{
    public async Task<AxataSynchronizationJobArtifactDto> WritePayloadAsync(
        AxataSynchronizationTaskExecutionContext context,
        object payload,
        CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        var basePath = string.IsNullOrWhiteSpace(currentOptions.OutboxBasePath)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "AxataSynchronizationOutbox")
            : currentOptions.OutboxBasePath.Trim();

        var directory = Path.Combine(
            basePath,
            context.Definition.Code,
            context.RequestedAtUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            context.JobId == Guid.Empty ? Guid.NewGuid().ToString("N") : context.JobId.ToString("N"));

        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, "payload.json");
        var json = JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        return new AxataSynchronizationJobArtifactDto(
            Path.GetFileName(filePath),
            "PayloadJson",
            filePath);
    }
}
