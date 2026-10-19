using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.OfflineSync;

public sealed class OfflineCreateGuardTests
{
    [Fact]
    public async Task ExecuteAsync_ContinuesWithBoundedServerTokenAfterClientDisconnects()
    {
        await using var db = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var service = new MobileOfflineSyncService(db, new TestClock());
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestCreateRequest(56, clientRequestId, userId);
        using var disconnectedClient = new CancellationTokenSource();
        disconnectedClient.Cancel();

        var result = await OfflineCreateGuard.ExecuteAsync(
            service,
            "test.create",
            userId,
            request.WarehouseNo,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>(null),
            executionToken =>
            {
                Assert.True(executionToken.CanBeCanceled);
                Assert.False(executionToken.IsCancellationRequested);
                return Task.FromResult("F56/15");
            },
            disconnectedClient.Token,
            preventReexecutionAfterUncertainOutcome: true);

        Assert.Equal("F56/15", result);
        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Completed, record.Status);
    }

    private sealed record TestCreateRequest(
        int WarehouseNo,
        Guid ClientRequestId,
        Guid RequestedByUserId);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    }
}
