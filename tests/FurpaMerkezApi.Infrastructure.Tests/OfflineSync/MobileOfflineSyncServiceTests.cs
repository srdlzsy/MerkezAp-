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
    public async Task Acquire_ValidatesNewRequestBeforeCreatingSafeWriteReservation()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            service.AcquireAsync<RequestWithIdentity, string>(
                "receiving.create",
                userId,
                56,
                clientRequestId,
                new RequestWithIdentity(56, 25, clientRequestId, userId),
                (_, _) => Task.FromResult<string?>(null),
                CancellationToken.None,
                validateBeforeReservation: _ => throw new OperationConflictException(
                    OperationConflictErrorCodes.MikroDocumentIdentityConflict,
                    OperationConflictMessages.MikroDocumentIdentityConflict,
                    retryable: false)));

        Assert.Equal(OperationConflictErrorCodes.MikroDocumentIdentityConflict, exception.ErrorCode);
        Assert.Empty(await db.MobileOfflineSyncRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Acquire_DoesNotRunNewRequestValidationWhenExistingResultCanBeReplayed()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new RequestWithIdentity(56, 25, clientRequestId, userId);

        await service.AcquireAsync<RequestWithIdentity, string>(
            "receiving.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None);
        await service.CompleteAsync(
            "receiving.create",
            userId,
            clientRequestId,
            "F56/123",
            CancellationToken.None);

        var validationCalled = false;
        var result = await service.AcquireAsync<RequestWithIdentity, string>(
            "receiving.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            validateBeforeReservation: _ =>
            {
                validationCalled = true;
                return Task.CompletedTask;
            });

        Assert.Equal(MobileOfflineSyncAcquireState.Completed, result.State);
        Assert.False(validationCalled);
        Assert.Single(await db.MobileOfflineSyncRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Acquire_BlocksSameLogicalPayloadWithDifferentClientRequestIdWhileFirstIsUnresolved()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var firstClientRequestId = Guid.NewGuid();
        var secondClientRequestId = Guid.NewGuid();
        var firstPayload = new RequestWithIdentity(56, 25, firstClientRequestId, userId);
        var secondPayload = firstPayload with { ClientRequestId = secondClientRequestId };

        await service.AcquireAsync<RequestWithIdentity, string>(
            "shipment.create",
            userId,
            56,
            firstClientRequestId,
            firstPayload,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            preventReexecutionAfterUncertainOutcome: true);

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            service.AcquireAsync<RequestWithIdentity, string>(
                "shipment.create",
                userId,
                56,
                secondClientRequestId,
                secondPayload,
                (_, _) => Task.FromResult<string?>(null),
                CancellationToken.None,
                preventReexecutionAfterUncertainOutcome: true));

        Assert.Equal(OperationConflictErrorCodes.MikroWriteInProgress, exception.ErrorCode);
        Assert.True(exception.Retryable);
        Assert.Contains(firstClientRequestId.ToString("D"), exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(await db.MobileOfflineSyncRequests.ToArrayAsync());
    }

    [Fact]
    public async Task Acquire_AllowsSameLogicalPayloadWithNewClientRequestIdAfterFirstCompleted()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var firstClientRequestId = Guid.NewGuid();
        var secondClientRequestId = Guid.NewGuid();

        await service.AcquireAsync<RequestWithIdentity, string>(
            "shipment.create",
            userId,
            56,
            firstClientRequestId,
            new RequestWithIdentity(56, 25, firstClientRequestId, userId),
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None);
        await service.CompleteAsync(
            "shipment.create",
            userId,
            firstClientRequestId,
            "F56/123",
            CancellationToken.None);

        var result = await service.AcquireAsync<RequestWithIdentity, string>(
            "shipment.create",
            userId,
            56,
            secondClientRequestId,
            new RequestWithIdentity(56, 25, secondClientRequestId, userId),
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None);

        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, result.State);
        Assert.Equal(2, await db.MobileOfflineSyncRequests.CountAsync());
    }

    [Fact]
    public async Task Acquire_ReplaysRecentCompletedLogicalDuplicateUnderNewClientRequestId()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var firstClientRequestId = Guid.NewGuid();
        var secondClientRequestId = Guid.NewGuid();

        await service.AcquireAsync<RequestWithIdentity, string>(
            "shipment.create",
            userId,
            56,
            firstClientRequestId,
            new RequestWithIdentity(56, 25, firstClientRequestId, userId),
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            recentCompletedDuplicateWindow: TimeSpan.FromHours(6));
        await service.CompleteAsync(
            "shipment.create",
            userId,
            firstClientRequestId,
            "F56/123",
            CancellationToken.None);

        var result = await service.AcquireAsync<RequestWithIdentity, string>(
            "shipment.create",
            userId,
            56,
            secondClientRequestId,
            new RequestWithIdentity(56, 25, secondClientRequestId, userId),
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            recentCompletedDuplicateWindow: TimeSpan.FromHours(6));

        Assert.Equal(MobileOfflineSyncAcquireState.Completed, result.State);
        Assert.Equal("F56/123", result.Response);
        var records = await db.MobileOfflineSyncRequests.OrderBy(item => item.CreatedAtUtc).ToArrayAsync();
        Assert.Equal(2, records.Length);
        Assert.All(records, record => Assert.Equal(MobileOfflineSyncRequestStatus.Completed, record.Status));
    }

    [Fact]
    public async Task Acquire_ForceCreateNew_BypassesRecentCompletedDuplicateReplay()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var firstClientRequestId = Guid.NewGuid();
        var secondClientRequestId = Guid.NewGuid();

        await service.AcquireAsync<RequestWithIdentity, string>(
            "shipment.create",
            userId,
            56,
            firstClientRequestId,
            new RequestWithIdentity(56, 25, firstClientRequestId, userId),
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            recentCompletedDuplicateWindow: TimeSpan.FromHours(6));
        await service.CompleteAsync(
            "shipment.create",
            userId,
            firstClientRequestId,
            "F56/123",
            CancellationToken.None);

        var result = await service.AcquireAsync<RequestWithIdentity, string>(
            "shipment.create",
            userId,
            56,
            secondClientRequestId,
            new RequestWithIdentity(56, 25, secondClientRequestId, userId),
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            recentCompletedDuplicateWindow: TimeSpan.FromHours(6),
            forceCreateNew: true);

        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, result.State);
        Assert.Equal(2, await db.MobileOfflineSyncRequests.CountAsync());
    }

    [Fact]
    public async Task GetStatus_ExposesClassifiedRetryMetadata()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        await AcquireAsync(service, userId, clientRequestId, new TestRequest(56, 25));
        await service.MarkFailedAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "Write outcome is still being verified.",
            CancellationToken.None,
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            retryable: true);

        var status = await service.GetStatusAsync<string>(
            "shipment.create",
            userId,
            clientRequestId,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None);

        Assert.Equal("Failed", status.Status);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, status.ErrorCode);
        Assert.True(status.Retryable);
        Assert.Equal(5, status.RetryAfterSeconds);
        Assert.Equal(OperationConflictMessages.MikroWriteOutcomeUnconfirmed, status.ErrorMessage);
        Assert.Null(status.Result);
    }

    [Fact]
    public async Task GetStatus_ClassifiesLegacyCanceledFailureAsRetryable()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        await AcquireAsync(service, userId, clientRequestId, new TestRequest(56, 25));
        await service.MarkFailedAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "A task was canceled.",
            CancellationToken.None);

        var status = await service.GetStatusAsync<string>(
            "shipment.create",
            userId,
            clientRequestId,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None);

        Assert.Equal("Failed", status.Status);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, status.ErrorCode);
        Assert.True(status.Retryable);
        Assert.Equal(5, status.RetryAfterSeconds);
        Assert.Equal(OfflineWriteFailureClassifier.UnconfirmedMessage, status.ErrorMessage);

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, record.ErrorCode);
        Assert.True(record.Retryable);
        Assert.Equal(OfflineWriteFailureClassifier.UnconfirmedMessage, record.ErrorMessage);
    }

    [Fact]
    public async Task GetStatus_CompletedResultHasNoRetryMetadata()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        await AcquireAsync(service, userId, clientRequestId, new TestRequest(56, 25));
        await service.CompleteAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "F56/88049",
            CancellationToken.None);

        var status = await service.GetStatusAsync<string>(
            "shipment.create",
            userId,
            clientRequestId,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None);

        Assert.Equal("Completed", status.Status);
        Assert.Equal("F56/88049", status.Result);
        Assert.Null(status.ErrorCode);
        Assert.Null(status.Retryable);
        Assert.Null(status.RetryAfterSeconds);
    }

    [Fact]
    public async Task Acquire_CompletedResultCanBeVerifiedBeforeReplay()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);
        var recoveryCalls = 0;

        await AcquireAsync(service, userId, clientRequestId, request);
        await service.CompleteAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "stale-response",
            CancellationToken.None);

        var result = await service.AcquireAsync<TestRequest, string>(
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) =>
            {
                recoveryCalls++;
                return Task.FromResult<string?>("F56/88049");
            },
            CancellationToken.None,
            preventReexecutionAfterUncertainOutcome: true,
            verifyCompletedWithRecovery: true);

        Assert.Equal(MobileOfflineSyncAcquireState.Completed, result.State);
        Assert.Equal("F56/88049", result.Response);
        Assert.Equal(1, recoveryCalls);
        Assert.Equal(
            MobileOfflineSyncRequestStatus.Completed,
            (await db.MobileOfflineSyncRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task Acquire_UnverifiableCompletedResultIsInvalidatedAndNotReplayed()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);

        await AcquireAsync(service, userId, clientRequestId, request);
        await service.CompleteAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "F56/4313",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            service.AcquireAsync<TestRequest, string>(
                "shipment.create",
                userId,
                56,
                clientRequestId,
                request,
                (_, _) => Task.FromResult<string?>(null),
                CancellationToken.None,
                preventReexecutionAfterUncertainOutcome: true,
                verifyCompletedWithRecovery: true));

        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, exception.ErrorCode);
        Assert.False(exception.Retryable);

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Failed, record.Status);
        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, record.ErrorCode);
        Assert.False(record.Retryable);
        Assert.Null(record.ResponsePayload);

        await Assert.ThrowsAsync<OperationConflictException>(() =>
            service.AcquireAsync<TestRequest, string>(
                "shipment.create",
                userId,
                56,
                clientRequestId,
                request,
                (_, _) => Task.FromResult<string?>("must-not-be-used"),
                CancellationToken.None,
                preventReexecutionAfterUncertainOutcome: true,
                verifyCompletedWithRecovery: true));
    }

    [Fact]
    public async Task GetStatus_UnverifiableCompletedResultIsInvalidated()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        await AcquireAsync(service, userId, clientRequestId, new TestRequest(56, 25));
        await service.CompleteAsync(
            "shipment.create",
            userId,
            clientRequestId,
            "F56/4313",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            service.GetStatusAsync<string>(
                "shipment.create",
                userId,
                clientRequestId,
                (_, _) => Task.FromResult<string?>(null),
                CancellationToken.None,
                verifyCompletedWithRecovery: true));

        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, exception.ErrorCode);
        Assert.False(exception.Retryable);

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Failed, record.Status);
        Assert.False(record.Retryable);
        Assert.Null(record.ResponsePayload);
    }

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
    public async Task Acquire_ReexecutesUncertainFailureWhenPreventionIsDisabled()
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
            "A task was canceled.",
            CancellationToken.None);

        var retry = await service.AcquireAsync<TestRequest, string>(
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>(null),
            CancellationToken.None,
            preventReexecutionAfterUncertainOutcome: false);

        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, retry.State);

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Processing, record.Status);
        Assert.Null(record.ErrorCode);
        Assert.Null(record.Retryable);
        Assert.Null(record.ErrorMessage);
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

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Failed, record.Status);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, record.ErrorCode);
        Assert.True(record.Retryable);
    }

    [Fact]
    public async Task GetStatus_RecoveryFailureMarksRequestRetryableInsteadOfLeavingItProcessing()
    {
        await using var db = CreateDbContext();
        var clock = new MutableClock(Now);
        var service = new MobileOfflineSyncService(db, clock);
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);

        await AcquireAsync(service, userId, clientRequestId, request);
        clock.UtcNow = Now.AddMinutes(30);

        var status = await service.GetStatusAsync<string>(
            "shipment.create",
            userId,
            clientRequestId,
            (_, _) => throw new TimeoutException("Recovery query timed out."),
            CancellationToken.None);

        Assert.Equal("Failed", status.Status);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, status.ErrorCode);
        Assert.True(status.Retryable);
        Assert.Equal(5, status.RetryAfterSeconds);

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Failed, record.Status);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, record.ErrorCode);
        Assert.True(record.Retryable);
    }

    [Fact]
    public async Task GetStatus_FreshProcessingRequestDoesNotStartCompetingRecovery()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var recoveryCalls = 0;

        await AcquireAsync(service, userId, clientRequestId, new TestRequest(56, 25));

        var status = await service.GetStatusAsync<string>(
            "shipment.create",
            userId,
            clientRequestId,
            (_, _) =>
            {
                recoveryCalls++;
                return Task.FromResult<string?>(null);
            },
            CancellationToken.None);

        Assert.Equal("Processing", status.Status);
        Assert.Equal("WaitAndCheck", status.RecommendedAction);
        Assert.Equal(0, recoveryCalls);
        Assert.Equal(5, status.RetryAfterSeconds);
    }

    [Fact]
    public async Task Acquire_FreshProcessingRequestReturnsProcessingWithoutStartingRecovery()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);

        await AcquireAsync(service, userId, clientRequestId, request);

        var recoveryCalls = 0;
        var retry = await service.AcquireAsync<TestRequest, string>(
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) =>
            {
                recoveryCalls++;
                throw new TimeoutException("Recovery query timed out.");
            },
            CancellationToken.None,
            preventReexecutionAfterUncertainOutcome: true);

        Assert.Equal(MobileOfflineSyncAcquireState.Processing, retry.State);
        Assert.Equal(0, recoveryCalls);

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Processing, record.Status);
        Assert.Null(record.ErrorCode);
        Assert.Null(record.Retryable);
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
    public async Task Acquire_RecoveryAlreadyStartedSurvivesCallerCancellation()
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

        using var callerCancellation = new CancellationTokenSource();
        var recoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRecovery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = service.AcquireAsync<TestRequest, string>(
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            async (_, recoveryCancellationToken) =>
            {
                recoveryStarted.SetResult();
                await releaseRecovery.Task.WaitAsync(recoveryCancellationToken);
                return "F56/88049";
            },
            callerCancellation.Token,
            preventReexecutionAfterUncertainOutcome: true);

        await recoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        callerCancellation.Cancel();
        await Task.Delay(25);
        releaseRecovery.SetResult();

        var result = await recovery;

        Assert.Equal(MobileOfflineSyncAcquireState.Completed, result.State);
        Assert.Equal("F56/88049", result.Response);
        Assert.Equal(
            MobileOfflineSyncRequestStatus.Completed,
            (await db.MobileOfflineSyncRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task OfflineCreateGuard_CompletionSurvivesCallerCancellationAfterWrite()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);
        using var callerCancellation = new CancellationTokenSource();

        var result = await OfflineCreateGuard.ExecuteAsync<TestRequest, string>(
            service,
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>(null),
            _ =>
            {
                callerCancellation.Cancel();
                return Task.FromResult("F56/88049");
            },
            callerCancellation.Token,
            preventReexecutionAfterUncertainOutcome: true);

        Assert.Equal("F56/88049", result);
        Assert.Equal(
            MobileOfflineSyncRequestStatus.Completed,
            (await db.MobileOfflineSyncRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task OfflineCreateGuard_WriteSurvivesCallerCancellationAfterReservation()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 25);
        using var callerCancellation = new CancellationTokenSource();

        var result = await OfflineCreateGuard.ExecuteAsync<TestRequest, string>(
            service,
            "shipment.create",
            userId,
            56,
            clientRequestId,
            request,
            (_, _) => Task.FromResult<string?>(null),
            async writeCancellationToken =>
            {
                callerCancellation.Cancel();
                await Task.Delay(10, writeCancellationToken);
                return "F56/88049";
            },
            callerCancellation.Token,
            preventReexecutionAfterUncertainOutcome: true);

        Assert.Equal("F56/88049", result);
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
    public async Task QueueBusyFailure_CanRetrySameRequestWithoutCreatingNewIdentity()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();
        var payload = new TestRequest(56, 2);
        var writes = 0;

        Task<string> Execute(CancellationToken _)
        {
            writes++;
            if (writes == 1)
            {
                throw new OperationConflictException(
                    OperationConflictErrorCodes.MikroWriteQueueBusy,
                    "Shipment queue is busy.",
                    retryable: true);
            }

            return Task.FromResult("F56/123");
        }

        var first = await Assert.ThrowsAsync<OperationConflictException>(() =>
            OfflineCreateGuard.ExecuteAsync<TestRequest, string>(
                service,
                "shipment.create",
                user,
                56,
                id,
                payload,
                (_, _) => Task.FromResult<string?>(null),
                Execute,
                CancellationToken.None,
                preventReexecutionAfterUncertainOutcome: true));

        Assert.Equal(OperationConflictErrorCodes.MikroWriteQueueBusy, first.ErrorCode);
        Assert.True(first.Retryable);

        var retry = await OfflineCreateGuard.ExecuteAsync<TestRequest, string>(
            service,
            "shipment.create",
            user,
            56,
            id,
            payload,
            (_, _) => Task.FromResult<string?>(null),
            Execute,
            CancellationToken.None,
            preventReexecutionAfterUncertainOutcome: true);

        Assert.Equal("F56/123", retry);
        Assert.Equal(2, writes);
        Assert.Equal(
            MobileOfflineSyncRequestStatus.Completed,
            (await db.MobileOfflineSyncRequests.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(OperationConflictErrorCodes.SafeWriteRetryAllowed)]
    [InlineData(OperationConflictErrorCodes.MikroWriteQueueBusy)]
    public async Task ProvenNotStartedFailure_AllowsLegacyDraftToUseNewIdentity(string errorCode)
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var user = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var payload = new TestRequest(56, 2);

        await AcquireAsync(service, user, oldId, payload);
        await service.MarkFailedAsync(
            "shipment.create",
            user,
            oldId,
            "Mikro write was proven not to have started.",
            CancellationToken.None,
            errorCode,
            retryable: true);

        var result = await AcquireAsync(service, user, newId, payload);

        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, result.State);
        Assert.Equal(2, await db.MobileOfflineSyncRequests.CountAsync());
    }

    [Fact]
    public async Task UnconfirmedFailure_BlocksLegacyDraftFromUsingNewIdentity()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var user = Guid.NewGuid();
        var oldId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var payload = new TestRequest(56, 2);

        await AcquireAsync(service, user, oldId, payload);
        await service.MarkFailedAsync(
            "shipment.create",
            user,
            oldId,
            "Write outcome is unconfirmed.",
            CancellationToken.None,
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            retryable: true);

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            AcquireAsync(service, user, newId, payload));

        Assert.Equal(OperationConflictErrorCodes.MikroWriteInProgress, exception.ErrorCode);
        Assert.Equal(oldId, exception.RelatedClientRequestId);
        Assert.Single(await db.MobileOfflineSyncRequests.ToArrayAsync());
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
        Task<string?> Recover(string? payload, CancellationToken token) => Task.FromResult<string?>(null);
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

    [Fact]
    public async Task ConcurrentCompletion_IsIdempotentWhenAnotherRequestCompletesFirst()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var firstDb = new AuthDbContext(options);
        await using var secondDb = new AuthDbContext(options);
        var first = new MobileOfflineSyncService(firstDb, new MutableClock(Now));
        var second = new MobileOfflineSyncService(secondDb, new MutableClock(Now.AddSeconds(1)));
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();

        await AcquireAsync(first, user, id, new TestRequest(56, 2));
        await secondDb.MobileOfflineSyncRequests.SingleAsync();

        await first.CompleteAsync("shipment.create", user, id, "F56/123", CancellationToken.None);
        await second.CompleteAsync("shipment.create", user, id, "F56/123", CancellationToken.None);

        secondDb.ChangeTracker.Clear();
        var record = await secondDb.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Completed, record.Status);
        Assert.Equal("\"F56/123\"", record.ResponsePayload);
        Assert.Equal(Now, record.CompletedAtUtc);
    }

    [Fact]
    public async Task DefinitiveCompletion_RecoversFromConcurrentRetryableFailure()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var completingDb = new AuthDbContext(options);
        await using var failingDb = new AuthDbContext(options);
        var completing = new MobileOfflineSyncService(completingDb, new MutableClock(Now.AddSeconds(1)));
        var failing = new MobileOfflineSyncService(failingDb, new MutableClock(Now));
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();

        await AcquireAsync(completing, user, id, new TestRequest(56, 2));
        await failingDb.MobileOfflineSyncRequests.SingleAsync();
        await failing.MarkFailedAsync(
            "shipment.create",
            user,
            id,
            "Temporary failure",
            CancellationToken.None,
            OperationConflictErrorCodes.MikroWriteInProgress,
            retryable: true);

        await completing.CompleteAsync("shipment.create", user, id, "F56/123", CancellationToken.None);

        completingDb.ChangeTracker.Clear();
        var record = await completingDb.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Completed, record.Status);
        Assert.Equal("\"F56/123\"", record.ResponsePayload);
        Assert.Null(record.ErrorCode);
        Assert.Null(record.Retryable);
    }

    [Fact]
    public async Task DefinitiveCompletion_DoesNotOverwriteConcurrentManualReviewDecision()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var completingDb = new AuthDbContext(options);
        await using var reviewDb = new AuthDbContext(options);
        var completing = new MobileOfflineSyncService(completingDb, new MutableClock(Now.AddSeconds(1)));
        var review = new MobileOfflineSyncService(reviewDb, new MutableClock(Now));
        var user = Guid.NewGuid();
        var id = Guid.NewGuid();

        await AcquireAsync(completing, user, id, new TestRequest(56, 2));
        await reviewDb.MobileOfflineSyncRequests.SingleAsync();
        await review.MarkFailedAsync(
            "shipment.create",
            user,
            id,
            "Manual review required",
            CancellationToken.None,
            OperationConflictErrorCodes.MikroDocumentContentMismatch,
            retryable: false);

        var exception = await Assert.ThrowsAsync<OperationConflictException>(() =>
            completing.CompleteAsync("shipment.create", user, id, "F56/123", CancellationToken.None));

        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, exception.ErrorCode);
        Assert.False(exception.Retryable);
        completingDb.ChangeTracker.Clear();
        var record = await completingDb.MobileOfflineSyncRequests.SingleAsync();
        Assert.Equal(MobileOfflineSyncRequestStatus.Failed, record.Status);
        Assert.False(record.Retryable);
    }

    [Fact]
    public async Task ReviewApprovedRetry_ProceedsWithTheSameRequestInsteadOfRemainingProcessing()
    {
        await using var db = CreateDbContext();
        var service = new MobileOfflineSyncService(db, new MutableClock(Now));
        var userId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var request = new TestRequest(56, 2);

        var first = await AcquireAsync(service, userId, clientRequestId, request);
        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, first.State);

        var record = await db.MobileOfflineSyncRequests.SingleAsync();
        record.AllowRetryAfterManualReview(
            OperationConflictErrorCodes.SafeWriteRetryAllowed,
            OperationConflictMessages.SafeWriteRetryAllowed,
            Now.AddMinutes(1));
        await db.SaveChangesAsync();

        var retry = await AcquireAsync(service, userId, clientRequestId, request);

        Assert.Equal(MobileOfflineSyncAcquireState.Proceed, retry.State);
        Assert.Equal(MobileOfflineSyncRequestStatus.Processing, record.Status);
    }

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"mobile-offline-sync-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

    private sealed record TestRequest(int WarehouseNo, int LineCount);

    private sealed record RequestWithIdentity(
        int WarehouseNo,
        int LineCount,
        Guid ClientRequestId,
        Guid RequestedByUserId);

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
