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
        CancellationToken cancellationToken)
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
            cancellationToken);

        if (acquireResult.State == MobileOfflineSyncAcquireState.Completed)
        {
            return acquireResult.Response!;
        }

        if (acquireResult.State == MobileOfflineSyncAcquireState.Processing)
        {
            throw new InvalidOperationException(
                $"A create request with the same clientRequestId is already being processed. OperationCode={operationCode}");
        }

        try
        {
            var response = await executeAsync(cancellationToken);
            await mobileOfflineSyncService.CompleteAsync(
                operationCode,
                requestedByUserId.Value,
                clientRequestId.Value,
                response,
                cancellationToken);

            return response;
        }
        catch (Exception exception)
        {
            await TryMarkFailedAsync(
                mobileOfflineSyncService,
                operationCode,
                requestedByUserId.Value,
                clientRequestId.Value,
                exception.Message,
                cancellationToken);
            throw;
        }
    }

    private static async Task TryMarkFailedAsync(
        MobileOfflineSyncService mobileOfflineSyncService,
        string operationCode,
        Guid requestedByUserId,
        Guid clientRequestId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await mobileOfflineSyncService.MarkFailedAsync(
                operationCode,
                requestedByUserId,
                clientRequestId,
                errorMessage,
                cancellationToken);
        }
        catch
        {
            // Original write exception must stay visible to the caller.
        }
    }
}
