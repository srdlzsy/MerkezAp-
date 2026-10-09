using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

public sealed class MobileOfflineSyncService(
    AuthDbContext authDbContext,
    IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ProcessingLeaseTimeout = TimeSpan.FromMinutes(5);

    internal async Task<MobileOfflineSyncAcquireResult<TResponse>> AcquireAsync<TRequest, TResponse>(
        string operationCode,
        Guid requestedByUserId,
        int warehouseNo,
        Guid clientRequestId,
        TRequest requestPayload,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken,
        bool preventReexecutionAfterUncertainOutcome = false)
    {
        var normalizedClientRequestId = NormalizeClientRequestId(clientRequestId);
        var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);
        var requestFingerprint = ComputeFingerprint(requestJson);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = await GetTrackedAsync(
                operationCode,
                requestedByUserId,
                normalizedClientRequestId,
                cancellationToken);

            if (existing is not null)
            {
                return await ResolveAcquireAsync(
                    existing,
                    requestFingerprint,
                    requestJson,
                    recoverAsync,
                    cancellationToken,
                    preventReexecutionAfterUncertainOutcome);
            }

            var record = new MobileOfflineSyncRequest(
                Guid.NewGuid(),
                operationCode,
                requestedByUserId,
                warehouseNo,
                normalizedClientRequestId,
                requestFingerprint,
                requestJson,
                clock.UtcNow);

            await authDbContext.MobileOfflineSyncRequests.AddAsync(record, cancellationToken);

            try
            {
                await authDbContext.SaveChangesAsync(cancellationToken);
                return MobileOfflineSyncAcquireResult<TResponse>.Proceed();
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                authDbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("Offline sync request could not be reserved.");
    }

    internal async Task CompleteAsync<TResponse>(
        string operationCode,
        Guid requestedByUserId,
        Guid clientRequestId,
        TResponse response,
        CancellationToken cancellationToken)
    {
        var normalizedClientRequestId = NormalizeClientRequestId(clientRequestId);
        var responsePayload = JsonSerializer.Serialize(response, JsonOptions);
        var record = await GetTrackedAsync(
            operationCode,
            requestedByUserId,
            normalizedClientRequestId,
            cancellationToken);

        if (record is null)
        {
            throw new KeyNotFoundException("Offline sync request was not found.");
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (record.Status == MobileOfflineSyncRequestStatus.Completed)
            {
                return;
            }

            ThrowIfReviewRequired(record);
            record.MarkCompleted(responsePayload, clock.UtcNow);

            try
            {
                await authDbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                authDbContext.ChangeTracker.Clear();
                record = await GetTrackedAsync(
                    operationCode,
                    requestedByUserId,
                    normalizedClientRequestId,
                    cancellationToken)
                    ?? throw new KeyNotFoundException("Offline sync request was not found.");

                if (record.Status == MobileOfflineSyncRequestStatus.Completed)
                {
                    return;
                }

                ThrowIfReviewRequired(record);
                if (attempt == 2)
                {
                    throw new OperationConflictException(
                        OperationConflictErrorCodes.MikroWriteInProgress,
                        "The request completion state changed concurrently. Retry with the same clientRequestId.",
                        retryable: true,
                        innerException: exception);
                }
            }
        }
    }

    internal async Task MarkFailedAsync(
        string operationCode,
        Guid requestedByUserId,
        Guid clientRequestId,
        string errorMessage,
        CancellationToken cancellationToken,
        string? errorCode = null,
        bool? retryable = null)
    {
        var record = await GetTrackedAsync(
            operationCode,
            requestedByUserId,
            NormalizeClientRequestId(clientRequestId),
            cancellationToken);

        if (record is null)
        {
            return;
        }

        record.MarkFailed(Truncate(errorMessage, 1000), clock.UtcNow, errorCode, retryable);
        try
        {
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A stale attempt must not overwrite a newer owner or a completed result.
            await authDbContext.Entry(record).ReloadAsync(cancellationToken);
        }
    }

    internal async Task<OfflineSyncStatusDto<TResponse>> GetStatusAsync<TResponse>(
        string operationCode,
        Guid requestedByUserId,
        Guid clientRequestId,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken)
    {
        var record = await GetTrackedAsync(
            operationCode,
            requestedByUserId,
            NormalizeClientRequestId(clientRequestId),
            cancellationToken)
            ?? throw new KeyNotFoundException("Offline sync request was not found.");

        TResponse? result = default;

        if (record.Status == MobileOfflineSyncRequestStatus.Completed)
        {
            result = DeserializeResponse<TResponse>(record.ResponsePayload);
        }
        else if (record.Retryable != false)
        {
            result = await TryRecoverAsync(record, recoverAsync, cancellationToken);
        }

        return new OfflineSyncStatusDto<TResponse>(
            clientRequestId,
            record.OperationCode,
            record.Status.ToExternalValue(),
            record.CreatedAtUtc,
            record.CompletedAtUtc,
            record.ErrorMessage,
            result);
    }

    private static string NormalizeClientRequestId(Guid clientRequestId) =>
        clientRequestId.ToString("D").ToLowerInvariant();

    internal static string ToTraceKey(Guid clientRequestId)
    {
        var encoded = Convert.ToBase64String(clientRequestId.ToByteArray());
        return "FR" + encoded
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task<MobileOfflineSyncAcquireResult<TResponse>> ResolveAcquireAsync<TResponse>(
        MobileOfflineSyncRequest existing,
        string requestFingerprint,
        string requestJson,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken,
        bool preventReexecutionAfterUncertainOutcome)
    {
        try
        {
            existing.EnsureRequestFingerprintMatches(requestFingerprint);
        }
        catch (InvalidOperationException exception)
        {
            throw new OperationConflictException(
                OperationConflictErrorCodes.ClientRequestPayloadMismatch,
                exception.Message,
                retryable: false,
                innerException: exception);
        }

        if (existing.Status == MobileOfflineSyncRequestStatus.Completed)
        {
            return MobileOfflineSyncAcquireResult<TResponse>.Completed(
                DeserializeResponse<TResponse>(existing.ResponsePayload));
        }

        ThrowIfReviewRequired(existing);

        var recovered = await TryRecoverAsync(existing, recoverAsync, cancellationToken);
        if (recovered is not null)
        {
            return MobileOfflineSyncAcquireResult<TResponse>.Completed(recovered);
        }

        if (preventReexecutionAfterUncertainOutcome &&
            (existing.Status == MobileOfflineSyncRequestStatus.Processing ||
             existing.ErrorCode is OperationConflictErrorCodes.MikroWriteInProgress or OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed ||
             IsUncertainWriteOutcome(existing.ErrorMessage)))
        {
            return MobileOfflineSyncAcquireResult<TResponse>.Processing();
        }

        if (existing.Status == MobileOfflineSyncRequestStatus.Failed || IsProcessingLeaseExpired(existing))
        {
            existing.RestartProcessing(requestFingerprint, requestJson, clock.UtcNow);
            try
            {
                await authDbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await authDbContext.Entry(existing).ReloadAsync(cancellationToken);
                ThrowIfReviewRequired(existing);
                return existing.Status == MobileOfflineSyncRequestStatus.Completed
                    ? MobileOfflineSyncAcquireResult<TResponse>.Completed(DeserializeResponse<TResponse>(existing.ResponsePayload))
                    : MobileOfflineSyncAcquireResult<TResponse>.Processing();
            }
            return MobileOfflineSyncAcquireResult<TResponse>.Proceed();
        }

        return MobileOfflineSyncAcquireResult<TResponse>.Processing();
    }

    internal static bool IsUncertainWriteOutcome(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        var normalized = errorMessage.Trim();
        return normalized.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("time out", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("cancelled", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("write outcome could not be confirmed", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<TResponse?> TryRecoverAsync<TResponse>(
        MobileOfflineSyncRequest record,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken)
    {
        TResponse? recovered;
        try
        {
            recovered = await recoverAsync(record.RequestPayload, cancellationToken);
        }
        catch (OperationConflictException exception)
        {
            using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await MarkFailedAsync(record.OperationCode, record.RequestedByUserId,
                Guid.Parse(record.ClientRequestId), exception.Message, persistenceTimeout.Token,
                exception.ErrorCode, exception.Retryable);
            throw;
        }
        if (recovered is null)
        {
            return default;
        }

        record.MarkCompleted(JsonSerializer.Serialize(recovered, JsonOptions), clock.UtcNow);
        try
        {
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await authDbContext.Entry(record).ReloadAsync(cancellationToken);
            ThrowIfReviewRequired(record);
            if (record.Status == MobileOfflineSyncRequestStatus.Completed)
                return DeserializeResponse<TResponse>(record.ResponsePayload);

            throw new OperationConflictException(OperationConflictErrorCodes.MikroWriteInProgress,
                "The request changed during readback. Retry with the same clientRequestId.", true);
        }
        return recovered;
    }

    private static void ThrowIfReviewRequired(MobileOfflineSyncRequest record)
    {
        if (record.Retryable == false)
        {
            throw new OperationConflictException(
                record.ErrorCode ?? OperationConflictErrorCodes.MikroDocumentContentMismatch,
                record.ErrorMessage ?? "Manual review is required; do not submit a new request.", false);
        }
    }

    private Task<MobileOfflineSyncRequest?> GetTrackedAsync(
        string operationCode,
        Guid requestedByUserId,
        string normalizedClientRequestId,
        CancellationToken cancellationToken) =>
        authDbContext.MobileOfflineSyncRequests
            .FirstOrDefaultAsync(
                item =>
                    item.OperationCode == operationCode &&
                    item.RequestedByUserId == requestedByUserId &&
                    item.ClientRequestId == normalizedClientRequestId,
                cancellationToken);

    private bool IsProcessingLeaseExpired(MobileOfflineSyncRequest record)
    {
        if (record.Status != MobileOfflineSyncRequestStatus.Processing)
        {
            return false;
        }

        var referenceTime = record.UpdatedAtUtc ?? record.CreatedAtUtc;
        return DateTime.SpecifyKind(referenceTime, DateTimeKind.Utc) + ProcessingLeaseTimeout <= clock.UtcNow;
    }

    private static TResponse DeserializeResponse<TResponse>(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Offline sync request result was not stored.");
        }

        var response = JsonSerializer.Deserialize<TResponse>(payload, JsonOptions);
        return response ?? throw new InvalidOperationException("Offline sync request result could not be deserialized.");
    }

    private static string ComputeFingerprint(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

internal enum MobileOfflineSyncAcquireState
{
    Proceed = 1,
    Completed = 2,
    Processing = 3
}

internal sealed record MobileOfflineSyncAcquireResult<TResponse>(
    MobileOfflineSyncAcquireState State,
    TResponse? Response)
{
    public static MobileOfflineSyncAcquireResult<TResponse> Proceed() =>
        new(MobileOfflineSyncAcquireState.Proceed, default);

    public static MobileOfflineSyncAcquireResult<TResponse> Completed(TResponse response) =>
        new(MobileOfflineSyncAcquireState.Completed, response);

    public static MobileOfflineSyncAcquireResult<TResponse> Processing() =>
        new(MobileOfflineSyncAcquireState.Processing, default);
}

internal static class MobileOfflineSyncRequestStatusExtensions
{
    public static string ToExternalValue(this MobileOfflineSyncRequestStatus status) =>
        status switch
        {
            MobileOfflineSyncRequestStatus.Processing => "Processing",
            MobileOfflineSyncRequestStatus.Completed => "Completed",
            MobileOfflineSyncRequestStatus.Failed => "Failed",
            _ => status.ToString()
        };
}
