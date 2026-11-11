using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataOrderDocumentFlowRecorderTests
{
    [Theory]
    [InlineData("C01", DocumentFlowType.IssuedWarehouseOrder, 50, 131, 131, 50)]
    [InlineData("G02", DocumentFlowType.ReceivedWarehouseOrder, 131, 50, 131, 50)]
    public async Task RecordWarehouseOrderSentAsync_UsesStableOwnershipAndExpectedType(
        string movementType,
        DocumentFlowType expectedType,
        int outWarehouseNo,
        int inWarehouseNo,
        int expectedSourceWarehouseNo,
        int expectedTargetWarehouseNo)
    {
        var service = new RecordingDocumentFlowService();
        var recorder = new AxataOrderDocumentFlowRecorder(service);
        var detail = CreateWarehouseOrder(outWarehouseNo, inWarehouseNo);

        await recorder.RecordWarehouseOrderSentAsync(
            detail,
            movementType,
            Guid.Empty,
            CancellationToken.None);

        var request = Assert.Single(service.Requests);
        Assert.Equal(expectedType, request.DocumentType);
        Assert.Equal(expectedSourceWarehouseNo, request.SourceWarehouseNo);
        Assert.Equal(expectedTargetWarehouseNo, request.TargetWarehouseNo);
        Assert.Equal(DocumentFlowStep.AxataOrderSent, request.Step);
        Assert.Equal(DocumentFlowStatus.Succeeded, request.Status);
        Assert.Equal(
            DocumentFlowKeys.Create(expectedType, expectedSourceWarehouseNo, "F131", 5506),
            request.FlowKey);
    }

    [Fact]
    public async Task RecordWarehouseOrderSentAsync_C01_AdvancesExistingCreatedOrderFlow()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 11, 11, 7, 0, 0, DateTimeKind.Utc));
        var service = new DocumentFlowService(
            dbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);
        var detail = CreateWarehouseOrder(50, 131);
        var flowKey = DocumentFlowKeys.Create(
            DocumentFlowType.IssuedWarehouseOrder,
            131,
            "F131",
            5506);

        await service.RecordAsync(
            new RecordDocumentFlowRequest(
                flowKey,
                DocumentFlowType.IssuedWarehouseOrder,
                131,
                50,
                "F131",
                5506,
                DocumentFlowStep.OrderCreated,
                DocumentFlowStatus.Succeeded,
                "Verilen depo siparisi olusturuldu."),
            CancellationToken.None);

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        await new AxataOrderDocumentFlowRecorder(service).RecordWarehouseOrderSentAsync(
            detail,
            "C01",
            Guid.Empty,
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();
        var flow = await dbContext.DocumentFlows
            .Include(item => item.Events)
            .SingleAsync();

        Assert.Equal(flowKey, flow.FlowKey);
        Assert.Equal(DocumentFlowStep.AxataOrderSent, flow.CurrentStep);
        Assert.Equal(2, flow.Events.Count);
    }

    [Theory]
    [InlineData("C02", DocumentFlowType.ReceivedCompanyOrder)]
    [InlineData("G01", DocumentFlowType.IssuedCompanyOrder)]
    public async Task RecordCompanyOrderSentAsync_UsesExpectedType(
        string movementType,
        DocumentFlowType expectedType)
    {
        var service = new RecordingDocumentFlowService();
        var recorder = new AxataOrderDocumentFlowRecorder(service);
        var detail = CreateCompanyOrder();

        await recorder.RecordCompanyOrderSentAsync(
            detail,
            movementType,
            Guid.Empty,
            CancellationToken.None);

        var request = Assert.Single(service.Requests);
        Assert.Equal(expectedType, request.DocumentType);
        Assert.Equal(50, request.SourceWarehouseNo);
        Assert.Null(request.TargetWarehouseNo);
        Assert.Equal(DocumentFlowStep.AxataOrderSent, request.Step);
        Assert.Equal(DocumentFlowStatus.Succeeded, request.Status);
        Assert.Equal(
            DocumentFlowKeys.Create(expectedType, 50, "A", 1939),
            request.FlowKey);
    }

    private static WarehouseOrderDetailDto CreateWarehouseOrder(
        int outWarehouseNo,
        int inWarehouseNo) =>
        new(
            new WarehouseOrderHeaderDto(
                null,
                new DateTime(2026, 10, 27),
                null,
                "F131",
                5506,
                "F131.5506",
                inWarehouseNo,
                "Hedef Depo",
                outWarehouseNo,
                "Kaynak Depo",
                inWarehouseNo,
                "Hedef Depo",
                outWarehouseNo,
                "Kaynak Depo",
                2,
                10,
                0,
                10,
                0,
                false),
            Array.Empty<WarehouseOrderLineItemDto>());

    private static CompanyOrderDetailDto CreateCompanyOrder() =>
        new(
            new CompanyOrderHeaderDto(
                null,
                new DateTime(2026, 10, 27),
                null,
                "A",
                1939,
                "A.1939",
                50,
                "MERKEZ DEPO",
                "7740669327",
                "SORELLA GIDA",
                string.Empty,
                "SORELLA GIDA",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                true,
                5,
                19740,
                0,
                19740,
                2027550,
                false),
            Array.Empty<CompanyOrderLineItemDto>());

    private sealed class RecordingDocumentFlowService : IDocumentFlowService
    {
        public List<RecordDocumentFlowRequest> Requests { get; } = [];

        public Task RecordAsync(
            RecordDocumentFlowRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<DocumentFlowListResponse> ListAsync(
            DocumentFlowListRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DocumentFlowDetailDto> GetAsync(
            Guid id,
            int? allowedWarehouseNo,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"axata-order-flow-{Guid.NewGuid():N}")
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
