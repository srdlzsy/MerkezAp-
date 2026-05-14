using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
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
        CancellationToken cancellationToken)
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
                    cancellationToken);
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
        var record = await GetTrackedAsync(
            operationCode,
            requestedByUserId,
            NormalizeClientRequestId(clientRequestId),
            cancellationToken);

        if (record is null)
        {
            throw new KeyNotFoundException("Offline sync request was not found.");
        }

        record.MarkCompleted(JsonSerializer.Serialize(response, JsonOptions), clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    internal async Task MarkFailedAsync(
        string operationCode,
        Guid requestedByUserId,
        Guid clientRequestId,
        string errorMessage,
        CancellationToken cancellationToken)
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

        record.MarkFailed(Truncate(errorMessage, 1000), clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);
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
        else
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
        return encoded
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private async Task<MobileOfflineSyncAcquireResult<TResponse>> ResolveAcquireAsync<TResponse>(
        MobileOfflineSyncRequest existing,
        string requestFingerprint,
        string requestJson,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken)
    {
        existing.EnsureRequestFingerprintMatches(requestFingerprint);

        if (existing.Status == MobileOfflineSyncRequestStatus.Completed)
        {
            return MobileOfflineSyncAcquireResult<TResponse>.Completed(
                DeserializeResponse<TResponse>(existing.ResponsePayload));
        }

        var recovered = await TryRecoverAsync(existing, recoverAsync, cancellationToken);
        if (recovered is not null)
        {
            return MobileOfflineSyncAcquireResult<TResponse>.Completed(recovered);
        }

        if (existing.Status == MobileOfflineSyncRequestStatus.Failed || IsProcessingLeaseExpired(existing))
        {
            existing.RestartProcessing(requestFingerprint, requestJson, clock.UtcNow);
            await authDbContext.SaveChangesAsync(cancellationToken);
            return MobileOfflineSyncAcquireResult<TResponse>.Proceed();
        }

        return MobileOfflineSyncAcquireResult<TResponse>.Processing();
    }

    private async Task<TResponse?> TryRecoverAsync<TResponse>(
        MobileOfflineSyncRequest record,
        Func<string?, CancellationToken, Task<TResponse?>> recoverAsync,
        CancellationToken cancellationToken)
    {
        var recovered = await recoverAsync(record.RequestPayload, cancellationToken);
        if (recovered is null)
        {
            return default;
        }

        record.MarkCompleted(JsonSerializer.Serialize(recovered, JsonOptions), clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);
        return recovered;
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
