namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed record AxataSynchronizationFetchProfilesOverviewDto(
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<AxataSynchronizationFetchProfileDto> Profiles,
    IReadOnlyCollection<string> Notes);

public sealed record AxataSynchronizationFetchProfileDto(
    string Code,
    string Name,
    string SourceSystem,
    string TargetSystem,
    string SourceEndpointKind,
    string SourceEndpointUrl,
    string FetchOperation,
    string AckEndpointKind,
    string AckEndpointUrl,
    string AckOperation,
    string CompanyCode,
    string WarehouseCode,
    string? MovementType,
    string PendingStatus,
    string CurrentHandling,
    string? CurrentRoute,
    bool IsImplemented);
