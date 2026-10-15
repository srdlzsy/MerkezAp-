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
                OperationConflictMessages.MikroDocumentContentMismatch,
                retryable: false,
                innerException: innerException);
        }

        return new OperationConflictException(
            OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
            OperationConflictMessages.MikroWriteOutcomeUnconfirmed,
            retryable: true,
            innerException: innerException);
    }
}
