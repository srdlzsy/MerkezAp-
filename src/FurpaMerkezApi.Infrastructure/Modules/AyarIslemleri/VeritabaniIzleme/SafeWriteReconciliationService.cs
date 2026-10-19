using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

internal sealed class SafeWriteReconciliationService(
    AuthDbContext authDbContext,
    ISafeWriteRecoveryDispatcher recoveryDispatcher,
    IClock clock,
    IOptionsMonitor<DatabaseMonitoringOptions> options,
    ILogger<SafeWriteReconciliationService> logger)
{
    private const string CheckingMessage =
        "Guvenli yazma sonucu arka planda salt-okuma ile dogrulaniyor.";

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        if (!settings.Enabled || !settings.SafeWriteReconciliationEnabled)
        {
            return 0;
        }

        var staleBefore = clock.UtcNow - MobileOfflineSyncService.ProcessingLeaseTimeout;
        var retryableBefore = clock.UtcNow.AddSeconds(-Math.Clamp(
            settings.SafeWriteReconciliationIntervalSeconds,
            30,
            3600));
        var batchSize = Math.Clamp(settings.SafeWriteReconciliationBatchSize, 1, 100);
        var candidates = await authDbContext.MobileOfflineSyncRequests
            .AsNoTracking()
            .Where(item =>
                (item.Status == MobileOfflineSyncRequestStatus.Processing &&
                 (item.UpdatedAtUtc ?? item.CreatedAtUtc) < staleBefore) ||
                (item.Status == MobileOfflineSyncRequestStatus.Failed &&
                 item.Retryable == true &&
                 item.ErrorCode != OperationConflictErrorCodes.SafeWriteRetryAllowed &&
                 (item.UpdatedAtUtc ?? item.CreatedAtUtc) < retryableBefore))
            .OrderBy(item => item.UpdatedAtUtc ?? item.CreatedAtUtc)
            .Select(item => new
            {
                item.Id,
                item.OperationCode,
                item.WarehouseNo,
                item.RequestedByUserId,
                item.ClientRequestId
            })
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        var processed = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var record = await authDbContext.MobileOfflineSyncRequests
                    .SingleOrDefaultAsync(item => item.Id == candidate.Id, cancellationToken);
                if (record is null ||
                    record.Status == MobileOfflineSyncRequestStatus.Completed ||
                    record.Status == MobileOfflineSyncRequestStatus.Cancelled ||
                    record.Retryable == false ||
                    record.ErrorCode == OperationConflictErrorCodes.SafeWriteRetryAllowed)
                {
                    continue;
                }

                record.PrepareForManualRecoveryCheck(
                    OperationConflictErrorCodes.SafeWriteReviewChecking,
                    CheckingMessage,
                    clock.UtcNow);
                await authDbContext.SaveChangesAsync(cancellationToken);

                var result = await recoveryDispatcher.RecoverAsync(
                    candidate.OperationCode,
                    candidate.WarehouseNo,
                    candidate.RequestedByUserId,
                    Guid.Parse(candidate.ClientRequestId),
                    cancellationToken);

                await authDbContext.Entry(record).ReloadAsync(cancellationToken);
                if (record.Status != MobileOfflineSyncRequestStatus.Completed &&
                    record.ErrorCode == OperationConflictErrorCodes.SafeWriteReviewChecking &&
                    result.Result is null)
                {
                    record.AllowRetryAfterManualReview(
                        OperationConflictErrorCodes.SafeWriteRetryAllowed,
                        OperationConflictMessages.SafeWriteRetryAllowed,
                        clock.UtcNow);
                    await authDbContext.SaveChangesAsync(cancellationToken);
                }

                logger.LogInformation(
                    "Stale safe write was reconciled without a new create. SafeWriteId={SafeWriteId}, OperationCode={OperationCode}, ClientRequestId={ClientRequestId}, ResultStatus={ResultStatus}",
                    candidate.Id,
                    candidate.OperationCode,
                    candidate.ClientRequestId,
                    result.Status);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationConflictException exception)
            {
                logger.LogInformation(
                    "Stale safe write readback was classified. SafeWriteId={SafeWriteId}, OperationCode={OperationCode}, ClientRequestId={ClientRequestId}, ErrorCode={ErrorCode}, Retryable={Retryable}",
                    candidate.Id,
                    candidate.OperationCode,
                    candidate.ClientRequestId,
                    exception.ErrorCode,
                    exception.Retryable);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Stale safe write reconciliation failed. SafeWriteId={SafeWriteId}, OperationCode={OperationCode}, ClientRequestId={ClientRequestId}",
                    candidate.Id,
                    candidate.OperationCode,
                    candidate.ClientRequestId);
            }
            finally
            {
                authDbContext.ChangeTracker.Clear();
                processed++;
            }
        }

        return processed;
    }
}
