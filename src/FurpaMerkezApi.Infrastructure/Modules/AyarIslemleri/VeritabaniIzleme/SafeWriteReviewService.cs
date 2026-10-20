using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class SafeWriteReviewService(
    AuthDbContext authDbContext,
    IClock clock,
    ISafeWriteRecoveryDispatcher recoveryDispatcher) : ISafeWriteReviewService
{
    private const string CheckingMessage =
        "Mikro belgesi salt-okuma ile yeniden dogrulaniyor.";
    private const string OwnerRecheckReason =
        "Kayit sahibi kullanici Mikro belgesini salt-okuma ile yeniden dogruladi.";
    private const string OwnerRetryExhaustedReason =
        "Terminal ayni istek kimligiyle izin verilen otomatik tekrari tamamladi; Mikro'da belge bulunmadigi kesinlesen bekleyen kayit kapatildi.";

    public async Task<SafeWriteReviewResultDto> RecheckOwnedAsync(
        string operationCode,
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        var normalizedClientRequestId = clientRequestId.ToString("D").ToLowerInvariant();
        var record = await authDbContext.MobileOfflineSyncRequests
            .FirstOrDefaultAsync(item =>
                item.OperationCode == operationCode &&
                item.WarehouseNo == warehouseNo &&
                item.RequestedByUserId == requestedByUserId &&
                item.ClientRequestId == normalizedClientRequestId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Safe write record was not found for the current user and warehouse.");

        return await ReviewWithReadbackAsync(
            record,
            new SafeWriteReviewRequest(OwnerRecheckReason),
            requestedByUserId,
            allowRetryWhenMissing: false,
            action: "OwnerRecheck",
            cancellationToken);
    }

    public async Task<SafeWriteReviewResultDto> CancelOwnedRetryAllowedAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        var normalizedClientRequestId = clientRequestId.ToString("D").ToLowerInvariant();
        var records = await authDbContext.MobileOfflineSyncRequests
            .Where(item =>
                item.WarehouseNo == warehouseNo &&
                item.RequestedByUserId == requestedByUserId &&
                item.ClientRequestId == normalizedClientRequestId)
            .ToArrayAsync(cancellationToken);

        if (records.Length == 0)
        {
            throw new KeyNotFoundException("Safe write record was not found for the current user and warehouse.");
        }

        if (records.Length != 1)
        {
            throw new OperationConflictException(
                OperationConflictErrorCodes.ClientRequestPayloadMismatch,
                "Ayni istek kimligi birden fazla operasyon kaydinda bulundu; otomatik kapatma yapilmadi.",
                retryable: false);
        }

        var record = records[0];
        var previous = Snapshot(record);

        if (record.Status == MobileOfflineSyncRequestStatus.Cancelled)
        {
            return await CompleteReviewAsync(
                record, previous, "OwnerRetryExhausted", "AlreadyCancelled",
                OwnerRetryExhaustedReason, requestedByUserId,
                "Bekleyen kayit daha once kapatilmis.", result: null, cancellationToken);
        }

        if (record.Status != MobileOfflineSyncRequestStatus.Failed ||
            record.Retryable != true ||
            record.ErrorCode != OperationConflictErrorCodes.SafeWriteRetryAllowed)
        {
            throw new OperationConflictException(
                record.ErrorCode ?? OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                "Yalniz Mikro'da belge bulunmadigi kesinlesen tekrar-izinli kayit terminal tarafindan kapatilabilir.",
                retryable: record.Retryable == true);
        }

        record.CancelAfterManualReview(
            OperationConflictErrorCodes.SafeWriteCancelled,
            "Mikro'da belge bulunmadi; izin verilen otomatik tekrar da tamamlanamayinca bekleyen terminal kaydi kapatildi.",
            clock.UtcNow);

        return await CompleteReviewAsync(
            record, previous, "OwnerRetryExhausted", "Cancelled",
            OwnerRetryExhaustedReason, requestedByUserId,
            "Mikro evraki olusmadi; bekleyen islem kapatildi. Kullanici formu kontrol edip yeniden kaydedebilir.",
            result: null, cancellationToken,
            recoveryState: "Missing",
            recommendedAction: "ClearCancelled");
    }

    public Task<SafeWriteReviewResultDto> RecheckAsync(
        Guid safeWriteId,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        CancellationToken cancellationToken) =>
        ReviewWithReadbackAsync(
            safeWriteId,
            request,
            reviewedByUserId,
            allowRetryWhenMissing: false,
            action: "Recheck",
            cancellationToken);

    public Task<SafeWriteReviewResultDto> AllowRetryAsync(
        Guid safeWriteId,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        CancellationToken cancellationToken) =>
        ReviewWithReadbackAsync(
            safeWriteId,
            request,
            reviewedByUserId,
            allowRetryWhenMissing: true,
            action: "AllowRetry",
            cancellationToken);

    public async Task<SafeWriteReviewResultDto> CancelAsync(
        Guid safeWriteId,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        CancellationToken cancellationToken)
    {
        var record = await GetRecordAsync(safeWriteId, cancellationToken);
        var previous = Snapshot(record);

        if (record.Status == MobileOfflineSyncRequestStatus.Completed)
        {
            return await CompleteReviewAsync(
                record, previous, "Cancel", "AlreadyCompleted", request.Reason, reviewedByUserId,
                "Kayit zaten tamamlanmis; iptal edilmedi.", result: null, cancellationToken);
        }

        if (record.Status != MobileOfflineSyncRequestStatus.Cancelled)
        {
            record.CancelAfterManualReview(
                OperationConflictErrorCodes.SafeWriteCancelled,
                OperationConflictMessages.SafeWriteCancelled,
                clock.UtcNow);
        }

        return await CompleteReviewAsync(
            record, previous, "Cancel", "Cancelled", request.Reason, reviewedByUserId,
            "Bekleyen kayit yetkili karariyla iptal edildi; terminal kaydi temizleyebilir.",
            result: null,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<SafeWriteReviewAuditDto>> GetHistoryAsync(
        Guid safeWriteId,
        CancellationToken cancellationToken)
    {
        var exists = await authDbContext.MobileOfflineSyncRequests
            .AsNoTracking()
            .AnyAsync(item => item.Id == safeWriteId, cancellationToken);
        if (!exists) throw new KeyNotFoundException("Safe write record was not found.");

        return await authDbContext.SafeWriteReviewAudits
            .AsNoTracking()
            .Where(item => item.SafeWriteId == safeWriteId)
            .OrderByDescending(item => item.ReviewedAtUtc)
            .Select(item => new SafeWriteReviewAuditDto(
                item.Id,
                item.SafeWriteId,
                item.Action,
                item.Outcome,
                item.Reason,
                item.ReviewedByUserId,
                item.ReviewedAtUtc,
                item.PreviousStatus,
                item.PreviousErrorCode,
                item.ResultStatus,
                item.ResultErrorCode,
                item.Message))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<SafeWriteReviewResultDto> ReviewWithReadbackAsync(
        Guid safeWriteId,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        bool allowRetryWhenMissing,
        string action,
        CancellationToken cancellationToken)
    {
        var record = await GetRecordAsync(safeWriteId, cancellationToken);
        return await ReviewWithReadbackAsync(
            record,
            request,
            reviewedByUserId,
            allowRetryWhenMissing,
            action,
            cancellationToken);
    }

    private async Task<SafeWriteReviewResultDto> ReviewWithReadbackAsync(
        MobileOfflineSyncRequest record,
        SafeWriteReviewRequest request,
        Guid reviewedByUserId,
        bool allowRetryWhenMissing,
        string action,
        CancellationToken cancellationToken)
    {
        var previous = Snapshot(record);

        if (record.Status == MobileOfflineSyncRequestStatus.Completed)
        {
            return await CompleteReviewAsync(
                record, previous, action, "AlreadyCompleted", request.Reason, reviewedByUserId,
                "Kayit zaten tamamlanmis.", DeserializeStoredResult(record.ResponsePayload), cancellationToken);
        }

        if (record.Status == MobileOfflineSyncRequestStatus.Cancelled)
        {
            return await CompleteReviewAsync(
                record, previous, action, "AlreadyCancelled", request.Reason, reviewedByUserId,
                "Iptal edilmis kayit yeniden dogrulanamaz veya tekrar acilamaz.", result: null, cancellationToken);
        }

        record.PrepareForManualRecoveryCheck(
            OperationConflictErrorCodes.SafeWriteReviewChecking,
            CheckingMessage,
            clock.UtcNow);
        await authDbContext.SaveChangesAsync(cancellationToken);

        SafeWriteRecoveryResult? recovery = null;
        try
        {
            recovery = await recoveryDispatcher.RecoverAsync(
                record.OperationCode,
                record.WarehouseNo,
                record.RequestedByUserId,
                Guid.Parse(record.ClientRequestId),
                cancellationToken);
        }
        catch (OperationConflictException exception)
        {
            await authDbContext.Entry(record).ReloadAsync(cancellationToken);

            if (exception.ErrorCode == OperationConflictErrorCodes.MikroDocumentIncomplete)
            {
                RestorePreviousReview(record, previous);
                return await CompleteReviewAsync(
                    record, previous, action, "Incomplete", request.Reason, reviewedByUserId,
                    OperationConflictMessages.MikroDocumentIncomplete,
                    result: null,
                    cancellationToken,
                    recoveryState: "Incomplete",
                    recommendedAction: "WaitAndCheck");
            }

            if (exception.Retryable)
            {
                RestorePreviousReview(record, previous);
                return await CompleteReviewAsync(
                    record, previous, action, "CheckFailed", request.Reason, reviewedByUserId,
                    $"Mikro yeniden dogrulamasi kesinlestirilemedi: {OperationConflictMessages.Resolve(exception.ErrorCode, exception.Message) ?? exception.Message}",
                    result: null,
                    cancellationToken,
                    recoveryState: "CheckFailed",
                    recommendedAction: "WaitAndCheck");
            }

            return await CompleteReviewAsync(
                record, previous, action, "StillNeedsReview", request.Reason, reviewedByUserId,
                OperationConflictMessages.Resolve(exception.ErrorCode, exception.Message) ?? exception.Message,
                result: null,
                cancellationToken,
                recoveryState: "Mismatch",
                recommendedAction: "NeedsReview");
        }
        catch (Exception exception)
        {
            await authDbContext.Entry(record).ReloadAsync(cancellationToken);
            RestorePreviousReview(record, previous);
            return await CompleteReviewAsync(
                record, previous, action, "CheckFailed", request.Reason, reviewedByUserId,
                $"Mikro yeniden dogrulamasi tamamlanamadi: {exception.GetBaseException().Message}",
                result: null,
                cancellationToken,
                recoveryState: "CheckFailed",
                recommendedAction: "WaitAndCheck");
        }

        await authDbContext.Entry(record).ReloadAsync(cancellationToken);
        if (record.Status == MobileOfflineSyncRequestStatus.Completed)
        {
            return await CompleteReviewAsync(
                record, previous, action, "Completed", request.Reason, reviewedByUserId,
                "Mikro belgesi istek icerigiyle tam eslesti; kayit tamamlandi.", recovery.Result, cancellationToken);
        }

        var documentWasNotFound =
            record.ErrorCode == OperationConflictErrorCodes.SafeWriteReviewChecking &&
            recovery.Status != "Completed" &&
            recovery.Result is null;

        if (allowRetryWhenMissing && documentWasNotFound)
        {
            record.AllowRetryAfterManualReview(
                OperationConflictErrorCodes.SafeWriteRetryAllowed,
                OperationConflictMessages.SafeWriteRetryAllowed,
                clock.UtcNow);
            return await CompleteReviewAsync(
                record, previous, action, "RetryAllowed", request.Reason, reviewedByUserId,
                "Mikro'da belge bulunamadi; yalniz ayni payload ve ayni clientRequestId ile tekrar denemeye izin verildi.",
                result: null,
                cancellationToken,
                recoveryState: "Missing",
                recommendedAction: "RetrySameRequest");
        }

        RestorePreviousReview(record, previous);
        var outcome = documentWasNotFound ? "NotFound" : "StillNeedsReview";
        var message = documentWasNotFound
            ? "Mikro'da belge bulunamadi; kayit tekrar acilmadi. Yetkili gerekirse kontrollu Tekrar Ac aksiyonunu kullanabilir."
            : "Mikro belgesi istekle eslesmedi veya kontrol kesinlesmedi; kayit incelemede kaldi.";
        return await CompleteReviewAsync(
            record, previous, action, outcome, request.Reason, reviewedByUserId, message,
            result: null,
            cancellationToken,
            recoveryState: documentWasNotFound ? "Missing" : "Mismatch",
            recommendedAction: "NeedsReview");
    }

    private async Task<SafeWriteReviewResultDto> CompleteReviewAsync(
        MobileOfflineSyncRequest record,
        ReviewSnapshot previous,
        string action,
        string outcome,
        string reason,
        Guid reviewedByUserId,
        string message,
        object? result,
        CancellationToken cancellationToken,
        string? recoveryState = null,
        string? recommendedAction = null)
    {
        authDbContext.SafeWriteReviewAudits.Add(new SafeWriteReviewAudit(
            Guid.NewGuid(),
            record.Id,
            action,
            outcome,
            reason,
            reviewedByUserId,
            clock.UtcNow,
            previous.Status,
            previous.ErrorCode,
            record.Status.ToString(),
            record.ErrorCode,
            message));
        await authDbContext.SaveChangesAsync(cancellationToken);

        var clientDecision = SafeWriteClientContract.Resolve(
            record.Status.ToString(),
            record.ErrorCode,
            record.Retryable);

        return new SafeWriteReviewResultDto(
            action,
            outcome,
            message,
            MapMonitoringItem(record),
            result,
            recoveryState ?? clientDecision.RecoveryState,
            recommendedAction ?? clientDecision.RecommendedAction);
    }

    private async Task<MobileOfflineSyncRequest> GetRecordAsync(Guid id, CancellationToken cancellationToken) =>
        await authDbContext.MobileOfflineSyncRequests
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("Safe write record was not found.");

    private void RestorePreviousReview(MobileOfflineSyncRequest record, ReviewSnapshot previous) =>
        record.RestoreManualReview(
            previous.ErrorCode ?? OperationConflictErrorCodes.MikroDocumentContentMismatch,
            previous.ErrorMessage ?? OperationConflictMessages.MikroDocumentContentMismatch,
            clock.UtcNow);

    private SafeWriteMonitoringItemDto MapMonitoringItem(MobileOfflineSyncRequest item)
    {
        var referenceTime = item.UpdatedAtUtc ?? item.CreatedAtUtc;
        var ageSeconds = Math.Max(0, (long)(clock.UtcNow - DateTime.SpecifyKind(referenceTime, DateTimeKind.Utc)).TotalSeconds);
        return new SafeWriteMonitoringItemDto(
            item.Id,
            item.OperationCode,
            item.RequestedByUserId,
            item.WarehouseNo,
            Guid.Parse(item.ClientRequestId),
            item.Status.ToString(),
            item.ErrorCode,
            item.Retryable,
            OperationConflictMessages.Resolve(item.ErrorCode, item.ErrorMessage),
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.CompletedAtUtc,
            ageSeconds);
    }

    private static object? DeserializeStoredResult(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        return System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(payload);
    }

    private static ReviewSnapshot Snapshot(MobileOfflineSyncRequest record) =>
        new(record.Status.ToString(), record.ErrorCode, record.ErrorMessage);

    private sealed record ReviewSnapshot(string Status, string? ErrorCode, string? ErrorMessage);
}
