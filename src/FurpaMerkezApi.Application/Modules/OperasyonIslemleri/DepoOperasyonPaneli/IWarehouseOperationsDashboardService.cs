namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.DepoOperasyonPaneli;

public interface IWarehouseOperationsDashboardService
{
    Task<WarehouseOperationsDashboardDto> GetAsync(
        DateOnly date,
        CancellationToken cancellationToken);
}

public sealed record WarehouseOperationsDashboardDto(
    DateOnly Date,
    DateTime GeneratedAtUtc,
    bool TrackingEnabled,
    WarehouseOperationsDashboardSummaryDto Summary,
    WarehouseOperationsDashboardHighlightDto? BusiestWarehouse,
    WarehouseOperationsDashboardHighlightDto? SlowestWarehouse,
    IReadOnlyCollection<WarehouseOperationsDashboardItemDto> Warehouses);

public sealed record WarehouseOperationsDashboardSummaryDto(
    int WarehouseCount,
    int TodayShipmentCount,
    int TodayReceivingCount,
    int PendingReceivingCount,
    int IncompleteOperationCount,
    int FailedEDespatchCount);

public sealed record WarehouseOperationsDashboardHighlightDto(
    int WarehouseNo,
    string WarehouseName,
    double Value);

public sealed record WarehouseOperationsDashboardItemDto(
    int WarehouseNo,
    string WarehouseName,
    int TodayShipmentCount,
    int TodayReceivingCount,
    int PendingReceivingCount,
    int IncompleteOperationCount,
    int FailedEDespatchCount,
    double? AverageReceivingMinutes,
    string HealthStatus);
