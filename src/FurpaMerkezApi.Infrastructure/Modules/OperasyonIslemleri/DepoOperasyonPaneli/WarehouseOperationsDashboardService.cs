using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.DepoOperasyonPaneli;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.DepoOperasyonPaneli;

public sealed class WarehouseOperationsDashboardService(
    AuthDbContext authDbContext,
    MikroDbContext mikroDbContext,
    IClock clock,
    IOptionsMonitor<DocumentFlowTrackingOptions> trackingOptions)
    : IWarehouseOperationsDashboardService
{
    public async Task<WarehouseOperationsDashboardDto> GetAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var startUtc = ToUtc(date);
        var endUtc = ToUtc(date.AddDays(1));

        var warehouseRows = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_iptal != true && warehouse.dep_no.HasValue)
            .OrderBy(warehouse => warehouse.dep_no)
            .Select(warehouse => new
            {
                WarehouseNo = warehouse.dep_no!.Value,
                WarehouseName = warehouse.dep_adi ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var flowRows = await authDbContext.DocumentFlows
            .AsNoTracking()
            .Where(flow =>
                (flow.CreatedAtUtc >= startUtc && flow.CreatedAtUtc < endUtc &&
                 (flow.DocumentType == DocumentFlowType.CompanyShipment ||
                  flow.DocumentType == DocumentFlowType.InterWarehouseShipment)) ||
                (flow.UpdatedAtUtc >= startUtc && flow.UpdatedAtUtc < endUtc &&
                 flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted) ||
                ((flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                  flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                 flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted) ||
                (flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                 flow.Status == DocumentFlowStatus.Failed))
            .Select(flow => new FlowRow(
                flow.DocumentType,
                flow.SourceWarehouseNo,
                flow.TargetWarehouseNo,
                flow.Status,
                flow.CurrentStep,
                flow.CreatedAtUtc,
                flow.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var items = warehouseRows
            .Select(warehouse => BuildWarehouseItem(
                warehouse.WarehouseNo,
                warehouse.WarehouseName,
                flowRows,
                startUtc,
                endUtc))
            .ToArray();

        var busiestWarehouse = items
            .OrderByDescending(item => item.TodayShipmentCount + item.TodayReceivingCount)
            .ThenBy(item => item.WarehouseNo)
            .FirstOrDefault(item => item.TodayShipmentCount + item.TodayReceivingCount > 0);

        var slowestWarehouse = items
            .Where(item => item.AverageReceivingMinutes.HasValue)
            .OrderByDescending(item => item.AverageReceivingMinutes)
            .ThenBy(item => item.WarehouseNo)
            .FirstOrDefault();

        var incompleteOperationCount = flowRows.Count(flow =>
            (flow.TargetWarehouseNo.HasValue &&
             IsWarehouseMovement(flow.DocumentType) &&
             flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted) ||
            (flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
             flow.Status == DocumentFlowStatus.Failed));

        return new WarehouseOperationsDashboardDto(
            date,
            clock.UtcNow,
            trackingOptions.CurrentValue.Enabled,
            new WarehouseOperationsDashboardSummaryDto(
                items.Length,
                items.Sum(item => item.TodayShipmentCount),
                items.Sum(item => item.TodayReceivingCount),
                items.Sum(item => item.PendingReceivingCount),
                incompleteOperationCount,
                items.Sum(item => item.FailedEDespatchCount)),
            busiestWarehouse is null
                ? null
                : new WarehouseOperationsDashboardHighlightDto(
                    busiestWarehouse.WarehouseNo,
                    busiestWarehouse.WarehouseName,
                    busiestWarehouse.TodayShipmentCount + busiestWarehouse.TodayReceivingCount),
            slowestWarehouse is null
                ? null
                : new WarehouseOperationsDashboardHighlightDto(
                    slowestWarehouse.WarehouseNo,
                    slowestWarehouse.WarehouseName,
                    slowestWarehouse.AverageReceivingMinutes!.Value),
            items);
    }

    private static WarehouseOperationsDashboardItemDto BuildWarehouseItem(
        int warehouseNo,
        string warehouseName,
        IReadOnlyCollection<FlowRow> flows,
        DateTime startUtc,
        DateTime endUtc)
    {
        var todayShipmentCount = flows.Count(flow =>
            flow.SourceWarehouseNo == warehouseNo &&
            flow.CreatedAtUtc >= startUtc &&
            flow.CreatedAtUtc < endUtc &&
            IsShipment(flow.DocumentType));

        var completedReceivings = flows
            .Where(flow =>
                flow.TargetWarehouseNo == warehouseNo &&
                flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted &&
                flow.UpdatedAtUtc >= startUtc &&
                flow.UpdatedAtUtc < endUtc)
            .ToArray();

        var pendingReceivingCount = flows.Count(flow =>
            flow.TargetWarehouseNo == warehouseNo &&
            IsWarehouseMovement(flow.DocumentType) &&
            flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted);

        var failedEDespatchCount = flows.Count(flow =>
            flow.SourceWarehouseNo == warehouseNo &&
            flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
            flow.Status == DocumentFlowStatus.Failed);

        var incompleteOperationCount = pendingReceivingCount + failedEDespatchCount;
        var averageReceivingMinutes = completedReceivings.Length == 0
            ? (double?)null
            : Math.Round(
                completedReceivings.Average(flow =>
                    Math.Max(0, (flow.UpdatedAtUtc - flow.CreatedAtUtc).TotalMinutes)),
                2);

        return new WarehouseOperationsDashboardItemDto(
            warehouseNo,
            warehouseName,
            todayShipmentCount,
            completedReceivings.Length,
            pendingReceivingCount,
            incompleteOperationCount,
            failedEDespatchCount,
            averageReceivingMinutes,
            ResolveHealthStatus(pendingReceivingCount, failedEDespatchCount));
    }

    private static bool IsShipment(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.CompanyShipment or DocumentFlowType.InterWarehouseShipment;

    private static bool IsWarehouseMovement(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.InterWarehouseShipment or DocumentFlowType.WarehouseReturn;

    private static string ResolveHealthStatus(int pendingReceivingCount, int failedEDespatchCount)
    {
        if (failedEDespatchCount > 0)
        {
            return "Critical";
        }

        return pendingReceivingCount > 0 ? "Warning" : "Healthy";
    }

    private static DateTime ToUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        return localStart.ToUniversalTime();
    }

    private sealed record FlowRow(
        DocumentFlowType DocumentType,
        int SourceWarehouseNo,
        int? TargetWarehouseNo,
        DocumentFlowStatus Status,
        DocumentFlowStep CurrentStep,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
