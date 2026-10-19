using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class SafeWriteReconciliationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReconcileAsync_RecoversOnlyExpiredProcessingRecords()
    {
        await using var db = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var stale = Request(Now.AddMinutes(-20));
        var recent = Request(Now.AddMinutes(-2));
        db.MobileOfflineSyncRequests.AddRange(stale, recent);
        await db.SaveChangesAsync();

        var dispatcher = new FakeDispatcher(async (_, _, _, clientRequestId, cancellationToken) =>
        {
            var tracked = await db.MobileOfflineSyncRequests.SingleAsync(
                item => item.ClientRequestId == clientRequestId.ToString("D"),
                cancellationToken);
            tracked.MarkCompleted("{}", Now);
            await db.SaveChangesAsync(cancellationToken);
            return new SafeWriteRecoveryResult("Completed", new { });
        });
        var service = new SafeWriteReconciliationService(
            db,
            dispatcher,
            new FixedClock(Now),
            new StaticOptionsMonitor<DatabaseMonitoringOptions>(new DatabaseMonitoringOptions
            {
                Enabled = true,
                SafeWriteReconciliationEnabled = true,
                SafeWriteReconciliationBatchSize = 10
            }),
            NullLogger<SafeWriteReconciliationService>.Instance);

        var processed = await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(MobileOfflineSyncRequestStatus.Completed,
            (await db.MobileOfflineSyncRequests.FindAsync(stale.Id))!.Status);
        Assert.Equal(MobileOfflineSyncRequestStatus.Processing,
            (await db.MobileOfflineSyncRequests.FindAsync(recent.Id))!.Status);
    }

    [Fact]
    public async Task ReconcileAsync_AllowsSameRequestRetryWhenReadbackConfirmsNoDocument()
    {
        await using var db = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var record = Request(Now.AddMinutes(-20));
        record.MarkFailed(
            "Write outcome could not be confirmed.",
            Now.AddMinutes(-10),
            FurpaMerkezApi.Application.Common.Errors.OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            retryable: true);
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();

        var service = new SafeWriteReconciliationService(
            db,
            new FakeDispatcher((_, _, _, _, _) =>
                Task.FromResult(new SafeWriteRecoveryResult("Failed", null))),
            new FixedClock(Now),
            new StaticOptionsMonitor<DatabaseMonitoringOptions>(new DatabaseMonitoringOptions
            {
                Enabled = true,
                SafeWriteReconciliationEnabled = true,
                SafeWriteReconciliationIntervalSeconds = 60,
                SafeWriteReconciliationBatchSize = 10
            }),
            NullLogger<SafeWriteReconciliationService>.Instance);

        var processed = await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        var updated = await db.MobileOfflineSyncRequests.FindAsync(record.Id);
        Assert.Equal(MobileOfflineSyncRequestStatus.Failed, updated!.Status);
        Assert.True(updated.Retryable);
        Assert.Equal(
            FurpaMerkezApi.Application.Common.Errors.OperationConflictErrorCodes.SafeWriteRetryAllowed,
            updated.ErrorCode);
    }

    private static MobileOfflineSyncRequest Request(DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            "mal-kabul-islemleri.firma-mal-kabulleri.create",
            Guid.NewGuid(),
            56,
            Guid.NewGuid().ToString("D"),
            new string('a', 64),
            "{}",
            createdAtUtc);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeDispatcher(
        Func<string, int, Guid, Guid, CancellationToken, Task<SafeWriteRecoveryResult>> handler)
        : ISafeWriteRecoveryDispatcher
    {
        public Task<SafeWriteRecoveryResult> RecoverAsync(
            string operationCode,
            int warehouseNo,
            Guid requestedByUserId,
            Guid clientRequestId,
            CancellationToken cancellationToken) =>
            handler(operationCode, warehouseNo, requestedByUserId, clientRequestId, cancellationToken);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
