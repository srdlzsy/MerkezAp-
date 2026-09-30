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
