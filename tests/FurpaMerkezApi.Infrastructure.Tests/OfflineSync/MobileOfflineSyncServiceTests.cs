using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Common.Errors;
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

    [Fact]
    public async Task Acquire_ClassifiesSameClientRequestIdWithDifferentPayloadAsNonRetryable()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        await AcquireAsync(service, userId, clientRequestId, new TestRequest(56, 25));

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            AcquireAsync(service, userId, clientRequestId, new TestRequest(56, 26)));

        Assert.Equal(OperationConflictErrorCodes.ClientRequestPayloadMismatch, exception.ErrorCode);
        Assert.False(exception.Retryable);
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

    [Fact]
    public async Task NonRetryableFailure_SurvivesNewContextAndSkipsRecovery()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AuthDbContext(options);
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        var payload = new TestRequest(56, 2);
        var writes = 0;
        await Assert.ThrowsAsync<OperationConflictException>(() => OfflineCreateGuard.ExecuteAsync<TestRequest, string>(
            service, "shipment.create", user, 56, id, payload,
            (_, _) => Task.FromResult<string?>(null),
            _ => { writes++; throw new OperationConflictException(OperationConflictErrorCodes.MikroDocumentContentMismatch, "Manual review", false); },
            CancellationToken.None, true));
        await using var retryDb = new AuthDbContext(options);
        var retryService = new MobileOfflineSyncService(retryDb, new MutableClock(Now));
        var recoveryCalled = false;
        var exception = await Assert.ThrowsAsync<OperationConflictException>(() => retryService.AcquireAsync<TestRequest, string>(
            "shipment.create", user, 56, id, payload,
            (_, _) => { recoveryCalled = true; return Task.FromResult<string?>("unexpected"); }, CancellationToken.None, true));
        Assert.False(exception.Retryable);
        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, exception.ErrorCode);
        Assert.False(recoveryCalled);
        Assert.Equal(1, writes);
        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.False(record.Retryable);
        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, record.ErrorCode);
    }

    [Fact]
    public async Task MismatchDuringRecovery_IsPersistedAndNotExecutedAgain()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        var payload = new TestRequest(56, 2);
        await AcquireAsync(service, user, id, payload);
        await service.MarkFailedAsync("shipment.create", user, id, "TimeOut", CancellationToken.None);
        await Assert.ThrowsAsync<OperationConflictException>(() => service.AcquireAsync<TestRequest, string>(
            "shipment.create", user, 56, id, payload,
            (_, _) => throw new OperationConflictException(OperationConflictErrorCodes.MikroDocumentContentMismatch, "Manual review", false),
            CancellationToken.None, true));
        await Assert.ThrowsAsync<OperationConflictException>(() => AcquireAsync(service, user, id, payload));
        Assert.False((await db.MobileOfflineSyncRequests.SingleAsync()).Retryable);
    }

    [Fact]
    public async Task ConcurrentFailedRetries_OnlyOneAcquiresExecution()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await VerifyConcurrentRetryAsync(options);
    }

    internal static async Task VerifyConcurrentRetryAsync(DbContextOptions<AuthDbContext> options)
    {
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        var payload = new TestRequest(56, 2);
        await using (var seed = new AuthDbContext(options))
        {
            var service = new MobileOfflineSyncService(seed, new MutableClock(Now));
            await AcquireAsync(service, user, id, payload);
            await service.MarkFailedAsync("shipment.create", user, id, "Definite validation rejection", CancellationToken.None);
        }
        await using var firstDb = new AuthDbContext(options);
        await using var secondDb = new AuthDbContext(options);
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async Task<string?> Recover(string? _, CancellationToken token)
        {
            if (Interlocked.Increment(ref arrived) == 2) barrier.SetResult();
            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(20), token);
            return null;
        }
        Task<MobileOfflineSyncAcquireResult<string>> Acquire(AuthDbContext context) =>
            new MobileOfflineSyncService(context, new MutableClock(Now)).AcquireAsync<TestRequest, string>(
                "shipment.create", user, 56, id, payload, Recover, CancellationToken.None, true);
        var results = await Task.WhenAll(Acquire(firstDb), Acquire(secondDb));
        Assert.Single(results, result => result.State == MobileOfflineSyncAcquireState.Proceed);
        Assert.Single(results, result => result.State == MobileOfflineSyncAcquireState.Processing);
    }

    [Fact]
    public async Task StaleFailure_CannotOverwriteCompletedResult()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var firstDb = new AuthDbContext(options);
        await using var secondDb = new AuthDbContext(options);
        var first = new MobileOfflineSyncService(firstDb, new MutableClock(Now));
        var second = new MobileOfflineSyncService(secondDb, new MutableClock(Now));
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        await AcquireAsync(first, user, id, new TestRequest(56, 2));
        await secondDb.MobileOfflineSyncRequests.SingleAsync();
        await first.CompleteAsync("shipment.create", user, id, "F56/123", CancellationToken.None);
        await second.MarkFailedAsync("shipment.create", user, id, "stale failure", CancellationToken.None);
        Assert.Equal(MobileOfflineSyncRequestStatus.Completed, (await secondDb.MobileOfflineSyncRequests.SingleAsync()).Status);
    }

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
