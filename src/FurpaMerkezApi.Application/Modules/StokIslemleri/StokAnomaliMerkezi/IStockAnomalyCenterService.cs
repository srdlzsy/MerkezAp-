using FurpaMerkezApi.Domain.Entities;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.StokAnomaliMerkezi;

public interface IStockAnomalyCenterService
{
    Task<StockAnomalyListResponse> ListAsync(
        StockAnomalyListRequest request,
        CancellationToken cancellationToken);

    Task<StockAnomalyDetailDto> GetAsync(
        Guid id,
        int? allowedWarehouseNo,
        CancellationToken cancellationToken);

    Task<StockAnomalyScanResponse> ScanAsync(
        StockAnomalyScanRequest request,
        CancellationToken cancellationToken);

    Task<StockAnomalyDetailDto> ChangeStatusAsync(
        ChangeStockAnomalyStatusRequest request,
        CancellationToken cancellationToken);
}

public sealed record StockAnomalyListRequest(
    int? WarehouseNo,
    StockAnomalyType? Type,
    StockAnomalyStatus? Status,
    StockAnomalySeverity? Severity,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Search,
    int Take = 100);

public sealed record StockAnomalyListResponse(
    int TotalCount,
    StockAnomalySummaryDto Summary,
    IReadOnlyCollection<StockAnomalyListItemDto> Items);

public sealed record StockAnomalySummaryDto(
    int OpenCount,
    int AcknowledgedCount,
    int ResolvedCount,
    int IgnoredCount,
    int CriticalCount,
    int HighCount);

public sealed record StockAnomalyListItemDto(
    Guid Id,
    string Type,
    string Severity,
    string Status,
    int WarehouseNo,
    int? RelatedWarehouseNo,
    string? WarehouseName,
    string? RelatedWarehouseName,
    string? ProductCode,
    string? ProductName,
    string? DocumentSerie,
    int? DocumentOrderNo,
    string? DocumentNo,
    double? Quantity,
    double? ExpectedQuantity,
    double? ActualQuantity,
    double? AverageQuantity,
    DateTime? OccurredAtUtc,
    string Message,
    DateTime FirstDetectedAtUtc,
    DateTime LastDetectedAtUtc);

public sealed record StockAnomalyDetailDto(
    Guid Id,
    string SourceKey,
    string Type,
    string Severity,
    string Status,
    int WarehouseNo,
    int? RelatedWarehouseNo,
    string? WarehouseName,
    string? RelatedWarehouseName,
    string? ProductCode,
    string? ProductName,
    string? DocumentSerie,
    int? DocumentOrderNo,
    string? DocumentNo,
    Guid? MovementGuid,
    double? Quantity,
    double? ExpectedQuantity,
    double? ActualQuantity,
    double? AverageQuantity,
    DateTime? OccurredAtUtc,
    string Message,
    string? Evidence,
    Guid? LastChangedByUserId,
    DateTime FirstDetectedAtUtc,
    DateTime LastDetectedAtUtc,
    DateTime? ResolvedAtUtc,
    IReadOnlyCollection<StockAnomalyEventDto> Events);

public sealed record StockAnomalyEventDto(
    Guid Id,
    string EventType,
    string Status,
    string Message,
    Guid? ChangedByUserId,
    DateTime OccurredAtUtc);

public sealed record StockAnomalyScanRequest(
    int? WarehouseNo,
    DateTime? StartDate,
    DateTime? EndDate,
    int DormantDays = 90,
    int PendingTransferHours = 24,
    int HighQuantityLookbackDays = 30,
    double HighQuantityMultiplier = 3d,
    double HighQuantityMinimum = 100d,
    int TakePerRule = 250);

public sealed record StockAnomalyScanResponse(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int DetectedCount,
    IReadOnlyCollection<StockAnomalyScanRuleResultDto> Rules);

public sealed record StockAnomalyScanRuleResultDto(
    string Type,
    int DetectedCount);

public sealed record ChangeStockAnomalyStatusRequest(
    Guid Id,
    StockAnomalyStatus Status,
    string? Note,
    Guid? ChangedByUserId,
    int? AllowedWarehouseNo);
