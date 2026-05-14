namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed record AxataSynchronizationPreviewRequest(
    string TaskCode,
    int? WarehouseNo,
    int? Take);

public sealed record AxataSynchronizationPreviewDto(
    string TaskCode,
    string TaskName,
    int? WarehouseNo,
    int TotalRecordCount,
    int ReturnedRecordCount,
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<AxataSynchronizationPreviewItemDto> Items,
    IReadOnlyCollection<string> Notes);

public sealed record AxataSynchronizationPreviewItemDto(
    string Key,
    string Summary,
    string PayloadJson);
