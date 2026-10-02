using FurpaMerkezApi.Application.Common.Errors;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal static class MikroApiCreateConflictFactory
{
    internal static OperationConflictException Create<TResponse>(
        MikroApiResult<TResponse> result,
        Exception innerException)
    {
        if (innerException is OperationConflictException conflict)
        {
            return conflict;
        }

        if (MikroApiWriteAuditService.IsDuplicateDocumentOutcome(result))
        {
            return new OperationConflictException(
                OperationConflictErrorCodes.MikroDocumentContentMismatch,
                "The existing Mikro document does not match the requested document content. Manual review is required; do not retry with a new clientRequestId.",
                retryable: false,
                innerException: innerException);
        }

        return new OperationConflictException(
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            $"Mikro API write outcome could not be confirmed. Do not create a new request; retry or query status with the same clientRequestId. Detail: {result.ErrorMessage}",
            retryable: true,
            innerException: innerException);
    }
}
