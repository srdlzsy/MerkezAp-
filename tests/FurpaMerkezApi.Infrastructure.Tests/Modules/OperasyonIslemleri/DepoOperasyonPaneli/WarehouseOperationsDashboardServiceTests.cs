using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.DepoOperasyonPaneli;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.DepoOperasyonPaneli;

public sealed class WarehouseOperationsDashboardServiceTests
{
    [Fact]
    public async Task GetAsync_ExcludesDeletedFlowsAndCountsPendingReceivingAndEDespatch()
    {
        await using var authDbContext = CreateAuthDbContext();
        await using var mikroDbContext = CreateMikroDbContext();

        mikroDbContext.DEPOLARs.AddRange(
            new DEPOLAR { dep_no = 50, dep_adi = "MERKEZ DEPO", dep_iptal = false },
            new DEPOLAR { dep_no = 104, dep_adi = "SUBE 104", dep_iptal = false });
        await mikroDbContext.SaveChangesAsync();

        var todayUtc = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);
        var lastWeekUtc = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);

        var pendingShipmentFrom50To104 = new DocumentFlow(
            Guid.NewGuid(),
            "InterWarehouseShipment:50:F50:10",
            DocumentFlowType.InterWarehouseShipment,
            sourceWarehouseNo: 50,
            targetWarehouseNo: 104,
            documentSerie: "F50",
            documentOrderNo: 10,
            createdAtUtc: lastWeekUtc);
        pendingShipmentFrom50To104.Record(
            DocumentFlowStep.EDespatchSubmission,
            DocumentFlowStatus.Succeeded,
            "E-irsaliye gonderildi.",
            error: null,
            changedByUserId: null,
            occurredAtUtc: lastWeekUtc);

        var deletedShipmentFrom50To104 = new DocumentFlow(
            Guid.NewGuid(),
            "InterWarehouseShipment:50:F50:11",
            DocumentFlowType.InterWarehouseShipment,
            sourceWarehouseNo: 50,
            targetWarehouseNo: 104,
            documentSerie: "F50",
            documentOrderNo: 11,
            createdAtUtc: todayUtc);
        deletedShipmentFrom50To104.Record(
            DocumentFlowStep.DocumentDeleted,
            DocumentFlowStatus.Succeeded,
            "Silindi.",
            error: null,
            changedByUserId: null,
            occurredAtUtc: todayUtc);

        var unsubmittedReturnFrom104To50 = new DocumentFlow(
            Guid.NewGuid(),
            "WarehouseReturn:104:I104:5",
            DocumentFlowType.WarehouseReturn,
            sourceWarehouseNo: 104,
            targetWarehouseNo: 50,
            documentSerie: "I104",
            documentOrderNo: 5,
            createdAtUtc: todayUtc);

        authDbContext.DocumentFlows.AddRange(
            pendingShipmentFrom50To104,
            deletedShipmentFrom50To104,
            unsubmittedReturnFrom104To50);
        await authDbContext.SaveChangesAsync();

        var service = new WarehouseOperationsDashboardService(
            authDbContext,
            mikroDbContext,
            new FixedClock(todayUtc),
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }));

        var result = await service.GetAsync(new DateOnly(2026, 11, 10), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 10, 11), result.LookbackStartDate);

        var warehouse50 = Assert.Single(result.Warehouses, item => item.WarehouseNo == 50);
        Assert.Equal(0, warehouse50.TodayShipmentCount);
        Assert.Equal(0, warehouse50.PendingReceivingCount);
        Assert.Equal(1, warehouse50.UnacceptedOutgoingCount);

        var warehouse104 = Assert.Single(result.Warehouses, item => item.WarehouseNo == 104);
        Assert.Equal(1, warehouse104.TodayReturnCount);
        Assert.Equal(1, warehouse104.PendingReceivingCount);
        Assert.Equal(1, warehouse104.PendingEDespatchCount);
        Assert.Equal(2, warehouse104.IncompleteOperationCount);
        Assert.Equal("Warning", warehouse104.HealthStatus);
    }

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"warehouse-dashboard-auth-{Guid.NewGuid():N}")
            .Options;
        return new AuthDbContext(options);
    }

    private static MikroDbContext CreateMikroDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"warehouse-dashboard-mikro-{Guid.NewGuid():N}")
            .Options;
        return new MikroDbContext(options);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
