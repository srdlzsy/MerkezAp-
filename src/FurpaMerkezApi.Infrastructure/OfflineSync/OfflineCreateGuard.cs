using FurpaMerkezApi.Application.Common.Errors;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

internal static class OfflineCreateGuard
{
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
        bool preventReexecutionAfterUncertainOutcome = false)
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

        var acquireResult = await mobileOfflineSyncService.AcquireAsync<TRequest, TResponse>(
            operationCode,
            requestedByUserId.Value,
            warehouseNo,
            clientRequestId.Value,
            requestPayload,
            recoverAsync,
            cancellationToken,
            preventReexecutionAfterUncertainOutcome);

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
            // Once a durable idempotency record exists, a dropped HTTP connection must
            // not cancel the business write. Database and upstream command timeouts still
            // bound the operation, and the stored result can be recovered by the client.
            var response = await executeAsync(CancellationToken.None);
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
