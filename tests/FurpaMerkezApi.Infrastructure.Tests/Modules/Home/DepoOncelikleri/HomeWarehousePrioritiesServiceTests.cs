using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.Home.DepoOncelikleri;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Home.DepoOncelikleri;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Home.DepoOncelikleri;

public sealed class HomeWarehousePrioritiesServiceTests
{
    [Fact]
    public async Task GetAsync_CountsOperationalDataWithinLookbackWindowAndExcludesDeletedFlows()
    {
        await using var authDbContext = CreateAuthDbContext();
        await using var mikroDbContext = CreateMikroDbContext();
        var now = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var withinThirtyDayLookback = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        var beforeThirtyDayLookback = new DateTime(2026, 10, 5, 20, 0, 0, DateTimeKind.Utc);
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        authDbContext.StockAnomalies.Add(CreateAnomaly(
            "critical-53",
            StockAnomalySeverity.Critical,
            warehouseNo: 53,
            relatedWarehouseNo: null,
            now));
        authDbContext.StockAnomalies.Add(CreateAnomaly(
            "high-53",
            StockAnomalySeverity.High,
            warehouseNo: 53,
            relatedWarehouseNo: 12,
            now));
        authDbContext.StockAnomalies.Add(CreateAnomaly(
            "related-53",
            StockAnomalySeverity.High,
            warehouseNo: 12,
            relatedWarehouseNo: 53,
            now));
        authDbContext.StockAnomalies.Add(CreateAnomaly(
            "acknowledged-53",
            StockAnomalySeverity.Critical,
            warehouseNo: 53,
            relatedWarehouseNo: null,
            now,
            StockAnomalyStatus.Acknowledged));
        authDbContext.StockAnomalies.Add(CreateAnomaly(
            "old-critical-53",
            StockAnomalySeverity.Critical,
            warehouseNo: 53,
            relatedWarehouseNo: null,
            beforeThirtyDayLookback));

        authDbContext.DocumentFlows.Add(CreatePendingReceivingFlow(
            "too-old-flow",
            sourceWarehouseNo: 50,
            targetWarehouseNo: 53,
            beforeThirtyDayLookback));
        authDbContext.DocumentFlows.Add(CreatePendingReceivingFlow(
            "october-pending-flow",
            sourceWarehouseNo: 50,
            targetWarehouseNo: 53,
            withinThirtyDayLookback));
        authDbContext.DocumentFlows.Add(CreatePendingReceivingFlow(
            "today-pending-flow",
            sourceWarehouseNo: 50,
            targetWarehouseNo: 53,
            now));

        var deletedFlow = CreatePendingReceivingFlow(
            "deleted-flow",
            sourceWarehouseNo: 50,
            targetWarehouseNo: 53,
            now);
        deletedFlow.Record(
            DocumentFlowStep.DocumentDeleted,
            DocumentFlowStatus.Succeeded,
            "Belge silindi.",
            error: null,
            changedByUserId: null,
            occurredAtUtc: now);
        authDbContext.DocumentFlows.Add(deletedFlow);

        var outgoingUnacceptedAndUnsubmittedFlow = CreatePendingReceivingFlow(
            "outgoing-from-53",
            sourceWarehouseNo: 53,
            targetWarehouseNo: 104,
            now);
        authDbContext.DocumentFlows.Add(outgoingUnacceptedAndUnsubmittedFlow);

        var unsubmittedFlow = new DocumentFlow(
            Guid.NewGuid(),
            "unsubmitted-53",
            DocumentFlowType.InterWarehouseShipment,
            sourceWarehouseNo: 53,
            targetWarehouseNo: 104,
            documentSerie: "F53",
            documentOrderNo: 2,
            createdAtUtc: now);
        authDbContext.DocumentFlows.Add(unsubmittedFlow);

        await authDbContext.SaveChangesAsync();

        var service = new HomeWarehousePrioritiesService(
            authDbContext,
            mikroDbContext,
            new FixedClock(now));

        var result = await service.GetAsync(
            new HomeWarehousePrioritiesRequest(
                new DateOnly(2026, 11, 10),
                WarehouseNo: 53,
                WarehouseName: "ET-SARKUTERI DEPO",
                UserId: userId),
            CancellationToken.None);

        Assert.Equal(2, result.Metrics.Single(metric => metric.Code == "openStockAnomaly").Value);
        Assert.Equal(1, result.Priorities.Single(priority => priority.Code == "criticalStockAnomaly").Count);
        Assert.Equal(1, result.Priorities.Single(priority => priority.Code == "highStockAnomaly").Count);
        Assert.Equal(2, result.Metrics.Single(metric => metric.Code == "pendingReceiving").Value);
        Assert.Equal(1, result.Metrics.Single(metric => metric.Code == "unacceptedOutgoing").Value);
        Assert.Equal(1, result.Metrics.Single(metric => metric.Code == "failedEDespatch").Value);

        var pendingReceivingPriority = result.Priorities.Single(priority => priority.Code == "pendingReceiving");
        Assert.Equal(2, pendingReceivingPriority.Count);
        Assert.Contains("preset=pending-receiving", pendingReceivingPriority.Route, StringComparison.Ordinal);
        Assert.Contains("direction=Incoming", pendingReceivingPriority.Route, StringComparison.Ordinal);
        Assert.Contains("startDate=2026-10-11", pendingReceivingPriority.Route, StringComparison.Ordinal);
        Assert.Contains("endDate=2026-11-10", pendingReceivingPriority.Route, StringComparison.Ordinal);
    }

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"home-priorities-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

    private static MikroDbContext CreateMikroDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"home-priorities-mikro-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }

    private static StockAnomaly CreateAnomaly(
        string sourceKey,
        StockAnomalySeverity severity,
        int warehouseNo,
        int? relatedWarehouseNo,
        DateTime now,
        StockAnomalyStatus? status = null)
    {
        var anomaly = new StockAnomaly(
            Guid.NewGuid(),
            sourceKey,
            StockAnomalyType.PendingInterWarehouseTransfer,
            severity,
            warehouseNo,
            now);

        anomaly.Detect(
            severity,
            relatedWarehouseNo,
            warehouseName: $"Depo {warehouseNo}",
            relatedWarehouseName: relatedWarehouseNo.HasValue ? $"Depo {relatedWarehouseNo.Value}" : null,
            productCode: "STK001",
            productName: "Test stock",
            productManagerCode: null,
            productManagerName: null,
            documentSerie: "D53",
            documentOrderNo: 1,
            documentNo: null,
            movementGuid: null,
            quantity: 1d,
            expectedQuantity: null,
            actualQuantity: null,
            averageQuantity: null,
            occurredAtUtc: now,
            message: "Test anomaly",
            evidence: null,
            detectedAtUtc: now);

        if (status.HasValue)
        {
            anomaly.ChangeStatus(status.Value, note: null, changedByUserId: null, changedAtUtc: now);
        }

        return anomaly;
    }

    private static DocumentFlow CreatePendingReceivingFlow(
        string flowKey,
        int sourceWarehouseNo,
        int targetWarehouseNo,
        DateTime createdAtUtc)
    {
        var flow = new DocumentFlow(
            Guid.NewGuid(),
            flowKey,
            DocumentFlowType.InterWarehouseShipment,
            sourceWarehouseNo,
            targetWarehouseNo,
            documentSerie: "F53",
            documentOrderNo: 1,
            createdAtUtc);
        flow.Record(
            DocumentFlowStep.EDespatchSubmission,
            DocumentFlowStatus.Succeeded,
            "E-irsaliye gonderildi.",
            error: null,
            changedByUserId: null,
            occurredAtUtc: createdAtUtc,
            externalDocumentNo: "FRM2026000001",
            externalUuid: Guid.NewGuid().ToString());
        return flow;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
