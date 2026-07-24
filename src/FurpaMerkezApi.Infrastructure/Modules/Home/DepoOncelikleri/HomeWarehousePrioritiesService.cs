using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.Home.DepoOncelikleri;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.Home.DepoOncelikleri;

public sealed class HomeWarehousePrioritiesService(
    AuthDbContext authDbContext,
    MikroDbContext mikroDbContext,
    IClock clock) : IHomeWarehousePrioritiesService
{
    private const string Critical = "critical";
    private const string Warning = "warning";
    private const string Info = "info";
    private const string Healthy = "healthy";

    public async Task<HomeWarehousePrioritiesDto> GetAsync(
        HomeWarehousePrioritiesRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("User information was not found on the current user.");
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var startUtc = ToUtc(request.Date);
        var endUtc = ToUtc(request.Date.AddDays(1));
        var warehouseName = await ResolveWarehouseNameAsync(request, cancellationToken);
        var flowSummary = await LoadFlowSummaryAsync(request.WarehouseNo, startUtc, endUtc, cancellationToken);
        var stockAnomalySummary = await LoadStockAnomalySummaryAsync(request.WarehouseNo, cancellationToken);
        var myOpenFeedbackCount = await authDbContext.FeedbackItems
            .AsNoTracking()
            .Where(item => item.CreatedByUserId == request.UserId)
            .CountAsync(
                item => item.Status != FeedbackItemStatus.Resolved &&
                        item.Status != FeedbackItemStatus.Closed &&
                        item.Status != FeedbackItemStatus.Rejected,
                cancellationToken);

        var priorities = BuildPriorities(
            request.WarehouseNo,
            flowSummary.PendingReceivingCount,
            flowSummary.FailedEDespatchCount,
            stockAnomalySummary.CriticalCount,
            stockAnomalySummary.HighCount,
            myOpenFeedbackCount);
        var metrics = BuildMetrics(
            request.WarehouseNo,
            flowSummary.TodayShipmentCount,
            flowSummary.TodayReceivingCount,
            flowSummary.PendingReceivingCount,
            flowSummary.FailedEDespatchCount,
            stockAnomalySummary.OpenCount,
            myOpenFeedbackCount);
        var overallStatus = ResolveOverallStatus(
            flowSummary.FailedEDespatchCount,
            stockAnomalySummary.CriticalCount,
            flowSummary.PendingReceivingCount,
            stockAnomalySummary.HighCount,
            stockAnomalySummary.OpenCount,
            myOpenFeedbackCount);

        return new HomeWarehousePrioritiesDto(
            request.Date,
            clock.UtcNow,
            request.WarehouseNo,
            warehouseName,
            overallStatus,
            CreateHeadline(priorities.Count),
            metrics,
            priorities,
            BuildQuickActions(request.WarehouseNo));
    }

    private async Task<FlowSummary> LoadFlowSummaryAsync(
        int? warehouseNo,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var hasWarehouseNo = warehouseNo.HasValue;
        var scopedWarehouseNo = warehouseNo ?? 0;

        var query = authDbContext.DocumentFlows
            .AsNoTracking()
            .Where(flow =>
                ((flow.DocumentType == DocumentFlowType.CompanyShipment ||
                  flow.DocumentType == DocumentFlowType.InterWarehouseShipment) &&
                 flow.CreatedAtUtc >= startUtc &&
                 flow.CreatedAtUtc < endUtc) ||
                (flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted &&
                 flow.UpdatedAtUtc >= startUtc &&
                 flow.UpdatedAtUtc < endUtc) ||
                ((flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                  flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                 flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted) ||
                (flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                 flow.Status == DocumentFlowStatus.Failed));

        if (hasWarehouseNo)
        {
            query = query.Where(flow =>
                flow.SourceWarehouseNo == scopedWarehouseNo ||
                flow.TargetWarehouseNo == scopedWarehouseNo);
        }

        return await query
            .GroupBy(_ => 1)
            .Select(group => new FlowSummary(
                group.Count(flow =>
                    (!hasWarehouseNo || flow.SourceWarehouseNo == scopedWarehouseNo) &&
                    (flow.DocumentType == DocumentFlowType.CompanyShipment ||
                     flow.DocumentType == DocumentFlowType.InterWarehouseShipment) &&
                    flow.CreatedAtUtc >= startUtc &&
                    flow.CreatedAtUtc < endUtc),
                group.Count(flow =>
                    (!hasWarehouseNo || flow.TargetWarehouseNo == scopedWarehouseNo) &&
                    flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted &&
                    flow.UpdatedAtUtc >= startUtc &&
                    flow.UpdatedAtUtc < endUtc),
                group.Count(flow =>
                    (!hasWarehouseNo || flow.TargetWarehouseNo == scopedWarehouseNo) &&
                    (flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                     flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                    flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted),
                group.Count(flow =>
                    (!hasWarehouseNo || flow.SourceWarehouseNo == scopedWarehouseNo) &&
                    flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                    flow.Status == DocumentFlowStatus.Failed)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? new FlowSummary(0, 0, 0, 0);
    }

    private async Task<StockAnomalySummary> LoadStockAnomalySummaryAsync(
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var hasWarehouseNo = warehouseNo.HasValue;
        var scopedWarehouseNo = warehouseNo ?? 0;

        var query = authDbContext.StockAnomalies
            .AsNoTracking()
            .Where(anomaly =>
                anomaly.Status == StockAnomalyStatus.Open ||
                anomaly.Status == StockAnomalyStatus.Acknowledged);

        if (hasWarehouseNo)
        {
            query = query.Where(anomaly =>
                anomaly.WarehouseNo == scopedWarehouseNo ||
                anomaly.RelatedWarehouseNo == scopedWarehouseNo);
        }

        return await query
            .GroupBy(_ => 1)
            .Select(group => new StockAnomalySummary(
                group.Count(),
                group.Count(anomaly => anomaly.Severity == StockAnomalySeverity.Critical),
                group.Count(anomaly => anomaly.Severity == StockAnomalySeverity.High)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? new StockAnomalySummary(0, 0, 0);
    }

    private async Task<string> ResolveWarehouseNameAsync(
        HomeWarehousePrioritiesRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.WarehouseNo.HasValue)
        {
            return "Tum Depolar";
        }

        if (!string.IsNullOrWhiteSpace(request.WarehouseName))
        {
            return request.WarehouseName.Trim();
        }

        var warehouseNo = request.WarehouseNo.Value;
        var warehouseName = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no == warehouseNo)
            .Select(warehouse => warehouse.dep_adi)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(warehouseName)
            ? $"Depo {warehouseNo}"
            : warehouseName.Trim();
    }

    private static IReadOnlyCollection<HomePriorityMetricDto> BuildMetrics(
        int? warehouseNo,
        int todayShipmentCount,
        int todayReceivingCount,
        int pendingReceivingCount,
        int failedEDespatchCount,
        int openStockAnomalyCount,
        int myOpenFeedbackCount) =>
        [
            new(
                "failedEDespatch",
                "E-Irsaliye Hatasi",
                failedEDespatchCount,
                failedEDespatchCount > 0 ? Critical : Healthy,
                AppendWarehouseQuery("/operasyon-islemleri/belge-akis-takibi?status=Failed", warehouseNo)),
            new(
                "pendingReceiving",
                "Bekleyen Kabul",
                pendingReceivingCount,
                pendingReceivingCount > 0 ? Warning : Healthy,
                AppendWarehouseQuery("/operasyon-islemleri/belge-akis-takibi", warehouseNo)),
            new(
                "openStockAnomaly",
                "Acik Stok Anomalisi",
                openStockAnomalyCount,
                openStockAnomalyCount > 0 ? Warning : Healthy,
                AppendWarehouseQuery("/stok-islemleri/stok-anomali-merkezi?status=Open", warehouseNo)),
            new(
                "myOpenFeedback",
                "Acik Talebim",
                myOpenFeedbackCount,
                myOpenFeedbackCount > 0 ? Info : Healthy,
                "/home/sikayet-oneri/benim"),
            new(
                "todayShipment",
                "Bugunku Sevk",
                todayShipmentCount,
                Info,
                AppendWarehouseQuery("/operasyon-islemleri/belge-akis-takibi", warehouseNo)),
            new(
                "todayReceiving",
                "Bugunku Kabul",
                todayReceivingCount,
                Info,
                AppendWarehouseQuery("/operasyon-islemleri/belge-akis-takibi", warehouseNo))
        ];

    private static IReadOnlyCollection<HomePriorityItemDto> BuildPriorities(
        int? warehouseNo,
        int pendingReceivingCount,
        int failedEDespatchCount,
        int criticalStockAnomalyCount,
        int highStockAnomalyCount,
        int myOpenFeedbackCount)
    {
        var items = new List<HomePriorityItemDto>();

        if (failedEDespatchCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "failedEDespatch",
                Critical,
                "E-irsaliye gonderimi basarisiz",
                $"{failedEDespatchCount} belge tekrar kontrol bekliyor.",
                failedEDespatchCount,
                AppendWarehouseQuery("/operasyon-islemleri/belge-akis-takibi?status=Failed", warehouseNo)));
        }

        if (criticalStockAnomalyCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "criticalStockAnomaly",
                Critical,
                "Kritik stok anomalisi var",
                $"{criticalStockAnomalyCount} kritik anomali acik durumda.",
                criticalStockAnomalyCount,
                AppendWarehouseQuery("/stok-islemleri/stok-anomali-merkezi?status=Open&severity=Critical", warehouseNo)));
        }

        if (pendingReceivingCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "pendingReceiving",
                Warning,
                "Depo kabul bekliyor",
                $"{pendingReceivingCount} depo hareketi henuz kabul edilmemis.",
                pendingReceivingCount,
                AppendWarehouseQuery("/operasyon-islemleri/belge-akis-takibi", warehouseNo)));
        }

        if (highStockAnomalyCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "highStockAnomaly",
                Warning,
                "Yuksek onemli stok anomalisi var",
                $"{highStockAnomalyCount} yuksek onemli anomali takip bekliyor.",
                highStockAnomalyCount,
                AppendWarehouseQuery("/stok-islemleri/stok-anomali-merkezi?status=Open&severity=High", warehouseNo)));
        }

        if (myOpenFeedbackCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "myOpenFeedback",
                Info,
                "Acik sikayet/onerin var",
                $"{myOpenFeedbackCount} talep henuz kapanmamis.",
                myOpenFeedbackCount,
                "/home/sikayet-oneri/benim"));
        }

        return items;
    }

    private static IReadOnlyCollection<HomeQuickActionDto> BuildQuickActions(int? warehouseNo) =>
        [
            new(
                "documentFlow",
                "Belge Akisina Git",
                AppendWarehouseQuery("/operasyon-islemleri/belge-akis-takibi", warehouseNo),
                "operasyon-islemleri.belge-akis-takibi.list"),
            new(
                "stockAnomaly",
                "Stok Anomalilerini Ac",
                AppendWarehouseQuery("/stok-islemleri/stok-anomali-merkezi?status=Open", warehouseNo),
                "stok-islemleri.stok-anomali-merkezi.list"),
            new(
                "feedback",
                "Sikayet/Oneri Gonder",
                "/home/sikayet-oneri",
                null)
        ];

    private static string ResolveOverallStatus(
        int failedEDespatchCount,
        int criticalStockAnomalyCount,
        int pendingReceivingCount,
        int highStockAnomalyCount,
        int openStockAnomalyCount,
        int myOpenFeedbackCount)
    {
        if (failedEDespatchCount > 0 || criticalStockAnomalyCount > 0)
        {
            return Critical;
        }

        if (pendingReceivingCount > 0 || highStockAnomalyCount > 0 || openStockAnomalyCount > 0)
        {
            return Warning;
        }

        return myOpenFeedbackCount > 0 ? Info : Healthy;
    }

    private static string CreateHeadline(int priorityCount) =>
        priorityCount == 0
            ? "Bugun acil oncelik yok"
            : $"Bugun {priorityCount} oncelikli konu var";

    private static string AppendWarehouseQuery(string route, int? warehouseNo)
    {
        if (!warehouseNo.HasValue)
        {
            return route;
        }

        var separator = route.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{route}{separator}warehouseNo={warehouseNo.Value}";
    }

    private static DateTime ToUtc(DateOnly date)
    {
        var localStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        return localStart.ToUniversalTime();
    }

    private sealed record FlowSummary(
        int TodayShipmentCount,
        int TodayReceivingCount,
        int PendingReceivingCount,
        int FailedEDespatchCount);

    private sealed record StockAnomalySummary(
        int OpenCount,
        int CriticalCount,
        int HighCount);
}
