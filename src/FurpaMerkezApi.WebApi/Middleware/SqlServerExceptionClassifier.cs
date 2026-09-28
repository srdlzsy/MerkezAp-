using Microsoft.Data.SqlClient;

namespace FurpaMerkezApi.WebApi.Middleware;

internal static class SqlServerExceptionClassifier
{
    internal static bool IsConnectivityFailure(Exception exception)
    {
        if (exception is SqlException sqlException &&
            sqlException.Errors.Cast<SqlError>().Any(error => IsConnectivityErrorNumber(error.Number)))
        {
            return true;
        }

        if (exception is AggregateException aggregateException &&
            aggregateException.InnerExceptions.Any(IsConnectivityFailure))
        {
            return true;
        }

        return exception.InnerException is not null &&
            IsConnectivityFailure(exception.InnerException);
    }

    internal static bool IsConnectivityErrorNumber(int errorNumber) =>
        errorNumber is
            0 or
            2 or
            20 or
            53 or
            64 or
            121 or
            233 or
            4060 or
            10053 or
            10054 or
            10060 or
            10061 or
            11001 or
            10928 or
            10929 or
            40197 or
            40501 or
            40613 or
            49918 or
            49919 or
            49920;
}
