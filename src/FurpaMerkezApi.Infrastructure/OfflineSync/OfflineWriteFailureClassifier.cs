using FurpaMerkezApi.Application.Common.Errors;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

internal static class OfflineWriteFailureClassifier
{
    internal const string UnconfirmedMessage =
        "The write request was interrupted before its outcome could be confirmed. " +
        "Retry with the same clientRequestId; do not create a new request identity.";

    internal static OfflineWriteFailure Classify(Exception exception)
    {
        if (exception is OperationConflictException conflict)
        {
            return new OfflineWriteFailure(
                exception.Message,
                conflict.ErrorCode,
                conflict.Retryable);
        }

        if (exception is OperationCanceledException ||
            exception.GetBaseException() is OperationCanceledException)
        {
            return new OfflineWriteFailure(
                UnconfirmedMessage,
                OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                Retryable: true);
        }

        return new OfflineWriteFailure(exception.Message, null, null);
    }
}

internal sealed record OfflineWriteFailure(
    string Message,
    string? ErrorCode,
    bool? Retryable);
