using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataG01InboundAtfImportServiceTests
{
    [Fact]
    public void GroupDocuments_SplitsSameOrderByDespatchNoInSequenceOrder()
    {
        var lines = new[]
        {
            new G01InboundAtfLine(141591, "F50.23562", 6, "024360", 4752, "32000441", "3998", "0", new DateTime(2026, 11, 19)),
            new G01InboundAtfLine(141575, "F50.23562", 0, "072693", 2880, "32000441", "3285", "0", new DateTime(2026, 11, 19)),
            new G01InboundAtfLine(141576, "F50.23562", 1, "069971", 720, "32000441", "3285", "0", new DateTime(2026, 11, 19))
        };

        var documents = AxataG01InboundAtfImportService.GroupDocuments(lines).ToArray();

        Assert.Equal(2, documents.Length);
        Assert.Equal("F50.23562", documents[0].OrderDocumentNo);
        Assert.Equal("3285", documents[0].DespatchNo);
        Assert.Equal(2, documents[0].Lines.Count);

        Assert.Equal("F50.23562", documents[1].OrderDocumentNo);
        Assert.Equal("3998", documents[1].DespatchNo);
        Assert.Single(documents[1].Lines);
    }

    [Fact]
    public async Task ResolveMovementOrderNoAsync_UsesOrderNoForFirstReceipt_AndMaxPlusOneForAdditionalReceipts()
    {
        await using var mikroDbContext = CreateMikroWriteDbContext();
        mikroDbContext.STOK_HAREKETLERIs.Add(new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_evraktip = 13,
            sth_evrakno_seri = "F50",
            sth_evrakno_sira = 918670,
            sth_satirno = 0
        });
        await mikroDbContext.SaveChangesAsync();

        var service = CreateService(mikroDbContext);

        // 1. irsaliye: F50-23562 bos -> dogrudan siparis numarasi kullanilir.
        var firstOrderNo = await service.ResolveMovementOrderNoAsync("F50", 23562, CancellationToken.None);
        Assert.Equal(23562, firstOrderNo);

        mikroDbContext.STOK_HAREKETLERIs.Add(new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_evraktip = 13,
            sth_evrakno_seri = "F50",
            sth_evrakno_sira = firstOrderNo,
            sth_satirno = 0
        });
        await mikroDbContext.SaveChangesAsync();

        // 2. irsaliye: F50-23562 dolu -> F50 serisinde MAX + 1 (918671) kullanilir.
        var secondOrderNo = await service.ResolveMovementOrderNoAsync("F50", 23562, CancellationToken.None);
        Assert.Equal(918671, secondOrderNo);

        mikroDbContext.STOK_HAREKETLERIs.Add(new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_evraktip = 13,
            sth_evrakno_seri = "F50",
            sth_evrakno_sira = secondOrderNo,
            sth_satirno = 0
        });
        await mikroDbContext.SaveChangesAsync();

        // 3. irsaliye: F50 serisinde MAX + 1 (918672) kullanilir.
        var thirdOrderNo = await service.ResolveMovementOrderNoAsync("F50", 23562, CancellationToken.None);
        Assert.Equal(918672, thirdOrderNo);
    }

    [Fact]
    public async Task FindExistingReceiptAsync_RequiresMatchingDespatchOrderLineAndQuantity()
    {
        await using var mikroDbContext = CreateMikroWriteDbContext();
        var orderGuid = Guid.NewGuid();
        mikroDbContext.STOK_HAREKETLERIs.Add(new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_iptal = false,
            sth_tip = 0,
            sth_cins = 0,
            sth_normal_iade = 0,
            sth_evraktip = 13,
            sth_evrakno_seri = "F50",
            sth_evrakno_sira = 918671,
            sth_belge_no = "3998",
            sth_sip_uid = orderGuid,
            sth_miktar = 12d
        });
        await mikroDbContext.SaveChangesAsync();

        var service = CreateService(mikroDbContext);
        var matching = CreateAnalysis(orderGuid, 12d);
        var mismatching = CreateAnalysis(orderGuid, 13d);

        var completeEvidence = await service.FindExistingReceiptAsync(matching, CancellationToken.None);
        var incompleteEvidence = await service.FindExistingReceiptAsync(mismatching, CancellationToken.None);

        Assert.NotNull(completeEvidence);
        Assert.True(completeEvidence.IsComplete);
        Assert.Equal(918671, completeEvidence.DocumentOrderNo);
        Assert.NotNull(incompleteEvidence);
        Assert.False(incompleteEvidence.IsComplete);
    }

    [Fact]
    public async Task RecordFailureAsync_PreservesExistingSucceededFlowAndDeduplicatesRepeatedErrors()
    {
        await using var authDbContext = CreateAuthDbContext();
        var clock = new FixedClock(new DateTime(2026, 11, 23, 13, 0, 0, DateTimeKind.Utc));
        var flowService = new DocumentFlowService(
            authDbContext,
            clock,
            new StaticOptionsMonitor<DocumentFlowTrackingOptions>(new DocumentFlowTrackingOptions { Enabled = true }),
            NullLogger<DocumentFlowService>.Instance);
        var recorder = new AxataImportDocumentFlowRecorder(flowService);

        // 1. irsaliye basarili kaydedildi.
        await recorder.RecordAsync(
            "G01",
            DocumentFlowType.CompanyReceiving,
            50,
            null,
            "F50",
            23562,
            "F50.23562.3285",
            "Ilk irsaliye aktarildi.",
            Guid.Empty,
            CancellationToken.None,
            "3285");

        // 2. irsaliye iki kez ayni hatayi aldi.
        await recorder.RecordFailureAsync(
            "G01",
            DocumentFlowType.CompanyReceiving,
            50,
            null,
            "F50",
            23562,
            "F50.23562.3998",
            "Kalan miktar yetersiz.",
            Guid.Empty,
            CancellationToken.None,
            "3998");

        await recorder.RecordFailureAsync(
            "G01",
            DocumentFlowType.CompanyReceiving,
            50,
            null,
            "F50",
            23562,
            "F50.23562.3998",
            "Kalan miktar yetersiz.",
            Guid.Empty,
            CancellationToken.None,
            "3998");

        // Ayni parcali irsaliye sonraki denemede farkli Mikro sira no ile basarili oldu.
        await recorder.RecordAsync(
            "G01",
            DocumentFlowType.CompanyReceiving,
            50,
            null,
            "F50",
            918671,
            "F50.23562.3998",
            "Ikinci irsaliye aktarildi. Mikro evraki: F50/918671.",
            Guid.Empty,
            CancellationToken.None,
            "3998",
            23562);

        authDbContext.ChangeTracker.Clear();
        var flows = await authDbContext.DocumentFlows
            .Include(flow => flow.Events)
            .OrderBy(flow => flow.FlowKey)
            .ToListAsync();

        Assert.Equal(2, flows.Count);

        var succeededFlow = Assert.Single(flows, flow => flow.FlowKey == "CompanyReceiving:50:F50:23562:3285");
        Assert.Equal(DocumentFlowStatus.Succeeded, succeededFlow.Status);
        Assert.Equal("3285", succeededFlow.DocumentNo);

        var failedFlow = Assert.Single(flows, flow => flow.FlowKey == "CompanyReceiving:50:F50:23562:3998");
        Assert.Equal(DocumentFlowStatus.Succeeded, failedFlow.Status);
        Assert.Equal("3998", failedFlow.DocumentNo);
        Assert.Null(failedFlow.LastError);
        Assert.Equal(2, failedFlow.Events.Count);
    }

    private static AxataG01InboundAtfImportService CreateService(MikroWriteDbContext mikroDbContext) =>
        new(
            new StaticOptionsMonitor<AxataSynchronizationOptions>(new AxataSynchronizationOptions()),
            mikroDbContext,
            new UnusedCreateCompanyReceivingUseCase(),
            new StaticOptionsMonitor<MikroWriteRoutingOptions>(new MikroWriteRoutingOptions()),
            new AxataImportDocumentFlowRecorder(new NoOpDocumentFlowService()),
            NullLogger<AxataG01InboundAtfImportService>.Instance);

    private static G01InboundAtfAnalysis CreateAnalysis(Guid orderGuid, double quantity)
    {
        var line = new G01InboundAtfLine(
            141591,
            "F50.23562",
            6,
            "024360",
            quantity,
            "32000441",
            "3998",
            "0",
            new DateTime(2026, 11, 19));
        var order = new SIPARISLER
        {
            sip_Guid = orderGuid,
            sip_stok_kod = line.StockCode,
            sip_miktar = quantity,
            sip_teslim_miktar = 0d
        };
        var document = new G01InboundAtfDocument(line.OrderDocumentNo, line.DespatchNo, [line]);
        var dto = new AxataG01InboundAtfDocumentDto(
            line.OrderDocumentNo,
            "F50",
            23562,
            line.CustomerCode,
            line.DespatchNo,
            50,
            1,
            quantity,
            1,
            quantity,
            0d,
            0,
            true,
            null);

        return new G01InboundAtfAnalysis(
            document,
            [line],
            [new MatchedG01InboundAtfLine(line, order)],
            dto);
    }

    private static MikroWriteDbContext CreateMikroWriteDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroWriteDbContext>()
            .UseInMemoryDatabase($"axata-g01-mikro-{Guid.NewGuid():N}")
            .Options;
        return new MikroWriteDbContext(options);
    }

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"axata-g01-auth-{Guid.NewGuid():N}")
            .Options;
        return new AuthDbContext(options);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable OnChange(Action<T, string?> listener) => EmptyDisposable.Instance;

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class UnusedCreateCompanyReceivingUseCase : ICreateCompanyReceivingUseCase
    {
        public Task<CreateCompanyReceivingResponse> ExecuteAsync(
            CreateCompanyReceivingRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpDocumentFlowService : IDocumentFlowService
    {
        public Task<DocumentFlowListResponse> ListAsync(DocumentFlowListRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DocumentFlowDetailDto> GetAsync(Guid id, int? warehouseNo, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordAsync(RecordDocumentFlowRequest request, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RecordManyAsync(IReadOnlyCollection<RecordDocumentFlowRequest> requests, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
