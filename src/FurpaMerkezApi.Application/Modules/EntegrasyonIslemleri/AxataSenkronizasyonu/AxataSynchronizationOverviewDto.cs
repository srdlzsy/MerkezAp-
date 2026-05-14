namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed record AxataSynchronizationOverviewDto(
    bool Enabled,
    bool WorkerEnabled,
    bool SchedulerEnabled,
    string SourceDatabaseProfile,
    string MainEndpointUrl,
    string ExtendedEndpointUrl,
    IReadOnlyCollection<AxataSynchronizationTaskDto> Tasks,
    IReadOnlyCollection<AxataSynchronizationJobDto> RecentJobs);

public sealed record AxataSynchronizationTaskDto(
    string Code,
    string Name,
    string Description,
    string Flow,
    bool RequiresWarehouseNo,
    bool Enabled,
    bool ScheduleEnabled,
    int IntervalMinutes,
    int? DefaultWarehouseNo,
    string SourceSystem,
    string TargetSystem,
    bool SupportsManualDocuments,
    bool SupportsLiveDispatch,
    string? LiveOperationName);
