namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed record AxataSynchronizationExecuteRequest(
    string TaskCode,
    string ExecutionMode,
    int? WarehouseNo);

public sealed record AxataSynchronizationJobDto(
    Guid JobId,
    string TaskCode,
    string TaskName,
    string Status,
    string ExecutionMode,
    string TriggerSource,
    int? WarehouseNo,
    DateTime CreatedAtUtc);

public sealed record AxataSynchronizationJobDetailDto(
    Guid JobId,
    string TaskCode,
    string TaskName,
    string Status,
    string ExecutionMode,
    string TriggerSource,
    int? WarehouseNo,
    Guid RequestedByUserId,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    int AffectedRecordCount,
    string? Message,
    string? ErrorMessage,
    IReadOnlyCollection<AxataSynchronizationJobArtifactDto> Artifacts);

public sealed record AxataSynchronizationJobArtifactDto(
    string Name,
    string Kind,
    string Path);
