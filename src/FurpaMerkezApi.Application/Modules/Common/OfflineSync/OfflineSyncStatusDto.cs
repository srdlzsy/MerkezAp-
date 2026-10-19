using FurpaMerkezApi.Application.Common.Errors;

namespace FurpaMerkezApi.Application.Modules.Common.OfflineSync;

public sealed record OfflineSyncStatusDto<TResponse>(
    Guid ClientRequestId,
    string OperationCode,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorMessage,
    string? ErrorCode,
    bool? Retryable,
    int? RetryAfterSeconds,
    TResponse? Result,
    string RecoveryState,
    string RecommendedAction);

public static class SafeWriteClientContract
{
    public static (string RecoveryState, string RecommendedAction) Resolve(
        string status,
        string? errorCode,
        bool? retryable)
    {
        if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return ("ExactMatch", "Completed");
        }

        if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            errorCode == OperationConflictErrorCodes.SafeWriteCancelled)
        {
            return ("Cancelled", "ClearCancelled");
        }

        return errorCode switch
        {
            OperationConflictErrorCodes.MikroWriteQueueBusy => ("NotStarted", "RetrySameRequest"),
            OperationConflictErrorCodes.SafeWriteRetryAllowed => ("Missing", "RetrySameRequest"),
            OperationConflictErrorCodes.MikroDocumentIncomplete => ("Incomplete", "WaitAndCheck"),
            OperationConflictErrorCodes.MikroDocumentContentMismatch => ("Mismatch", "RecheckExisting"),
            OperationConflictErrorCodes.MikroDocumentIdentityConflict => ("NotStarted", "EditAndRetry"),
            OperationConflictErrorCodes.MikroOfficialDocumentAlreadyReceived => ("ExistingDocument", "RefreshList"),
            OperationConflictErrorCodes.WarehouseOrderLineClosed => ("StaleReference", "RefreshList"),
            OperationConflictErrorCodes.CreateReferenceNotFound => ("NotStarted", "EditAndRetry"),
            OperationConflictErrorCodes.MikroBusinessRuleRejected => ("NotStarted", "EditAndRetry"),
            OperationConflictErrorCodes.ClientRequestPayloadMismatch => ("RequestChanged", "RestoreOriginalRequest"),
            OperationConflictErrorCodes.SafeWriteReviewChecking => ("Checking", "WaitAndCheck"),
            OperationConflictErrorCodes.MikroWriteInProgress => ("Checking", "WaitAndCheck"),
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed => ("Unknown", "WaitAndCheck"),
            _ when string.Equals(status, "Processing", StringComparison.OrdinalIgnoreCase) =>
                ("Checking", "WaitAndCheck"),
            _ when retryable == true => ("Unknown", "WaitAndCheck"),
            _ => ("Unknown", "NeedsReview")
        };
    }
}
