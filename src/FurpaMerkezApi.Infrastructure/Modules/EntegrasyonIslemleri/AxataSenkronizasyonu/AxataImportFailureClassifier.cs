using System.Data.Common;
using System.ServiceModel;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal static class AxataImportFailureClassifier
{
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
