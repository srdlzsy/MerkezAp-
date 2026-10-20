using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class SafeWriteReviewServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Recheck_CompletesOnlyWhenRecoveryMarksTheRecordCompleted()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();

        var dispatcher = new FakeDispatcher(async (_, _, _, _, cancellationToken) =>
        {
            var tracked = await db.MobileOfflineSyncRequests.SingleAsync(cancellationToken);
            tracked.MarkCompleted("{\"documentSerie\":\"F56\",\"documentOrderNo\":123}", Now);
            await db.SaveChangesAsync(cancellationToken);
            return new SafeWriteRecoveryResult("Completed", new { documentSerie = "F56", documentOrderNo = 123 });
        });
        var service = new SafeWriteReviewService(db, new FixedClock(Now), dispatcher);

        var result = await service.RecheckAsync(
            record.Id,
            new SafeWriteReviewRequest("Mikro belgesi duzeltildi ve tekrar kontrol edildi."),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("Completed", result.Outcome);
        Assert.Equal("Completed", result.SafeWrite.Status);
        Assert.Single(db.SafeWriteReviewAudits);
    }

    [Fact]
    public async Task RecheckOwned_CompletesOnlyTheCurrentUsersRecordInTheCurrentWarehouse()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();

        var dispatcher = new FakeDispatcher(async (operationCode, warehouseNo, userId, clientRequestId, cancellationToken) =>
        {
            Assert.Equal(record.OperationCode, operationCode);
            Assert.Equal(record.WarehouseNo, warehouseNo);
            Assert.Equal(record.RequestedByUserId, userId);
            Assert.Equal(Guid.Parse(record.ClientRequestId), clientRequestId);

            var tracked = await db.MobileOfflineSyncRequests.SingleAsync(cancellationToken);
            tracked.MarkCompleted("{\"documentSerie\":\"F56\",\"documentOrderNo\":123}", Now);
            await db.SaveChangesAsync(cancellationToken);
            return new SafeWriteRecoveryResult("Completed", new { documentSerie = "F56", documentOrderNo = 123 });
        });
        var service = new SafeWriteReviewService(db, new FixedClock(Now), dispatcher);

        var result = await service.RecheckOwnedAsync(
            record.OperationCode,
            record.WarehouseNo,
            record.RequestedByUserId,
            Guid.Parse(record.ClientRequestId),
            CancellationToken.None);

        Assert.Equal("Completed", result.Outcome);
        Assert.Equal("Completed", result.SafeWrite.Status);
        var audit = Assert.Single(db.SafeWriteReviewAudits);
        Assert.Equal("OwnerRecheck", audit.Action);
        Assert.Equal(record.RequestedByUserId, audit.ReviewedByUserId);
    }

    [Fact]
    public async Task RecheckOwned_DoesNotExposeAnotherUsersRecord()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();

        var service = new SafeWriteReviewService(
            db,
            new FixedClock(Now),
            new FakeDispatcher((_, _, _, _, _) => throw new InvalidOperationException("Should not run.")));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.RecheckOwnedAsync(
            record.OperationCode,
            record.WarehouseNo,
            Guid.NewGuid(),
            Guid.Parse(record.ClientRequestId),
            CancellationToken.None));
    }

    [Fact]
    public async Task RecheckOwned_MissingDocumentStopsTheUserRecheckLoop()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();
        var service = new SafeWriteReviewService(
            db,
            new FixedClock(Now),
            new FakeDispatcher((_, _, _, _, _) =>
                Task.FromResult(new SafeWriteRecoveryResult("Failed", null))));

        var result = await service.RecheckOwnedAsync(
            record.OperationCode,
            record.WarehouseNo,
            record.RequestedByUserId,
            Guid.Parse(record.ClientRequestId),
            CancellationToken.None);

        Assert.Equal("NotFound", result.Outcome);
        Assert.Equal("Missing", result.RecoveryState);
        Assert.Equal("NeedsReview", result.RecommendedAction);
        Assert.False(result.SafeWrite.Retryable);
    }

    [Fact]
    public async Task AllowRetry_OpensSameRequestOnlyWhenReadbackFindsNoDocument()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();
        var service = new SafeWriteReviewService(
            db,
            new FixedClock(Now),
            new FakeDispatcher((_, _, _, _, _) =>
                Task.FromResult(new SafeWriteRecoveryResult("Failed", null))));

        var result = await service.AllowRetryAsync(
            record.Id,
            new SafeWriteReviewRequest("Mikro tarafinda belge bulunmadigi yetkili tarafindan kontrol edildi."),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("RetryAllowed", result.Outcome);
        Assert.Equal("Failed", result.SafeWrite.Status);
        Assert.True(result.SafeWrite.Retryable);
        Assert.Equal(OperationConflictErrorCodes.SafeWriteRetryAllowed, result.SafeWrite.ErrorCode);
        Assert.Equal("Missing", result.RecoveryState);
        Assert.Equal("RetrySameRequest", result.RecommendedAction);
    }

    [Fact]
    public async Task AllowRetry_DoesNotOpenRequestWhenReadbackFails()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();

        var dispatcher = new FakeDispatcher(async (_, _, _, _, cancellationToken) =>
        {
            var tracked = await db.MobileOfflineSyncRequests.SingleAsync(cancellationToken);
            tracked.MarkFailed(
                "SQL connection failed.",
                Now,
                OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                retryable: true);
            await db.SaveChangesAsync(cancellationToken);
            return new SafeWriteRecoveryResult("Failed", null);
        });
        var service = new SafeWriteReviewService(db, new FixedClock(Now), dispatcher);

        var result = await service.AllowRetryAsync(
            record.Id,
            new SafeWriteReviewRequest("Gecici hata sonrasi tekrar acma talebi kontrol edildi."),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("StillNeedsReview", result.Outcome);
        Assert.False(result.SafeWrite.Retryable);
        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, result.SafeWrite.ErrorCode);
    }

    [Fact]
    public async Task Recheck_IncompleteDocumentWaitsWithoutAllowingAnotherCreate()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();
        var service = new SafeWriteReviewService(
            db,
            new FixedClock(Now),
            new FakeDispatcher((_, _, _, _, _) => throw new OperationConflictException(
                OperationConflictErrorCodes.MikroDocumentIncomplete,
                OperationConflictMessages.MikroDocumentIncomplete,
                retryable: true)));

        var result = await service.RecheckOwnedAsync(
            record.OperationCode,
            record.WarehouseNo,
            record.RequestedByUserId,
            Guid.Parse(record.ClientRequestId),
            CancellationToken.None);

        Assert.Equal("Incomplete", result.Outcome);
        Assert.Equal("Incomplete", result.RecoveryState);
        Assert.Equal("WaitAndCheck", result.RecommendedAction);
        Assert.False(result.SafeWrite.Retryable);
        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, result.SafeWrite.ErrorCode);
    }

    [Fact]
    public async Task Cancel_ReturnsCancelledStatusAndKeepsAudit()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();
        var service = new SafeWriteReviewService(
            db,
            new FixedClock(Now),
            new FakeDispatcher((_, _, _, _, _) => throw new InvalidOperationException("Should not run.")));

        var result = await service.CancelAsync(
            record.Id,
            new SafeWriteReviewRequest("Hatali terminal taslagi yetkili karariyla iptal edildi."),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Equal("Cancelled", result.Outcome);
        Assert.Equal("Cancelled", result.SafeWrite.Status);
        Assert.Equal(OperationConflictErrorCodes.SafeWriteCancelled, result.SafeWrite.ErrorCode);
        Assert.Single(await service.GetHistoryAsync(record.Id, CancellationToken.None));

        var recoveryWasCalled = false;
        var offlineSyncService = new MobileOfflineSyncService(db, new FixedClock(Now));
        var terminalStatus = await offlineSyncService.GetStatusAsync<object>(
            record.OperationCode,
            record.RequestedByUserId,
            Guid.Parse(record.ClientRequestId),
            (_, _) =>
            {
                recoveryWasCalled = true;
                return Task.FromResult<object?>(null);
            },
            CancellationToken.None);

        Assert.Equal("Cancelled", terminalStatus.Status);
        Assert.Equal(OperationConflictErrorCodes.SafeWriteCancelled, terminalStatus.ErrorCode);
        Assert.False(terminalStatus.Retryable);
        Assert.False(recoveryWasCalled);
    }

    [Fact]
    public async Task CancelOwnedRetryAllowed_ClosesOnlyTheOwnersDefinitivelyMissingRecord()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        record.AllowRetryAfterManualReview(
            OperationConflictErrorCodes.SafeWriteRetryAllowed,
            OperationConflictMessages.SafeWriteRetryAllowed,
            Now.AddMinutes(-1));
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();
        var service = new SafeWriteReviewService(
            db,
            new FixedClock(Now),
            new FakeDispatcher((_, _, _, _, _) => throw new InvalidOperationException("Should not run.")));

        var result = await service.CancelOwnedRetryAllowedAsync(
            record.WarehouseNo,
            record.RequestedByUserId,
            Guid.Parse(record.ClientRequestId),
            CancellationToken.None);

        Assert.Equal("Cancelled", result.Outcome);
        Assert.Equal("Cancelled", result.SafeWrite.Status);
        Assert.Equal(OperationConflictErrorCodes.SafeWriteCancelled, result.SafeWrite.ErrorCode);
        Assert.Equal("ClearCancelled", result.RecommendedAction);
        var audit = Assert.Single(db.SafeWriteReviewAudits);
        Assert.Equal("OwnerRetryExhausted", audit.Action);
    }

    [Fact]
    public async Task CancelOwnedRetryAllowed_RejectsUncertainRecord()
    {
        await using var db = CreateDb();
        var record = CreateReviewRecord();
        record.RestoreManualReview(
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            OperationConflictMessages.MikroWriteOutcomeUnconfirmed,
            Now.AddMinutes(-1));
        db.MobileOfflineSyncRequests.Add(record);
        await db.SaveChangesAsync();
        var service = new SafeWriteReviewService(
            db,
            new FixedClock(Now),
            new FakeDispatcher((_, _, _, _, _) => throw new InvalidOperationException("Should not run.")));

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            service.CancelOwnedRetryAllowedAsync(
                record.WarehouseNo,
                record.RequestedByUserId,
                Guid.Parse(record.ClientRequestId),
                CancellationToken.None));

        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, exception.ErrorCode);
        Assert.NotEqual(MobileOfflineSyncRequestStatus.Cancelled, record.Status);
        Assert.Empty(db.SafeWriteReviewAudits);
    }

    private static MobileOfflineSyncRequest CreateReviewRecord()
    {
        var record = new MobileOfflineSyncRequest(
            Guid.NewGuid(),
            "mal-kabul-islemleri.firma-mal-kabulleri.create",
            Guid.NewGuid(),
            56,
            Guid.NewGuid().ToString("D"),
            new string('A', 64),
            "{}",
            Now.AddMinutes(-10));
        record.MarkFailed(
            OperationConflictMessages.MikroDocumentContentMismatch,
            Now.AddMinutes(-9),
            OperationConflictErrorCodes.MikroDocumentContentMismatch,
            retryable: false);
        return record;
    }

    private static AuthDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuthDbContext(options);
    }

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
}
