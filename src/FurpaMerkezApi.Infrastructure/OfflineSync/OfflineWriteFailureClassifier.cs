using FurpaMerkezApi.Application.Common.Errors;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

internal static class OfflineWriteFailureClassifier
{
    internal const string UnconfirmedMessage =
        OperationConflictMessages.MikroWriteOutcomeUnconfirmed;

    internal static OfflineWriteFailure Classify(Exception exception)
    {
        if (exception is OperationConflictException conflict)
        {
            return new OfflineWriteFailure(
                exception.Message,
                conflict.ErrorCode,
                conflict.Retryable);
        }

        if (WriteTimeoutClassifier.IsTimeout(exception))
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
