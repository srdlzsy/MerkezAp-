using Microsoft.Data.SqlClient;

namespace FurpaMerkezApi.Infrastructure.OfflineSync;

internal static class WriteTimeoutClassifier
{
    internal static bool IsTimeout(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is TimeoutException or OperationCanceledException)
        {
            return true;
        }

        if (exception is SqlException sqlException &&
            sqlException.Errors.Cast<SqlError>().Any(error => IsSqlCommandTimeoutNumber(error.Number)))
        {
            return true;
        }

        if (exception is AggregateException aggregateException)
        {
            return aggregateException.InnerExceptions.Any(IsTimeout);
        }

        return exception.InnerException is not null && IsTimeout(exception.InnerException);
    }

    internal static bool ContainsTimeoutMarker(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("time out", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("timed out", StringComparison.OrdinalIgnoreCase));

    internal static bool IsSqlCommandTimeoutNumber(int errorNumber) => errorNumber == -2;
}
