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
    [Theory]
    [InlineData(DocumentFlowStep.DocumentCreated)]
    [InlineData(DocumentFlowStep.OrderCreated)]
    public async Task RecordAsync_DoesNotAppendDuplicateSuccessfulCreateEvent(DocumentFlowStep step)
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
            step,
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
                Assert.Equal(step, item.Step);
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
    public async Task RecordManyAsync_AppendsCreatedEventAfterDocumentWasDeleted()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 11, 3, 7, 0, 0, DateTimeKind.Utc));
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);
        const string flowKey = "StockMovementDocument:116:POS:20261103:X:410";

        RecordDocumentFlowRequest CreateRequest(DocumentFlowStep step, string message) => new(
            flowKey,
            DocumentFlowType.StockMovementDocument,
            116,
            null,
            "X",
            410,
            step,
            DocumentFlowStatus.Succeeded,
            message);

        await service.RecordManyAsync(
            [CreateRequest(DocumentFlowStep.DocumentCreated, "POS evragi olusturuldu.")],
            CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordManyAsync(
            [CreateRequest(DocumentFlowStep.DocumentDeleted, "POS evragi silindi.")],
            CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await service.RecordManyAsync(
            [CreateRequest(DocumentFlowStep.DocumentCreated, "POS evragi yeniden olusturuldu.")],
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var flow = await dbContext.DocumentFlows
            .Include(item => item.Events)
            .SingleAsync(item => item.FlowKey == flowKey);

        Assert.Equal(DocumentFlowStep.DocumentCreated, flow.CurrentStep);
        Assert.Equal(
            [
                DocumentFlowStep.DocumentCreated,
                DocumentFlowStep.DocumentDeleted,
                DocumentFlowStep.DocumentCreated
            ],
            flow.Events.OrderBy(item => item.OccurredAtUtc).Select(item => item.Step));
    }

    [Fact]
    public async Task SuccessfulDocumentCreatedIndex_AllowsMultipleLifecycleEvents()
    {
        await using var dbContext = CreateDbContext();

        var eventType = dbContext.Model.FindEntityType(typeof(DocumentFlowEvent));
        var index = Assert.Single(
            eventType!.GetIndexes(),
            item => item.GetDatabaseName() == "ix_document_flow_events_successful_creation");

        Assert.False(index.IsUnique);
        Assert.Equal(
            "[step] = N'DocumentCreated' AND [status] = N'Succeeded'",
            index.GetFilter());
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

    [Fact]
    public async Task ListAsync_ReturnsStablePagesWithoutHidingTotalCount()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc));
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);

        for (var orderNo = 1; orderNo <= 3; orderNo++)
        {
            await service.RecordAsync(
                new RecordDocumentFlowRequest(
                    $"IssuedWarehouseOrder:50:F50:{orderNo}",
                    DocumentFlowType.IssuedWarehouseOrder,
                    50,
                    100 + orderNo,
                    "F50",
                    orderNo,
                    DocumentFlowStep.AxataOrderSent,
                    DocumentFlowStatus.Succeeded,
                    "AXATA siparisi gonderildi."),
                CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddMinutes(1);
        }

        var firstPage = await service.ListAsync(
            new DocumentFlowListRequest(null, null, null, null, null, null, Skip: 0, Take: 2),
            CancellationToken.None);
        var secondPage = await service.ListAsync(
            new DocumentFlowListRequest(null, null, null, null, null, null, Skip: 2, Take: 2),
            CancellationToken.None);

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(3, secondPage.TotalCount);
        Assert.Equal([3, 2], firstPage.Items.Select(item => item.DocumentOrderNo));
        Assert.Equal([1], secondPage.Items.Select(item => item.DocumentOrderNo));
    }

    [Fact]
    public async Task ListAsync_FiltersByPresetDirectionAndComputesSummaryCounts()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc));
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);

        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                "InterWarehouseShipment:50:F50:1",
                DocumentFlowType.InterWarehouseShipment,
                50,
                104,
                "F50",
                1,
                DocumentFlowStep.EDespatchSubmission,
                DocumentFlowStatus.Succeeded,
                "E-irsaliye gonderildi."),
            CancellationToken.None);

        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                "InterWarehouseShipment:104:F104:2",
                DocumentFlowType.InterWarehouseShipment,
                104,
                105,
                "F104",
                2,
                DocumentFlowStep.DocumentCreated,
                DocumentFlowStatus.Succeeded,
                "Sevk olusturuldu ama e-irsaliye gonderilmedi."),
            CancellationToken.None);

        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                "IssuedWarehouseOrder:104:D104:4",
                DocumentFlowType.IssuedWarehouseOrder,
                104,
                50,
                "D104",
                4,
                DocumentFlowStep.OrderCreated,
                DocumentFlowStatus.Succeeded,
                "Siparis olusturuldu."),
            CancellationToken.None);

        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                "InterWarehouseShipment:50:F50:3",
                DocumentFlowType.InterWarehouseShipment,
                50,
                104,
                "F50",
                3,
                DocumentFlowStep.DocumentDeleted,
                DocumentFlowStatus.Succeeded,
                "Silinen sevk."),
            CancellationToken.None);

        var pendingReceivingFor104 = await service.ListAsync(
            new DocumentFlowListRequest(
                WarehouseNo: 104,
                StartDate: new DateTime(2026, 10, 11),
                EndDate: new DateTime(2026, 11, 10),
                DocumentType: null,
                Status: null,
                Search: null,
                Direction: DocumentFlowDirection.Incoming,
                Preset: DocumentFlowPreset.PendingReceiving),
            CancellationToken.None);

        Assert.Equal(1, pendingReceivingFor104.TotalCount);
        Assert.Single(pendingReceivingFor104.Items);
        Assert.Equal(1, pendingReceivingFor104.Items.Single().DocumentOrderNo);
        Assert.NotNull(pendingReceivingFor104.Summary);
        Assert.Equal(1, pendingReceivingFor104.Summary.PendingReceivingCount);
        Assert.Equal(0, pendingReceivingFor104.Summary.UnacceptedOutgoingCount);
        Assert.Equal(1, pendingReceivingFor104.Summary.PendingEDespatchCount);
        Assert.Equal(3, pendingReceivingFor104.Summary.ActionRequiredCount);
        Assert.Equal(1, pendingReceivingFor104.Summary.OpenOrderCount);

        var actionRequiredFor104 = await service.ListAsync(
            new DocumentFlowListRequest(
                WarehouseNo: 104,
                StartDate: new DateTime(2026, 10, 11),
                EndDate: new DateTime(2026, 11, 10),
                DocumentType: null,
                Status: null,
                Search: null,
                Direction: DocumentFlowDirection.All,
                Preset: DocumentFlowPreset.ActionRequired),
            CancellationToken.None);

        Assert.Equal(3, actionRequiredFor104.TotalCount);
        Assert.Contains(
            actionRequiredFor104.Items,
            item => item.DocumentType == nameof(DocumentFlowType.IssuedWarehouseOrder) &&
                    item.DocumentOrderNo == 4);
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
