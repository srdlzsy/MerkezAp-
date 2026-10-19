namespace FurpaMerkezApi.Application.Common.Errors;

public sealed class OperationConflictException(
    string errorCode,
    string message,
    bool retryable,
    Exception? innerException = null,
    Guid? relatedClientRequestId = null) : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;

    public bool Retryable { get; } = retryable;

    public Guid? RelatedClientRequestId { get; } = relatedClientRequestId;
}

public static class OperationConflictErrorCodes
{
    public const string MikroWriteInProgress = "MIKRO_WRITE_IN_PROGRESS";
    public const string MikroWriteQueueBusy = "MIKRO_WRITE_QUEUE_BUSY";
    public const string MikroWriteOutcomeUnconfirmed = "MIKRO_WRITE_OUTCOME_UNCONFIRMED";
    public const string MikroDocumentIncomplete = "MIKRO_DOCUMENT_INCOMPLETE";
    public const string MikroDocumentContentMismatch = "MIKRO_DOCUMENT_CONTENT_MISMATCH";
    public const string MikroDocumentIdentityConflict = "MIKRO_DOCUMENT_IDENTITY_CONFLICT";
    public const string MikroOfficialDocumentAlreadyReceived = "MIKRO_OFFICIAL_DOCUMENT_ALREADY_RECEIVED";
    public const string ClientRequestPayloadMismatch = "CLIENT_REQUEST_PAYLOAD_MISMATCH";
    public const string SafeWriteRetryAllowed = "SAFE_WRITE_RETRY_ALLOWED";
    public const string SafeWriteCancelled = "SAFE_WRITE_CANCELLED";
    public const string SafeWriteReviewChecking = "SAFE_WRITE_REVIEW_CHECKING";
}
