namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
public sealed record AxataSynchronizationConnectionTestDto(
    DateTime TestedAtUtc,
    string SourceDatabaseProfile,
    IReadOnlyCollection<AxataSynchronizationProbeDto> Probes);

public sealed record AxataSynchronizationProbeDto(
    string Name,
    string Status,
    long? DurationMs,
    string? Message);
