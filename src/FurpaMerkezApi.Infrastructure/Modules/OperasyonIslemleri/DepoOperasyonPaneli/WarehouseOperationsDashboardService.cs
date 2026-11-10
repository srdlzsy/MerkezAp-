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
    private const int OperationalLookbackDays = 30;
    private static readonly DateOnly MinimumOperationalStartDate = new(2026, 10, 1);

    public async Task<WarehouseOperationsDashboardDto> GetAsync(
        DateOnly date,
        CancellationToken cancellationToken,
        int? warehouseNo = null)
    {
        var lookbackStartDate = ResolveLookbackStartDate(date);
        var lookbackStartUtc = ToUtc(lookbackStartDate);
        var startUtc = ToUtc(date);
        var endUtc = ToUtc(date.AddDays(1));

        var warehousesQuery = mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_iptal != true && warehouse.dep_no.HasValue);

        if (warehouseNo.HasValue)
        {
            warehousesQuery = warehousesQuery.Where(warehouse => warehouse.dep_no == warehouseNo.Value);
        }

        var warehouseRows = await warehousesQuery
            .OrderBy(warehouse => warehouse.dep_no)
            .Select(warehouse => new
            {
                WarehouseNo = warehouse.dep_no!.Value,
                WarehouseName = warehouse.dep_adi ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        var flowsQuery = authDbContext.DocumentFlows
            .AsNoTracking()
            .Where(flow =>
                flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                ((flow.CreatedAtUtc >= lookbackStartUtc && flow.CreatedAtUtc < endUtc) ||
                 (flow.UpdatedAtUtc >= startUtc && flow.UpdatedAtUtc < endUtc &&
                  flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted)));

        if (warehouseNo.HasValue)
        {
            var scopedWarehouse = warehouseNo.Value;
            flowsQuery = flowsQuery.Where(flow =>
                flow.SourceWarehouseNo == scopedWarehouse ||
                flow.TargetWarehouseNo == scopedWarehouse);
        }

        var flowRows = await flowsQuery
            .Select(flow => new FlowRow(
                flow.DocumentType,
                flow.SourceWarehouseNo,
                flow.TargetWarehouseNo,
                flow.Status,
                flow.CurrentStep,
                flow.ExternalUuid,
                flow.CreatedAtUtc,
                flow.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var items = warehouseRows
            .Select(warehouse => BuildWarehouseItem(
                warehouse.WarehouseNo,
                warehouse.WarehouseName,
                flowRows,
                lookbackStartUtc,
                startUtc,
                endUtc))
            .ToArray();

        var busiestWarehouse = items
            .OrderByDescending(item => item.TodayShipmentCount + item.TodayReturnCount + item.TodayReceivingCount)
            .ThenBy(item => item.WarehouseNo)
            .FirstOrDefault(item => item.TodayShipmentCount + item.TodayReturnCount + item.TodayReceivingCount > 0);

        var slowestWarehouse = items
            .Where(item => item.AverageReceivingMinutes.HasValue)
            .OrderByDescending(item => item.AverageReceivingMinutes)
            .ThenBy(item => item.WarehouseNo)
            .FirstOrDefault();

        var summaryPendingReceivingCount = items.Sum(item => item.PendingReceivingCount);
        var summaryUnacceptedOutgoingCount = items.Sum(item => item.UnacceptedOutgoingCount);
        var summaryPendingEDespatchCount = items.Sum(item => item.PendingEDespatchCount);
        var summaryFailedEDespatchCount = items.Sum(item => item.FailedEDespatchCount);

        // Hatali akislar hem kaynak hem hedef depo satirinda gorunebilir; genel toplamda
        // ayni belgenin iki kez sayilmamasi icin ozet belge bazinda hesaplanir.
        var activeWarehouseNos = warehouseRows.Select(warehouse => warehouse.WarehouseNo).ToHashSet();
        var summaryOperationalFlows = flowRows
            .Where(flow =>
                flow.CreatedAtUtc >= lookbackStartUtc &&
                flow.CreatedAtUtc < endUtc &&
                (activeWarehouseNos.Contains(flow.SourceWarehouseNo) ||
                 (flow.TargetWarehouseNo.HasValue && activeWarehouseNos.Contains(flow.TargetWarehouseNo.Value))))
            .ToArray();
        var summaryOpenOrderCount = summaryOperationalFlows.Count(flow =>
            IsOrder(flow.DocumentType) &&
            flow.CurrentStep == DocumentFlowStep.OrderCreated &&
            flow.Status == DocumentFlowStatus.Succeeded);
        var summaryOtherFailedOperationCount = summaryOperationalFlows.Count(flow =>
            flow.CurrentStep != DocumentFlowStep.EDespatchSubmission &&
            flow.Status == DocumentFlowStatus.Failed);
        var summaryIncompleteOperationCount = summaryOperationalFlows.Count(flow =>
            (flow.TargetWarehouseNo.HasValue &&
             activeWarehouseNos.Contains(flow.TargetWarehouseNo.Value) &&
             IsPendingReceiving(flow)) ||
            (activeWarehouseNos.Contains(flow.SourceWarehouseNo) && IsPendingOrFailedEDespatch(flow)) ||
            flow.Status == DocumentFlowStatus.Failed);

        return new WarehouseOperationsDashboardDto(
            date,
            clock.UtcNow,
            trackingOptions.CurrentValue.Enabled,
            new WarehouseOperationsDashboardSummaryDto(
                items.Length,
                items.Sum(item => item.TodayShipmentCount),
                items.Sum(item => item.TodayReturnCount),
                items.Sum(item => item.TodayReceivingCount),
                summaryPendingReceivingCount,
                summaryIncompleteOperationCount,
                summaryFailedEDespatchCount,
                summaryUnacceptedOutgoingCount,
                summaryPendingEDespatchCount,
                summaryOpenOrderCount,
                summaryOtherFailedOperationCount),
            busiestWarehouse is null
                ? null
                : new WarehouseOperationsDashboardHighlightDto(
                    busiestWarehouse.WarehouseNo,
                    busiestWarehouse.WarehouseName,
                    busiestWarehouse.TodayShipmentCount +
                    busiestWarehouse.TodayReturnCount +
                    busiestWarehouse.TodayReceivingCount),
            slowestWarehouse is null
                ? null
                : new WarehouseOperationsDashboardHighlightDto(
                    slowestWarehouse.WarehouseNo,
                    slowestWarehouse.WarehouseName,
                    slowestWarehouse.AverageReceivingMinutes!.Value),
            items,
            lookbackStartDate);
    }

    private static WarehouseOperationsDashboardItemDto BuildWarehouseItem(
        int warehouseNo,
        string warehouseName,
        IReadOnlyCollection<FlowRow> flows,
        DateTime lookbackStartUtc,
        DateTime startUtc,
        DateTime endUtc)
    {
        var todayShipmentCount = flows.Count(flow =>
            flow.SourceWarehouseNo == warehouseNo &&
            flow.CreatedAtUtc >= startUtc &&
            flow.CreatedAtUtc < endUtc &&
            IsShipment(flow.DocumentType) &&
            !IsFailedCreation(flow));

        var todayReturnCount = flows.Count(flow =>
            flow.SourceWarehouseNo == warehouseNo &&
            flow.CreatedAtUtc >= startUtc &&
            flow.CreatedAtUtc < endUtc &&
            IsReturn(flow.DocumentType) &&
            !IsFailedCreation(flow));

        var completedReceivings = flows
            .Where(flow =>
                flow.TargetWarehouseNo == warehouseNo &&
                flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted &&
                flow.UpdatedAtUtc >= startUtc &&
                flow.UpdatedAtUtc < endUtc)
            .ToArray();

        var operationalFlows = flows
            .Where(flow => flow.CreatedAtUtc >= lookbackStartUtc && flow.CreatedAtUtc < endUtc)
            .ToArray();

        var pendingReceivingCount = operationalFlows.Count(flow =>
            flow.TargetWarehouseNo == warehouseNo &&
            IsPendingReceiving(flow));

        var unacceptedOutgoingCount = operationalFlows.Count(flow =>
            flow.SourceWarehouseNo == warehouseNo &&
            IsPendingReceiving(flow));

        var failedEDespatchCount = operationalFlows.Count(flow =>
            flow.SourceWarehouseNo == warehouseNo &&
            flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
            flow.Status == DocumentFlowStatus.Failed);

        var pendingEDespatchCount = operationalFlows.Count(flow =>
            flow.SourceWarehouseNo == warehouseNo &&
            IsPendingOrFailedEDespatch(flow));

        var openOrderCount = operationalFlows.Count(flow =>
            (flow.SourceWarehouseNo == warehouseNo || flow.TargetWarehouseNo == warehouseNo) &&
            IsOrder(flow.DocumentType) &&
            flow.CurrentStep == DocumentFlowStep.OrderCreated &&
            flow.Status == DocumentFlowStatus.Succeeded);

        var otherFailedOperationCount = operationalFlows.Count(flow =>
            (flow.SourceWarehouseNo == warehouseNo || flow.TargetWarehouseNo == warehouseNo) &&
            flow.CurrentStep != DocumentFlowStep.EDespatchSubmission &&
            flow.Status == DocumentFlowStatus.Failed);

        var incompleteOperationCount = operationalFlows.Count(flow =>
            (flow.TargetWarehouseNo == warehouseNo && IsPendingReceiving(flow)) ||
            (flow.SourceWarehouseNo == warehouseNo && IsPendingOrFailedEDespatch(flow)) ||
            ((flow.SourceWarehouseNo == warehouseNo || flow.TargetWarehouseNo == warehouseNo) &&
             flow.Status == DocumentFlowStatus.Failed));

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
            todayReturnCount,
            completedReceivings.Length,
            pendingReceivingCount,
            incompleteOperationCount,
            failedEDespatchCount,
            averageReceivingMinutes,
            ResolveHealthStatus(
                pendingReceivingCount,
                pendingEDespatchCount,
                failedEDespatchCount,
                otherFailedOperationCount),
            unacceptedOutgoingCount,
            pendingEDespatchCount,
            openOrderCount,
            otherFailedOperationCount);
    }

    private static DateOnly ResolveLookbackStartDate(DateOnly date)
    {
        var lookbackStart = date.AddDays(-OperationalLookbackDays);
        if (date < MinimumOperationalStartDate)
        {
            return lookbackStart;
        }

        return lookbackStart > MinimumOperationalStartDate
            ? lookbackStart
            : MinimumOperationalStartDate;
    }

    private static bool IsShipment(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.CompanyShipment or DocumentFlowType.InterWarehouseShipment;

    private static bool IsReturn(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.CompanyReturn or DocumentFlowType.WarehouseReturn;

    private static bool IsDespatchDocument(DocumentFlowType documentType) =>
        IsShipment(documentType) || IsReturn(documentType);

    private static bool IsWarehouseMovement(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.InterWarehouseShipment or DocumentFlowType.WarehouseReturn;

    private static bool IsOrder(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.IssuedCompanyOrder
            or DocumentFlowType.ReceivedCompanyOrder
            or DocumentFlowType.IssuedWarehouseOrder
            or DocumentFlowType.ReceivedWarehouseOrder;

    private static bool IsFailedCreation(FlowRow flow) =>
        flow.CurrentStep == DocumentFlowStep.DocumentCreated &&
        flow.Status == DocumentFlowStatus.Failed;

    private static bool IsPendingReceiving(FlowRow flow) =>
        flow.TargetWarehouseNo.HasValue &&
        IsWarehouseMovement(flow.DocumentType) &&
        flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
        flow.Status == DocumentFlowStatus.Succeeded;

    private static bool IsPendingOrFailedEDespatch(FlowRow flow) =>
        flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
        flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted &&
        ((flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
          flow.Status == DocumentFlowStatus.Failed) ||
         (IsDespatchDocument(flow.DocumentType) &&
          flow.Status == DocumentFlowStatus.Succeeded &&
          flow.ExternalUuid == null &&
          (flow.CurrentStep == DocumentFlowStep.DocumentCreated ||
           flow.CurrentStep == DocumentFlowStep.AxataDocumentImported ||
           flow.CurrentStep == DocumentFlowStep.DocumentUpdated)));

    private static string ResolveHealthStatus(
        int pendingReceivingCount,
        int pendingEDespatchCount,
        int failedEDespatchCount,
        int otherFailedOperationCount)
    {
        if (failedEDespatchCount > 0 || otherFailedOperationCount > 0)
        {
            return "Critical";
        }

        return pendingReceivingCount > 0 || pendingEDespatchCount > 0
            ? "Warning"
            : "Healthy";
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
        string? ExternalUuid,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
