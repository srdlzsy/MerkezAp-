using Microsoft.Data.SqlClient;

namespace FurpaMerkezApi.Infrastructure.Persistence;

internal static class OptionalAuthTableExceptionClassifier
{
    private const int InvalidObjectNameErrorNumber = 208;

    public static bool IsMissingTable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException &&
                sqlException.Errors.Cast<SqlError>().Any(error => error.Number == InvalidObjectNameErrorNumber))
            {
                return true;
            }
        }

        return false;
    }
}
