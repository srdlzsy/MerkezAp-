namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.DepoOperasyonPaneli;

public interface IWarehouseOperationsDashboardService
{
    Task<WarehouseOperationsDashboardDto> GetAsync(
        DateOnly date,
        CancellationToken cancellationToken,
        int? warehouseNo = null);
}

public sealed record WarehouseOperationsDashboardDto(
    DateOnly Date,
    DateTime GeneratedAtUtc,
    bool TrackingEnabled,
    WarehouseOperationsDashboardSummaryDto Summary,
    WarehouseOperationsDashboardHighlightDto? BusiestWarehouse,
    WarehouseOperationsDashboardHighlightDto? SlowestWarehouse,
    IReadOnlyCollection<WarehouseOperationsDashboardItemDto> Warehouses,
    DateOnly? LookbackStartDate = null);

public sealed record WarehouseOperationsDashboardSummaryDto(
    int WarehouseCount,
    int TodayShipmentCount,
    int TodayReturnCount,
    int TodayReceivingCount,
    int PendingReceivingCount,
    int IncompleteOperationCount,
    int FailedEDespatchCount,
    int UnacceptedOutgoingCount = 0,
    int PendingEDespatchCount = 0,
    int OpenOrderCount = 0,
    int OtherFailedOperationCount = 0);

public sealed record WarehouseOperationsDashboardHighlightDto(
    int WarehouseNo,
    string WarehouseName,
    double Value);

public sealed record WarehouseOperationsDashboardItemDto(
    int WarehouseNo,
    string WarehouseName,
    int TodayShipmentCount,
    int TodayReturnCount,
    int TodayReceivingCount,
    int PendingReceivingCount,
    int IncompleteOperationCount,
    int FailedEDespatchCount,
    double? AverageReceivingMinutes,
    string HealthStatus,
    int UnacceptedOutgoingCount = 0,
    int PendingEDespatchCount = 0,
    int OpenOrderCount = 0,
    int OtherFailedOperationCount = 0);
