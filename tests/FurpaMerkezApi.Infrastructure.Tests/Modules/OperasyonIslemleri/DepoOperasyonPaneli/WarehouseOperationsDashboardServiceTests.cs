using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.DepoOperasyonPaneli;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.DepoOperasyonPaneli;

public sealed class WarehouseOperationsDashboardServiceTests
{
    [Fact]
    public async Task GetAsync_UsesMikroSnapshotForMovementAndReceivingCounts()
    {
        await using var authDbContext = CreateAuthDbContext();
        await using var mikroDbContext = CreateMikroDbContext();
        mikroDbContext.DEPOLARs.AddRange(
            new DEPOLAR { dep_no = 50, dep_adi = "MERKEZ DEPO", dep_iptal = false },
            new DEPOLAR { dep_no = 104, dep_adi = "SUBE 104", dep_iptal = false });
        await mikroDbContext.SaveChangesAsync();

        var selectedDate = new DateOnly(2026, 11, 10);
        var createdAt = new DateTime(2026, 11, 10, 8, 0, 0);
        var snapshot = new WarehouseMovementMikroSnapshot(
            true,
            [
                new("F50", 10, false, 50, 104, selectedDate, createdAt, null, false, true),
                new("F50", 11, false, 50, 104, selectedDate, createdAt, createdAt.AddMinutes(12), true, true),
                new("I104", 5, true, 104, 50, selectedDate, createdAt, null, false, false)
            ]);
        var axataSnapshot = new AxataOrderMikroSnapshot(
            true,
            [
                new(DocumentFlowType.IssuedWarehouseOrder, 104, 50, "D104", 6, selectedDate,
                    createdAt, createdAt, true, 10, 0, "issued-warehouse-order-sync"),
                new(DocumentFlowType.IssuedWarehouseOrder, 104, 50, "D104", 7, selectedDate,
                    createdAt, createdAt, false, 10, 0, "issued-warehouse-order-sync")
            ]);
        authDbContext.DocumentFlows.AddRange(
            CreateFailedEDespatchFlow("WarehouseReturn:104:I104:5", "I104", 5, createdAt),
            CreateFailedEDespatchFlow("WarehouseReturn:104:I104:99", "I104", 99, createdAt));
        await authDbContext.SaveChangesAsync();
        var service = new WarehouseOperationsDashboardService(
            authDbContext,
            mikroDbContext,
            new StaticMikroSnapshotReader(snapshot),
            new StaticAxataOrderSnapshotReader(axataSnapshot),
            new FixedClock(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc)),
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<WarehouseOperationsDashboardService>.Instance);

        var result = await service.GetAsync(selectedDate, CancellationToken.None);

        var warehouse50 = Assert.Single(result.Warehouses, item => item.WarehouseNo == 50);
        Assert.Equal(2, warehouse50.TodayShipmentCount);
        Assert.Equal(0, warehouse50.TodayReturnCount);
        Assert.Equal(0, warehouse50.TodayReceivingCount);
        Assert.Equal(0, warehouse50.PendingEDespatchCount);

        var warehouse104 = Assert.Single(result.Warehouses, item => item.WarehouseNo == 104);
        Assert.Equal(1, warehouse104.TodayReturnCount);
        Assert.Equal(1, warehouse104.TodayReceivingCount);
        Assert.Equal(1, warehouse104.PendingReceivingCount);
        Assert.Equal(0, warehouse104.UnacceptedOutgoingCount);
        Assert.Equal(0, warehouse104.PendingEDespatchCount);
        Assert.Equal(1, warehouse104.FailedEDespatchCount);
        Assert.Equal(12d, warehouse104.AverageReceivingMinutes);
        Assert.Equal(1, warehouse104.OpenOrderCount);
        Assert.Equal(1, result.Summary.OpenOrderCount);
    }

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

        var openOrderFrom104To50 = new DocumentFlow(
            Guid.NewGuid(),
            "IssuedWarehouseOrder:104:D104:6",
            DocumentFlowType.IssuedWarehouseOrder,
            sourceWarehouseNo: 104,
            targetWarehouseNo: 50,
            documentSerie: "D104",
            documentOrderNo: 6,
            createdAtUtc: todayUtc);
        openOrderFrom104To50.Record(
            DocumentFlowStep.OrderCreated,
            DocumentFlowStatus.Succeeded,
            "Siparis olusturuldu.",
            error: null,
            changedByUserId: null,
            occurredAtUtc: todayUtc);

        var orderBeforeOperationalStart = new DocumentFlow(
            Guid.NewGuid(),
            "IssuedWarehouseOrder:104:D104:5",
            DocumentFlowType.IssuedWarehouseOrder,
            sourceWarehouseNo: 104,
            targetWarehouseNo: 50,
            documentSerie: "D104",
            documentOrderNo: 5,
            createdAtUtc: new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));
        orderBeforeOperationalStart.Record(
            DocumentFlowStep.OrderCreated,
            DocumentFlowStatus.Succeeded,
            "Siparis olusturuldu.",
            error: null,
            changedByUserId: null,
            occurredAtUtc: new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc));

        authDbContext.DocumentFlows.AddRange(
            pendingShipmentFrom50To104,
            deletedShipmentFrom50To104,
            unsubmittedReturnFrom104To50,
            openOrderFrom104To50,
            orderBeforeOperationalStart);
        await authDbContext.SaveChangesAsync();

        var service = new WarehouseOperationsDashboardService(
            authDbContext,
            mikroDbContext,
            new UnavailableMikroSnapshotReader(),
            new UnavailableAxataOrderSnapshotReader(),
            new FixedClock(todayUtc),
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<WarehouseOperationsDashboardService>.Instance);

        var result = await service.GetAsync(new DateOnly(2026, 11, 10), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 11, 1), result.LookbackStartDate);

        var warehouse50 = Assert.Single(result.Warehouses, item => item.WarehouseNo == 50);
        Assert.Equal(0, warehouse50.TodayShipmentCount);
        Assert.Equal(0, warehouse50.PendingReceivingCount);
        Assert.Equal(1, warehouse50.UnacceptedOutgoingCount);
        Assert.Equal(1, warehouse50.OpenOrderCount);

        var warehouse104 = Assert.Single(result.Warehouses, item => item.WarehouseNo == 104);
        Assert.Equal(1, warehouse104.TodayReturnCount);
        Assert.Equal(1, warehouse104.PendingReceivingCount);
        Assert.Equal(1, warehouse104.PendingEDespatchCount);
        Assert.Equal(1, warehouse104.OpenOrderCount);
        Assert.Equal(3, warehouse104.IncompleteOperationCount);
        Assert.Equal("Warning", warehouse104.HealthStatus);
        Assert.Equal(1, result.Summary.OpenOrderCount);
        Assert.Equal(3, result.Summary.IncompleteOperationCount);
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

    private static DocumentFlow CreateFailedEDespatchFlow(
        string flowKey,
        string documentSerie,
        int documentOrderNo,
        DateTime createdAt)
    {
        var flow = new DocumentFlow(
            Guid.NewGuid(),
            flowKey,
            DocumentFlowType.WarehouseReturn,
            104,
            50,
            documentSerie,
            documentOrderNo,
            createdAt);
        flow.Record(
            DocumentFlowStep.EDespatchSubmission,
            DocumentFlowStatus.Failed,
            "E-irsaliye gonderilemedi.",
            "UYUM hatasi",
            null,
            createdAt);
        return flow;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class UnavailableMikroSnapshotReader : IWarehouseMovementMikroSnapshotReader
    {
        public Task<WarehouseMovementMikroSnapshot> ReadAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(WarehouseMovementMikroSnapshot.Unavailable);
    }

    private sealed class StaticMikroSnapshotReader(WarehouseMovementMikroSnapshot snapshot)
        : IWarehouseMovementMikroSnapshotReader
    {
        public Task<WarehouseMovementMikroSnapshot> ReadAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class UnavailableAxataOrderSnapshotReader : IAxataOrderMikroSnapshotReader
    {
        public Task<AxataOrderMikroSnapshot> ReadAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(AxataOrderMikroSnapshot.Unavailable);
    }

    private sealed class StaticAxataOrderSnapshotReader(AxataOrderMikroSnapshot snapshot)
        : IAxataOrderMikroSnapshotReader
    {
        public Task<AxataOrderMikroSnapshot> ReadAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken) =>
            Task.FromResult(snapshot);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
