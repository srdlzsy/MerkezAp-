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
    public async Task GetAsync_CountsStockAnomaliesByPrimaryWarehouseOnly()
    {
        await using var authDbContext = CreateAuthDbContext();
        await using var mikroDbContext = CreateMikroDbContext();
        var now = new DateTime(2026, 7, 28, 8, 0, 0, DateTimeKind.Utc);
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
        await authDbContext.SaveChangesAsync();

        var service = new HomeWarehousePrioritiesService(
            authDbContext,
            mikroDbContext,
            new FixedClock(now));

        var result = await service.GetAsync(
            new HomeWarehousePrioritiesRequest(
                new DateOnly(2026, 7, 28),
                WarehouseNo: 53,
                WarehouseName: "ET-SARKUTERI DEPO",
                UserId: userId),
            CancellationToken.None);

        Assert.Equal(2, result.Metrics.Single(metric => metric.Code == "openStockAnomaly").Value);
        Assert.Equal(1, result.Priorities.Single(priority => priority.Code == "criticalStockAnomaly").Count);
        Assert.Equal(1, result.Priorities.Single(priority => priority.Code == "highStockAnomaly").Count);
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

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
