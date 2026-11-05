using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationWorkerTests
{
    [Fact]
    public void ResolveJobTimeoutMinutesUsesTaskOverride()
    {
        var options = new AxataSynchronizationOptions
        {
            JobTimeoutMinutes = 10,
            Tasks = new Dictionary<string, AxataSynchronizationTaskOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["issued-warehouse-order-sync"] = new() { JobTimeoutMinutes = 30 }
            }
        };

        var result = AxataSynchronizationWorker.ResolveJobTimeoutMinutes(
            options,
            "ISSUED-WAREHOUSE-ORDER-SYNC");

        Assert.Equal(30, result);
    }

    [Fact]
    public void ResolveJobTimeoutMinutesFallsBackToGlobalTimeout()
    {
        var options = new AxataSynchronizationOptions { JobTimeoutMinutes = 12 };

        var result = AxataSynchronizationWorker.ResolveJobTimeoutMinutes(
            options,
            "product-master-sync");

        Assert.Equal(12, result);
    }
}
