namespace FurpaMerkezApi.Application.Common.Errors;

public sealed class OperationConflictException(
    string errorCode,
    string message,
    bool retryable,
    Exception? innerException = null) : InvalidOperationException(message, innerException)
{
    public string ErrorCode { get; } = errorCode;

    public bool Retryable { get; } = retryable;
}

public static class OperationConflictErrorCodes
{
    public const string MikroWriteInProgress = "MIKRO_WRITE_IN_PROGRESS";
    public const string MikroWriteOutcomeUnconfirmed = "MIKRO_WRITE_OUTCOME_UNCONFIRMED";
    public const string MikroDocumentContentMismatch = "MIKRO_DOCUMENT_CONTENT_MISMATCH";
    public const string ClientRequestPayloadMismatch = "CLIENT_REQUEST_PAYLOAD_MISMATCH";
}
