using FurpaMerkezApi.Application.Common.Errors;
using Microsoft.EntityFrameworkCore;

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

        if (exception is DbUpdateException)
        {
            return new OfflineWriteFailure(
                UnconfirmedMessage,
                OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                Retryable: true);
        }

        if (exception is KeyNotFoundException)
        {
            return new OfflineWriteFailure(
                OperationConflictMessages.CreateReferenceNotFound,
                OperationConflictErrorCodes.CreateReferenceNotFound,
                Retryable: false);
        }

        if (ContainsMikroBusinessRuleMarker(exception.Message))
        {
            return new OfflineWriteFailure(
                exception.Message,
                OperationConflictErrorCodes.MikroBusinessRuleRejected,
                Retryable: false);
        }

        return new OfflineWriteFailure(exception.Message, null, null);
    }

    private static bool ContainsMikroBusinessRuleMarker(string? message) =>
        !string.IsNullOrWhiteSpace(message) &&
        (message.Contains("Uyari Kodu", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("Uyarı Kodu", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("U17", StringComparison.OrdinalIgnoreCase));
}

internal sealed record OfflineWriteFailure(
    string Message,
    string? ErrorCode,
    bool? Retryable);
