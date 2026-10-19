using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

internal static class OfflineCreateGuard
{
    internal static readonly TimeSpan RecentCompletedDuplicateWindow = TimeSpan.FromHours(6);

    public static async Task<TResponse> ExecuteAsync<TRequest, TResponse>(
        MobileOfflineSyncService mobileOfflineSyncService,
        string operationCode,
        Guid? requestedByUserId,
        int warehouseNo,
        Guid? clientRequestId,
        TRequest requestPayload,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        Func<CancellationToken, Task<TResponse>> executeAsync,
        CancellationToken cancellationToken,
        bool preventReexecutionAfterUncertainOutcome = false,
        bool verifyCompletedWithRecovery = false)
    {
        if (!clientRequestId.HasValue)
        {
            return await executeAsync(cancellationToken);
        }

        if (clientRequestId.Value == Guid.Empty)
        {
            throw new ArgumentException("Client request id can not be empty.", nameof(clientRequestId));
        }

        if (!requestedByUserId.HasValue || requestedByUserId.Value == Guid.Empty)
        {
            throw new ArgumentException("Requested by user id is required when clientRequestId is provided.", nameof(requestedByUserId));
        }

        using var durableExecutionTimeout = DurableCreateExecution.CreateTimeout();
        var durableCancellationToken = durableExecutionTimeout.Token;

        var acquireResult = await mobileOfflineSyncService.AcquireAsync<TRequest, TResponse>(
            operationCode,
            requestedByUserId.Value,
            warehouseNo,
            clientRequestId.Value,
            requestPayload,
            recoverAsync,
            durableCancellationToken,
            preventReexecutionAfterUncertainOutcome,
            verifyCompletedWithRecovery,
            RecentCompletedDuplicateWindow,
            requestPayload is IExplicitNewCreateRequest { ForceCreateNew: true });

        if (acquireResult.State == MobileOfflineSyncAcquireState.Completed)
        {
            return acquireResult.Response!;
        }

        if (acquireResult.State == MobileOfflineSyncAcquireState.Processing)
        {
            throw new OperationConflictException(
                OperationConflictErrorCodes.MikroWriteInProgress,
                $"A create request with the same clientRequestId is already being processed. OperationCode={operationCode}",
                retryable: true);
        }

        try
        {
            // Action execution means ASP.NET has received and bound the complete body.
            // From this point the durable request owns the bounded business operation.
            var response = await executeAsync(durableCancellationToken);
            using var completionTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await mobileOfflineSyncService.CompleteAsync(
                operationCode,
                requestedByUserId.Value,
                clientRequestId.Value,
                response,
                completionTimeout.Token);

            return response;
        }
        catch (Exception exception)
        {
            await TryMarkFailedAsync(
                mobileOfflineSyncService,
                operationCode,
                requestedByUserId.Value,
                clientRequestId.Value,
                exception);
            throw;
        }
    }

    private static async Task TryMarkFailedAsync(
        MobileOfflineSyncService mobileOfflineSyncService,
        string operationCode,
        Guid requestedByUserId,
        Guid clientRequestId,
        Exception exception)
    {
        try
        {
            using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var failure = OfflineWriteFailureClassifier.Classify(exception);
            await mobileOfflineSyncService.MarkFailedAsync(
                operationCode,
                requestedByUserId,
                clientRequestId,
                failure.Message,
                persistenceTimeout.Token,
                failure.ErrorCode,
                failure.Retryable);
        }
        catch
        {
            // Original write exception must stay visible to the caller.
        }
    }
}
