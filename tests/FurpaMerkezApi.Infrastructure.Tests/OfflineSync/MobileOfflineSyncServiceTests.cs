using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.OfflineSync;

public sealed class MobileOfflineSyncServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 30, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Acquire_DoesNotReexecuteUncertainFailedWrite()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);

        var first = await AcquireAsync(service, userId, clientRequestId, request);
        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, first.State);

        await service.MarkFailedAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "MikroAPI - TimeOut",
            CancellationToken.None);

        var retry = await AcquireAsync(service, userId, clientRequestId, request);

        Assert.Equal(MobileOfflineSyncAcquireState.Processing, retry.State);
        Assert.Equal(
            MobileOfflineSyncRequestStatus.Failed,
            (await db.MobileOfflineSyncRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Acquire_DoesNotReexecuteExpiredProcessingWrite()
    {
        await using var db = CreateDbContext();
        var clock = new MutableClock(Now);
        var service = new MobileOfflineSyncService(db, clock);
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);

        var first = await AcquireAsync(service, userId, clientRequestId, request);
        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, first.State);

        clock.UtcNow = Now.AddMinutes(30);
        var retry = await AcquireAsync(service, userId, clientRequestId, request);

        Assert.Equal(MobileOfflineSyncAcquireState.Processing, retry.State);
    }

    [Fact]
    public async Task Acquire_RecoversCompletedWriteWithoutExecutingAgain()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);

        await AcquireAsync(service, userId, clientRequestId, request);
        await service.MarkFailedAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "MikroAPI - TimeOut",
            CancellationToken.None);

        var retry = await service.AcquireAsync<TestRequest, string>(
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>("F56/88049"),
            CancellationToken.None,
            preventReexecutionAfterUncertainOutcome: true);

        Assert.Equal(MobileOfflineSyncAcquireState.Completed, retry.State);
        Assert.Equal("F56/88049", retry.Response);
        Assert.Equal(
            MobileOfflineSyncRequestStatus.Completed,
            (await db.MobileOfflineSyncRequests.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("MikroAPI - TimeOut")]
    [InlineData("The request timed out.")]
    [InlineData("Write outcome could not be confirmed because the request was canceled.")]
    public void IsUncertainWriteOutcome_RecognizesAmbiguousFailures(string errorMessage)
    {
        Assert.True(MobileOfflineSyncService.IsUncertainWriteOutcome(errorMessage));
        Assert.False(MobileOfflineSyncService.IsUncertainWriteOutcome("Stock code is invalid."));
    }

    private static Task<MobileOfflineSyncAcquireResult<string>> AcquireAsync(
        MobileOfflineSyncService service,
        Guid userId,
        Guid clientRequestId,
        TestRequest request) =>
        service.AcquireAsync<TestRequest, string>(
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            preventReexecutionAfterUncertainOutcome: true);

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"mobile-offline-sync-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

    private sealed record TestRequest(int WarehouseNo, int LineCount);

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
