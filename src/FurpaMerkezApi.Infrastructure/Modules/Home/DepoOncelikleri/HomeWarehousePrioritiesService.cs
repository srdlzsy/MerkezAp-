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
    private const int OperationalLookbackDays = 30;
    private static readonly DateOnly MinimumOperationalStartDate = new(2026, 10, 1);

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

        var lookbackStartDate = ResolveLookbackStartDate(request.Date);
        var startUtc = ToUtc(request.Date);
        var endUtc = ToUtc(request.Date.AddDays(1));
        var operationalDataStartUtc = ToUtc(lookbackStartDate);
        var warehouseName = await ResolveWarehouseNameAsync(request, cancellationToken);
        var flowSummary = await LoadFlowSummaryAsync(
            request.WarehouseNo,
            startUtc,
            endUtc,
            operationalDataStartUtc,
            cancellationToken);
        var stockAnomalySummary = await LoadStockAnomalySummaryAsync(
            request.WarehouseNo,
            operationalDataStartUtc,
            cancellationToken);
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
            lookbackStartDate,
            request.Date,
            flowSummary,
            stockAnomalySummary.CriticalCount,
            stockAnomalySummary.HighCount,
            myOpenFeedbackCount);
        var metrics = BuildMetrics(
            request.WarehouseNo,
            lookbackStartDate,
            request.Date,
            flowSummary,
            stockAnomalySummary.OpenCount,
            myOpenFeedbackCount);
        var overallStatus = ResolveOverallStatus(
            flowSummary,
            stockAnomalySummary.CriticalCount,
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
            BuildQuickActions(request.WarehouseNo, lookbackStartDate, request.Date));
    }

    private async Task<FlowSummary> LoadFlowSummaryAsync(
        int? warehouseNo,
        DateTime startUtc,
        DateTime endUtc,
        DateTime operationalDataStartUtc,
        CancellationToken cancellationToken)
    {
        var hasWarehouseNo = warehouseNo.HasValue;
        var scopedWarehouseNo = warehouseNo ?? 0;

        var query = authDbContext.DocumentFlows
            .AsNoTracking()
            .Where(flow =>
                flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                ((flow.CreatedAtUtc >= operationalDataStartUtc && flow.CreatedAtUtc < endUtc) ||
                 (flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted &&
                  flow.UpdatedAtUtc >= startUtc &&
                  flow.UpdatedAtUtc < endUtc)));

        if (hasWarehouseNo)
        {
            query = query.Where(flow =>
                flow.SourceWarehouseNo == scopedWarehouseNo ||
                flow.TargetWarehouseNo == scopedWarehouseNo);
        }

        var rows = await query
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

        var operationalRows = rows
            .Where(flow => flow.CreatedAtUtc >= operationalDataStartUtc && flow.CreatedAtUtc < endUtc)
            .ToArray();

        var todayShipmentCount = rows.Count(flow =>
            (!hasWarehouseNo || flow.SourceWarehouseNo == scopedWarehouseNo) &&
            IsShipment(flow.DocumentType) &&
            flow.CreatedAtUtc >= startUtc &&
            flow.CreatedAtUtc < endUtc &&
            !IsFailedCreation(flow));

        var todayReceivingCount = rows.Count(flow =>
            (!hasWarehouseNo || flow.TargetWarehouseNo == scopedWarehouseNo) &&
            flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted &&
            flow.UpdatedAtUtc >= startUtc &&
            flow.UpdatedAtUtc < endUtc);

        var pendingReceivingCount = operationalRows.Count(flow =>
            (!hasWarehouseNo || flow.TargetWarehouseNo == scopedWarehouseNo) &&
            IsPendingReceiving(flow));

        var overduePendingReceivingCount = operationalRows.Count(flow =>
            (!hasWarehouseNo || flow.TargetWarehouseNo == scopedWarehouseNo) &&
            IsPendingReceiving(flow) &&
            flow.CreatedAtUtc < startUtc);

        var unacceptedOutgoingCount = operationalRows.Count(flow =>
            (!hasWarehouseNo || flow.SourceWarehouseNo == scopedWarehouseNo) &&
            IsPendingReceiving(flow));

        var failedEDespatchCount = operationalRows.Count(flow =>
            (!hasWarehouseNo || flow.SourceWarehouseNo == scopedWarehouseNo) &&
            flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
            flow.Status == DocumentFlowStatus.Failed);

        var unsubmittedEDespatchCount = operationalRows.Count(flow =>
            (!hasWarehouseNo || flow.SourceWarehouseNo == scopedWarehouseNo) &&
            flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
            flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted &&
            flow.Status == DocumentFlowStatus.Succeeded &&
            flow.ExternalUuid == null &&
            IsDespatchDocument(flow.DocumentType) &&
            (flow.CurrentStep == DocumentFlowStep.DocumentCreated ||
             flow.CurrentStep == DocumentFlowStep.AxataDocumentImported ||
             flow.CurrentStep == DocumentFlowStep.DocumentUpdated));

        var otherFailedOperationCount = operationalRows.Count(flow =>
            (!hasWarehouseNo ||
             flow.SourceWarehouseNo == scopedWarehouseNo ||
             flow.TargetWarehouseNo == scopedWarehouseNo) &&
            flow.CurrentStep != DocumentFlowStep.EDespatchSubmission &&
            flow.Status == DocumentFlowStatus.Failed);

        return new FlowSummary(
            todayShipmentCount,
            todayReceivingCount,
            pendingReceivingCount,
            overduePendingReceivingCount,
            unacceptedOutgoingCount,
            failedEDespatchCount,
            unsubmittedEDespatchCount,
            otherFailedOperationCount);
    }

    private async Task<StockAnomalySummary> LoadStockAnomalySummaryAsync(
        int? warehouseNo,
        DateTime operationalDataStartUtc,
        CancellationToken cancellationToken)
    {
        var hasWarehouseNo = warehouseNo.HasValue;
        var scopedWarehouseNo = warehouseNo ?? 0;

        var query = authDbContext.StockAnomalies
            .AsNoTracking()
            .Where(anomaly =>
                anomaly.Status == StockAnomalyStatus.Open &&
                anomaly.FirstDetectedAtUtc >= operationalDataStartUtc);

        if (hasWarehouseNo)
        {
            query = query.Where(anomaly => anomaly.WarehouseNo == scopedWarehouseNo);
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
        DateOnly lookbackStartDate,
        DateOnly selectedDate,
        FlowSummary flowSummary,
        int openStockAnomalyCount,
        int myOpenFeedbackCount)
    {
        var pendingEDespatchTotal = flowSummary.FailedEDespatchCount + flowSummary.UnsubmittedEDespatchCount;

        return
        [
            new(
                "failedEDespatch",
                "E-Irsaliye Eksik / Hata",
                pendingEDespatchTotal,
                flowSummary.FailedEDespatchCount > 0
                    ? Critical
                    : pendingEDespatchTotal > 0
                        ? Warning
                        : Healthy,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "pending-edespatch",
                    direction: "Outgoing",
                    startDate: lookbackStartDate,
                    endDate: selectedDate)),
            new(
                "pendingReceiving",
                "Bekleyen Kabul",
                flowSummary.PendingReceivingCount,
                flowSummary.PendingReceivingCount > 0 ? Warning : Healthy,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "pending-receiving",
                    direction: "Incoming",
                    startDate: lookbackStartDate,
                    endDate: selectedDate)),
            new(
                "unacceptedOutgoing",
                "Kabul Bekleyen Cikis",
                flowSummary.UnacceptedOutgoingCount,
                flowSummary.UnacceptedOutgoingCount > 0 ? Info : Healthy,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "unaccepted-outgoing",
                    direction: "Outgoing",
                    startDate: lookbackStartDate,
                    endDate: selectedDate)),
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
                flowSummary.TodayShipmentCount,
                Info,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "today-shipments",
                    direction: "Outgoing",
                    startDate: selectedDate,
                    endDate: selectedDate)),
            new(
                "todayReceiving",
                "Bugunku Kabul",
                flowSummary.TodayReceivingCount,
                Info,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "today-receivings",
                    direction: "Incoming",
                    startDate: selectedDate,
                    endDate: selectedDate))
        ];
    }

    private static IReadOnlyCollection<HomePriorityItemDto> BuildPriorities(
        int? warehouseNo,
        DateOnly lookbackStartDate,
        DateOnly selectedDate,
        FlowSummary flowSummary,
        int criticalStockAnomalyCount,
        int highStockAnomalyCount,
        int myOpenFeedbackCount)
    {
        var items = new List<HomePriorityItemDto>();

        if (flowSummary.FailedEDespatchCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "failedEDespatch",
                Critical,
                "E-irsaliye gonderimi basarisiz",
                $"{flowSummary.FailedEDespatchCount} belge tekrar kontrol bekliyor.",
                flowSummary.FailedEDespatchCount,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "failed-edespatch",
                    direction: "Outgoing",
                    startDate: lookbackStartDate,
                    endDate: selectedDate)));
        }

        if (flowSummary.OtherFailedOperationCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "otherFailedFlows",
                Critical,
                "Hatali belge akisi var",
                $"Toplam {flowSummary.FailedEDespatchCount + flowSummary.OtherFailedOperationCount} belge akisi hata almis durumda.",
                flowSummary.FailedEDespatchCount + flowSummary.OtherFailedOperationCount,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "failed-only",
                    direction: null,
                    startDate: lookbackStartDate,
                    endDate: selectedDate)));
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

        if (flowSummary.UnsubmittedEDespatchCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "unsubmittedEDespatch",
                Warning,
                "E-irsaliyesi gonderilmemis sevk/iade var",
                $"{flowSummary.UnsubmittedEDespatchCount} evrak olusturulmus ancak e-irsaliyesi henuz gonderilmemis.",
                flowSummary.UnsubmittedEDespatchCount,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "pending-edespatch",
                    direction: "Outgoing",
                    startDate: lookbackStartDate,
                    endDate: selectedDate)));
        }

        if (flowSummary.PendingReceivingCount > 0)
        {
            var description = flowSummary.OverduePendingReceivingCount > 0
                ? $"{flowSummary.PendingReceivingCount} depo hareketi kabul bekliyor ({flowSummary.OverduePendingReceivingCount} tanesi gecmis gunlerden)."
                : $"{flowSummary.PendingReceivingCount} depo hareketi henuz kabul edilmemis.";

            items.Add(new HomePriorityItemDto(
                "pendingReceiving",
                Warning,
                "Depo kabul bekliyor",
                description,
                flowSummary.PendingReceivingCount,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "pending-receiving",
                    direction: "Incoming",
                    startDate: lookbackStartDate,
                    endDate: selectedDate)));
        }

        if (flowSummary.UnacceptedOutgoingCount > 0)
        {
            items.Add(new HomePriorityItemDto(
                "unacceptedOutgoing",
                Info,
                "Karsi depoda kabul bekleyen cikis var",
                $"{flowSummary.UnacceptedOutgoingCount} gonderilen sevk/iade karsi depo tarafindan henuz kabul edilmemis.",
                flowSummary.UnacceptedOutgoingCount,
                BuildFlowRoute(
                    warehouseNo,
                    preset: "unaccepted-outgoing",
                    direction: "Outgoing",
                    startDate: lookbackStartDate,
                    endDate: selectedDate)));
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

    private static IReadOnlyCollection<HomeQuickActionDto> BuildQuickActions(
        int? warehouseNo,
        DateOnly lookbackStartDate,
        DateOnly selectedDate) =>
        [
            new(
                "actionRequiredFlows",
                "Bekleyen Eksik Islemleri Ac",
                BuildFlowRoute(
                    warehouseNo,
                    preset: "action-required",
                    direction: null,
                    startDate: lookbackStartDate,
                    endDate: selectedDate),
                "operasyon-islemleri.belge-akis-takibi.list"),
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
        FlowSummary flowSummary,
        int criticalStockAnomalyCount,
        int highStockAnomalyCount,
        int openStockAnomalyCount,
        int myOpenFeedbackCount)
    {
        if (flowSummary.FailedEDespatchCount > 0 ||
            flowSummary.OtherFailedOperationCount > 0 ||
            criticalStockAnomalyCount > 0)
        {
            return Critical;
        }

        if (flowSummary.PendingReceivingCount > 0 ||
            flowSummary.UnsubmittedEDespatchCount > 0 ||
            highStockAnomalyCount > 0 ||
            openStockAnomalyCount > 0)
        {
            return Warning;
        }

        return flowSummary.UnacceptedOutgoingCount > 0 || myOpenFeedbackCount > 0
            ? Info
            : Healthy;
    }

    private static string CreateHeadline(int priorityCount) =>
        priorityCount == 0
            ? "Bugun acil oncelik yok"
            : $"Bugun {priorityCount} oncelikli konu var";

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

    private static bool IsFailedCreation(FlowRow flow) =>
        flow.CurrentStep == DocumentFlowStep.DocumentCreated &&
        flow.Status == DocumentFlowStatus.Failed;

    private static bool IsPendingReceiving(FlowRow flow) =>
        flow.TargetWarehouseNo.HasValue &&
        IsWarehouseMovement(flow.DocumentType) &&
        flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
        flow.Status == DocumentFlowStatus.Succeeded;

    private static string BuildFlowRoute(
        int? warehouseNo,
        string? preset,
        string? direction,
        DateOnly startDate,
        DateOnly endDate)
    {
        var parameters = new List<string>();
        if (warehouseNo.HasValue)
        {
            parameters.Add($"warehouseNo={warehouseNo.Value}");
        }

        if (!string.IsNullOrWhiteSpace(preset))
        {
            parameters.Add($"preset={preset}");
        }

        if (!string.IsNullOrWhiteSpace(direction))
        {
            parameters.Add($"direction={direction}");
        }

        parameters.Add($"startDate={startDate:yyyy-MM-dd}");
        parameters.Add($"endDate={endDate:yyyy-MM-dd}");

        return $"/operasyon-islemleri/belge-akis-takibi?{string.Join("&", parameters)}";
    }

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

    private sealed record FlowRow(
        DocumentFlowType DocumentType,
        int SourceWarehouseNo,
        int? TargetWarehouseNo,
        DocumentFlowStatus Status,
        DocumentFlowStep CurrentStep,
        string? ExternalUuid,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed record FlowSummary(
        int TodayShipmentCount,
        int TodayReceivingCount,
        int PendingReceivingCount,
        int OverduePendingReceivingCount,
        int UnacceptedOutgoingCount,
        int FailedEDespatchCount,
        int UnsubmittedEDespatchCount,
        int OtherFailedOperationCount);

    private sealed record StockAnomalySummary(
        int OpenCount,
        int CriticalCount,
        int HighCount);
}
