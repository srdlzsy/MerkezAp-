using System.Security.Cryptography;
using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

public sealed class MobileOfflineSyncService(
    AuthDbContext authDbContext,
    IClock clock,
    ILogger<MobileOfflineSyncService>? logger = null)
{
    private const int StatusRetryAfterSeconds = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ProcessingLeaseTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(180);

    internal async Task<MobileOfflineSyncAcquireResult<TResponse>> AcquireAsync<TRequest, TResponse>(
        string operationCode,
        Guid requestedByUserId,
        int warehouseNo,
        Guid clientRequestId,
        TRequest requestPayload,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken,
        bool preventReexecutionAfterUncertainOutcome = false,
        bool verifyCompletedWithRecovery = false,
        TimeSpan? recentCompletedDuplicateWindow = null,
        bool forceCreateNew = false)
    {
        var normalizedClientRequestId = NormalizeClientRequestId(clientRequestId);
        var requestJson = JsonSerializer.Serialize(requestPayload, JsonOptions);
        var requestFingerprint = ComputeLogicalFingerprint(requestJson);

        await using var logicalRequestLock = await OfflineLogicalRequestLock.AcquireAsync(
            authDbContext,
            operationCode,
            requestedByUserId,
            warehouseNo,
            requestFingerprint,
            cancellationToken);

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
                    preventReexecutionAfterUncertainOutcome,
                    verifyCompletedWithRecovery);
            }

            if (!forceCreateNew && recentCompletedDuplicateWindow > TimeSpan.Zero)
            {
                var recentCompletedDuplicate = await FindRecentCompletedLogicalDuplicateAsync(
                    operationCode,
                    requestedByUserId,
                    warehouseNo,
                    normalizedClientRequestId,
                    requestFingerprint,
                    recentCompletedDuplicateWindow.Value,
                    cancellationToken);

                if (recentCompletedDuplicate is not null)
                {
                    var completedResult = await ResolveCompletedAcquireAsync<TResponse>(
                        recentCompletedDuplicate,
                        recoverAsync,
                        cancellationToken,
                        verifyCompletedWithRecovery);
                    var alias = new MobileOfflineSyncRequest(
                        Guid.NewGuid(),
                        operationCode,
                        requestedByUserId,
                        warehouseNo,
                        normalizedClientRequestId,
                        requestFingerprint,
                        requestJson,
                        clock.UtcNow);
                    alias.MarkCompleted(
                        JsonSerializer.Serialize(completedResult.Response, JsonOptions),
                        clock.UtcNow);
                    await authDbContext.MobileOfflineSyncRequests.AddAsync(alias, cancellationToken);
                    await authDbContext.SaveChangesAsync(cancellationToken);

                    logger?.LogWarning(
                        "Recent duplicate create was replayed without a new Mikro write. OperationCode={OperationCode}, OriginalClientRequestId={OriginalClientRequestId}, ReplayedClientRequestId={ReplayedClientRequestId}, WindowMinutes={WindowMinutes}",
                        operationCode,
                        recentCompletedDuplicate.ClientRequestId,
                        normalizedClientRequestId,
                        recentCompletedDuplicateWindow.Value.TotalMinutes);

                    return completedResult;
                }
            }

            var unresolvedDuplicate = await FindUnresolvedLogicalDuplicateAsync(
                operationCode,
                requestedByUserId,
                warehouseNo,
                normalizedClientRequestId,
                requestFingerprint,
                cancellationToken);

            if (unresolvedDuplicate is not null)
            {
                throw new OperationConflictException(
                    OperationConflictErrorCodes.MikroWriteInProgress,
                    $"Ayni kayit icerigi daha once {unresolvedDuplicate.ClientRequestId} istek kimligiyle gonderildi ve sonucu henuz kesinlesmedi. Yeni istek kimligiyle tekrar yazmayin; onceki kimlikle durum kontrolu yapin.",
                    retryable: true,
                    relatedClientRequestId: Guid.Parse(unresolvedDuplicate.ClientRequestId));
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
        CancellationToken cancellationToken,
        bool verifyCompletedWithRecovery = false)
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
            result = verifyCompletedWithRecovery
                ? await VerifyCompletedAsync(record, recoverAsync, cancellationToken)
                : DeserializeResponse<TResponse>(record.ResponsePayload);
        }
        else if (record.Retryable != false)
        {
            result = await TryRecoverAsync(record, recoverAsync, cancellationToken);
            if (result is null)
            {
                await MarkExpiredProcessingAsUnconfirmedAsync(record);
                await ClassifyLegacyUncertainFailureAsync(record);
            }
        }

        return new OfflineSyncStatusDto<TResponse>(
            clientRequestId,
            record.OperationCode,
            record.Status.ToExternalValue(),
            record.CreatedAtUtc,
            record.CompletedAtUtc,
            OperationConflictMessages.Resolve(record.ErrorCode, record.ErrorMessage),
            record.ErrorCode,
            record.Retryable,
            record.Status == MobileOfflineSyncRequestStatus.Processing || record.Retryable == true
                ? StatusRetryAfterSeconds
                : null,
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

    internal static TRequest DeserializeRequest<TRequest>(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Offline sync request payload was not stored.");
        }

        var request = JsonSerializer.Deserialize<TRequest>(payload, JsonOptions);
        return request ?? throw new InvalidOperationException(
            "Offline sync request payload could not be deserialized.");
    }

    private async Task<MobileOfflineSyncAcquireResult<TResponse>> ResolveAcquireAsync<TResponse>(
        MobileOfflineSyncRequest existing,
        string requestFingerprint,
        string requestJson,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken,
        bool preventReexecutionAfterUncertainOutcome,
        bool verifyCompletedWithRecovery)
    {
        try
        {
            if (!MatchesLogicalRequest(existing, requestFingerprint))
            {
                throw new InvalidOperationException(
                    "The same clientRequestId was already used with a different request payload.");
            }
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
            return await ResolveCompletedAcquireAsync(
                existing,
                recoverAsync,
                cancellationToken,
                verifyCompletedWithRecovery);
        }

        ThrowIfReviewRequired(existing);

        var recovered = await TryRecoverAsync(existing, recoverAsync, cancellationToken);
        if (recovered is not null)
        {
            return MobileOfflineSyncAcquireResult<TResponse>.Completed(recovered);
        }

        await MarkExpiredProcessingAsUnconfirmedAsync(existing);
        await ClassifyLegacyUncertainFailureAsync(existing);

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
                if (existing.Status == MobileOfflineSyncRequestStatus.Completed)
                {
                    return await ResolveCompletedAcquireAsync(
                        existing,
                        recoverAsync,
                        cancellationToken,
                        verifyCompletedWithRecovery);
                }

                return MobileOfflineSyncAcquireResult<TResponse>.Processing();
            }
            return MobileOfflineSyncAcquireResult<TResponse>.Proceed();
        }

        return MobileOfflineSyncAcquireResult<TResponse>.Processing();
    }

    private async Task<MobileOfflineSyncAcquireResult<TResponse>> ResolveCompletedAcquireAsync<TResponse>(
        MobileOfflineSyncRequest record,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken,
        bool verifyCompletedWithRecovery)
    {
        var response = verifyCompletedWithRecovery
            ? await VerifyCompletedAsync(record, recoverAsync, cancellationToken)
            : DeserializeResponse<TResponse>(record.ResponsePayload);

        return MobileOfflineSyncAcquireResult<TResponse>.Completed(response);
    }

    private async Task<TResponse> VerifyCompletedAsync<TResponse>(
        MobileOfflineSyncRequest record,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var recoveryTimeout = new CancellationTokenSource(RecoveryTimeout);

        try
        {
            var recovered = await recoverAsync(record.RequestPayload, recoveryTimeout.Token);
            if (recovered is null)
            {
                throw new OperationConflictException(
                    OperationConflictErrorCodes.MikroDocumentContentMismatch,
                    "The stored completed result could not be verified in Mikro. Manual review is required; do not reuse this clientRequestId.",
                    retryable: false);
            }

            return recovered;
        }
        catch (OperationConflictException exception)
        {
            await MarkCompletedVerificationFailedAsync(
                record,
                exception.Message,
                exception.ErrorCode,
                exception.Retryable);
            throw;
        }
        catch (Exception exception)
        {
            var rootCause = exception.GetBaseException();
            var errorMessage = string.IsNullOrWhiteSpace(rootCause.Message)
                ? "Completed result verification failed; the Mikro document could not be confirmed."
                : $"Completed result verification failed; the Mikro document could not be confirmed. {rootCause.Message}";

            await MarkCompletedVerificationFailedAsync(
                record,
                errorMessage,
                OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                retryable: true);

            logger?.LogWarning(
                exception,
                "Completed offline write verification failed. OperationCode={OperationCode}, ClientRequestId={ClientRequestId}; the stored success response will not be replayed.",
                record.OperationCode,
                record.ClientRequestId);

            throw new OperationConflictException(
                OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                errorMessage,
                retryable: true,
                innerException: exception);
        }
    }

    private async Task MarkCompletedVerificationFailedAsync(
        MobileOfflineSyncRequest record,
        string errorMessage,
        string errorCode,
        bool retryable)
    {
        record.MarkCompletedVerificationFailed(
            Truncate(errorMessage, 1000),
            clock.UtcNow,
            errorCode,
            retryable);

        try
        {
            using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await authDbContext.SaveChangesAsync(persistenceTimeout.Token);
        }
        catch (DbUpdateConcurrencyException)
        {
            await authDbContext.Entry(record).ReloadAsync(CancellationToken.None);
        }
    }

    internal static bool IsUncertainWriteOutcome(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        var normalized = errorMessage.Trim();
        return WriteTimeoutClassifier.ContainsTimeoutMarker(normalized) ||
               normalized.Contains("canceled", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("cancelled", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("write outcome could not be confirmed", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ClassifyLegacyUncertainFailureAsync(MobileOfflineSyncRequest record)
    {
        if (record.Status != MobileOfflineSyncRequestStatus.Failed ||
            record.Retryable.HasValue ||
            !string.IsNullOrWhiteSpace(record.ErrorCode) ||
            !IsUncertainWriteOutcome(record.ErrorMessage))
        {
            return;
        }

        record.MarkFailed(
            OfflineWriteFailureClassifier.UnconfirmedMessage,
            clock.UtcNow,
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            retryable: true);

        try
        {
            await authDbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            await authDbContext.Entry(record).ReloadAsync(CancellationToken.None);
        }
    }

    private async Task<TResponse?> TryRecoverAsync<TResponse>(
        MobileOfflineSyncRequest record,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var recoveryTimeout = new CancellationTokenSource(RecoveryTimeout);
        var recoveryCancellationToken = recoveryTimeout.Token;
        TResponse? recovered;
        try
        {
            recovered = await recoverAsync(record.RequestPayload, recoveryCancellationToken);
        }
        catch (OperationConflictException exception)
        {
            using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await MarkFailedAsync(record.OperationCode, record.RequestedByUserId,
                Guid.Parse(record.ClientRequestId), exception.Message, persistenceTimeout.Token,
                exception.ErrorCode, exception.Retryable);
            throw;
        }
        catch (Exception exception)
        {
            var rootCause = exception.GetBaseException();
            var errorMessage = string.IsNullOrWhiteSpace(rootCause.Message)
                ? "Recovery readback failed; the write outcome could not be confirmed."
                : $"Recovery readback failed; the write outcome could not be confirmed. {rootCause.Message}";

            using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await MarkFailedAsync(
                record.OperationCode,
                record.RequestedByUserId,
                Guid.Parse(record.ClientRequestId),
                errorMessage,
                persistenceTimeout.Token,
                OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                retryable: true);

            logger?.LogWarning(
                exception,
                "Offline write recovery readback failed. OperationCode={OperationCode}, ClientRequestId={ClientRequestId}; the request was marked retryable instead of remaining Processing.",
                record.OperationCode,
                record.ClientRequestId);

            return default;
        }
        if (recovered is null)
        {
            return default;
        }

        record.MarkCompleted(JsonSerializer.Serialize(recovered, JsonOptions), clock.UtcNow);
        try
        {
            await authDbContext.SaveChangesAsync(recoveryCancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await authDbContext.Entry(record).ReloadAsync(recoveryCancellationToken);
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

    private async Task<MobileOfflineSyncRequest?> FindUnresolvedLogicalDuplicateAsync(
        string operationCode,
        Guid requestedByUserId,
        int warehouseNo,
        string normalizedClientRequestId,
        string requestFingerprint,
        CancellationToken cancellationToken)
    {
        var candidates = await authDbContext.MobileOfflineSyncRequests
            .AsNoTracking()
            .Where(item =>
                item.OperationCode == operationCode &&
                item.RequestedByUserId == requestedByUserId &&
                item.WarehouseNo == warehouseNo &&
                item.ClientRequestId != normalizedClientRequestId &&
                (item.Status == MobileOfflineSyncRequestStatus.Processing ||
                 (item.Status == MobileOfflineSyncRequestStatus.Failed && item.Retryable == true)))
            .OrderByDescending(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return candidates.FirstOrDefault(candidate => MatchesLogicalRequest(candidate, requestFingerprint));
    }

    private async Task<MobileOfflineSyncRequest?> FindRecentCompletedLogicalDuplicateAsync(
        string operationCode,
        Guid requestedByUserId,
        int warehouseNo,
        string normalizedClientRequestId,
        string requestFingerprint,
        TimeSpan completedDuplicateWindow,
        CancellationToken cancellationToken)
    {
        var cutoff = clock.UtcNow - completedDuplicateWindow;
        var candidates = await authDbContext.MobileOfflineSyncRequests
            .AsNoTracking()
            .Where(item =>
                item.OperationCode == operationCode &&
                item.RequestedByUserId == requestedByUserId &&
                item.WarehouseNo == warehouseNo &&
                item.ClientRequestId != normalizedClientRequestId &&
                item.Status == MobileOfflineSyncRequestStatus.Completed &&
                item.CompletedAtUtc >= cutoff)
            .OrderByDescending(item => item.CompletedAtUtc)
            .Take(20)
            .ToArrayAsync(cancellationToken);

        return candidates.FirstOrDefault(candidate => MatchesLogicalRequest(candidate, requestFingerprint));
    }

    private bool IsProcessingLeaseExpired(MobileOfflineSyncRequest record)
    {
        if (record.Status != MobileOfflineSyncRequestStatus.Processing)
        {
            return false;
        }

        var referenceTime = record.UpdatedAtUtc ?? record.CreatedAtUtc;
        return DateTime.SpecifyKind(referenceTime, DateTimeKind.Utc) + ProcessingLeaseTimeout <= clock.UtcNow;
    }

    private async Task MarkExpiredProcessingAsUnconfirmedAsync(MobileOfflineSyncRequest record)
    {
        if (!IsProcessingLeaseExpired(record))
        {
            return;
        }

        using var persistenceTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await MarkFailedAsync(
            record.OperationCode,
            record.RequestedByUserId,
            Guid.Parse(record.ClientRequestId),
            "The processing lease expired and recovery readback did not find a completed document. The write outcome remains unconfirmed.",
            persistenceTimeout.Token,
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            retryable: true);
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

    private static bool MatchesLogicalRequest(
        MobileOfflineSyncRequest record,
        string requestFingerprint) =>
        string.Equals(record.RequestFingerprint, requestFingerprint, StringComparison.Ordinal) ||
        (!string.IsNullOrWhiteSpace(record.RequestPayload) &&
         string.Equals(
             ComputeLogicalFingerprint(record.RequestPayload),
             requestFingerprint,
             StringComparison.Ordinal));

    private static string ComputeLogicalFingerprint(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.RootElement.WriteTo(writer);
            }
            else
            {
                writer.WriteStartObject();
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name.Equals("clientRequestId", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("requestedByUserId", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("createdByUserId", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("forceCreateNew", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    property.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
        }

        var bytes = SHA256.HashData(stream.ToArray());
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
