using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.BelgeAkisTakibi;

public sealed class DocumentFlowServiceTests
{
    [Fact]
    public async Task RecordAsync_DoesNotAppendDuplicateSuccessfulDocumentCreatedEvent()
    {
        await using var dbContext = CreateDbContext();
        var initialTime = new DateTime(2026, 10, 14, 6, 30, 0, DateTimeKind.Utc);
        var clock = new MutableClock(initialTime);
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);
        const string flowKey = "CompanyReceiving:56:CX56:101";
        var request = new RecordDocumentFlowRequest(
            flowKey,
            DocumentFlowType.CompanyReceiving,
            56,
            null,
            "CX56",
            101,
            DocumentFlowStep.DocumentCreated,
            DocumentFlowStatus.Succeeded,
            "Firma mal kabul olusturuldu.");

        await service.RecordAsync(request, CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordAsync(request, CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var flow = await dbContext.DocumentFlows
            .Include(item => item.Events)
            .SingleAsync(item => item.FlowKey == flowKey);

        Assert.Equal(initialTime, flow.UpdatedAtUtc);
        Assert.Collection(
            flow.Events,
            item =>
            {
                Assert.Equal(DocumentFlowStep.DocumentCreated, item.Step);
                Assert.Equal(DocumentFlowStatus.Succeeded, item.Status);
                Assert.Equal(initialTime, item.OccurredAtUtc);
            });
    }

    [Fact]
    public async Task RecordAsync_AppendsEventWhenExistingFlowIsUpdated()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 9, 30, 7, 0, 0, DateTimeKind.Utc));
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);
        const string flowKey = "InterWarehouseShipment:56:F56:88029";

        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                flowKey,
                DocumentFlowType.InterWarehouseShipment,
                56,
                102,
                "F56",
                88029,
                DocumentFlowStep.DocumentCreated,
                DocumentFlowStatus.Succeeded,
                "Depolar arasi sevk olusturuldu."),
            CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                flowKey,
                DocumentFlowType.InterWarehouseShipment,
                56,
                102,
                "F56",
                88029,
                DocumentFlowStep.EDespatchSubmission,
                DocumentFlowStatus.Succeeded,
                "E-irsaliye Uyumsoft'a basariyla gonderildi.",
                ExternalDocumentNo: "FRM2026600132100",
                ExternalUuid: "73e31507-3b9a-4ae2-b21d-83291b70e517"),
            CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                flowKey,
                DocumentFlowType.InterWarehouseShipment,
                56,
                102,
                "F56",
                88029,
                DocumentFlowStep.DocumentCreated,
                DocumentFlowStatus.Succeeded,
                "Depolar arasi sevk olusturuldu."),
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var flow = await dbContext.DocumentFlows
            .Include(item => item.Events)
            .SingleAsync(item => item.FlowKey == flowKey);

        Assert.Equal(DocumentFlowStep.EDespatchSubmission, flow.CurrentStep);
        Assert.Equal(DocumentFlowStatus.Succeeded, flow.Status);
        Assert.Equal("FRM2026600132100", flow.ExternalDocumentNo);
        Assert.Equal("73e31507-3b9a-4ae2-b21d-83291b70e517", flow.ExternalUuid);
        Assert.Collection(
            flow.Events.OrderBy(item => item.OccurredAtUtc),
            item => Assert.Equal(DocumentFlowStep.DocumentCreated, item.Step),
            item => Assert.Equal(DocumentFlowStep.EDespatchSubmission, item.Step));
    }

    [Fact]
    public async Task RecordAsync_DoesNotAppendDuplicateSuccessfulWarehouseReceivingAcceptedEvent()
    {
        await using var dbContext = CreateDbContext();
        var initialTime = new DateTime(2026, 10, 15, 7, 30, 0, DateTimeKind.Utc);
        var clock = new MutableClock(initialTime);
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);
        const string flowKey = "InterWarehouseShipment:56:F56:88730";
        var request = new RecordDocumentFlowRequest(
            flowKey,
            DocumentFlowType.InterWarehouseShipment,
            56,
            110,
            "F56",
            88730,
            DocumentFlowStep.WarehouseReceivingAccepted,
            DocumentFlowStatus.Succeeded,
            "AXATA G02 worker ile depo mal kabulu tamamlandi.");

        await service.RecordAsync(request, CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordAsync(request, CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var flow = await dbContext.DocumentFlows
            .Include(item => item.Events)
            .SingleAsync(item => item.FlowKey == flowKey);

        Assert.Equal(initialTime, flow.UpdatedAtUtc);
        Assert.Collection(
            flow.Events,
            item =>
            {
                Assert.Equal(DocumentFlowStep.WarehouseReceivingAccepted, item.Step);
                Assert.Equal(DocumentFlowStatus.Succeeded, item.Status);
                Assert.Equal(initialTime, item.OccurredAtUtc);
            });
    }

    [Fact]
    public async Task RecordAsync_DeduplicatesAxataOrderSentPerMovementType()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc));
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);
        const string flowKey = "IssuedWarehouseOrder:56:F56:88731";

        RecordDocumentFlowRequest CreateRequest(string movementType) => new(
            flowKey,
            DocumentFlowType.IssuedWarehouseOrder,
            56,
            110,
            "F56",
            88731,
            DocumentFlowStep.AxataOrderSent,
            DocumentFlowStatus.Succeeded,
            $"Siparis AXATA'ya {movementType} olarak gonderildi ve Mikro gonderim isareti dogrulandi.");

        await service.RecordAsync(CreateRequest("C01"), CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordAsync(CreateRequest("C01"), CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordAsync(CreateRequest("G02"), CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var flow = await dbContext.DocumentFlows
            .Include(item => item.Events)
            .SingleAsync(item => item.FlowKey == flowKey);

        Assert.Collection(
            flow.Events.OrderBy(item => item.OccurredAtUtc),
            item => Assert.Contains("C01", item.Message, StringComparison.Ordinal),
            item => Assert.Contains("G02", item.Message, StringComparison.Ordinal));
    }

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"document-flow-service-{Guid.NewGuid():N}")
            .Options;
        return new AuthDbContext(options);
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
