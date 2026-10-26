using System.Data.Common;
using System.ServiceModel;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal static class AxataImportFailureClassifier
{
    public static string FormatExceptionMessage(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) &&
                !messages.Contains(current.Message, StringComparer.Ordinal))
            {
                messages.Add(current.Message.Trim());
            }
        }

        return messages.Count == 0
            ? exception.GetType().Name
            : string.Join(" --> ", messages);
    }

    public static bool IsInfrastructureFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or
                HttpRequestException or
                CommunicationException or
                TimeoutException or
                TaskCanceledException)
            {
                return true;
            }
        }

        return false;
    }
}
