using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationImportWindowTests
{
    [Fact]
    public void GetMinimumDate_UsesInclusiveLookbackWindow()
    {
        var options = CreateOptions(importLookbackDays: 14);

        var result = AxataSynchronizationImportWindow.GetMinimumDate(
            options,
            "g02-inbound-delivery-import",
            new DateTime(2026, 10, 21, 18, 30, 0));

        Assert.Equal(new DateTime(2026, 10, 8), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetMinimumDate_DisablesWindow_WhenLookbackIsNotPositive(int importLookbackDays)
    {
        var options = CreateOptions(importLookbackDays);

        var result = AxataSynchronizationImportWindow.GetMinimumDate(
            options,
            "g02-inbound-delivery-import",
            new DateTime(2026, 10, 21));

        Assert.Null(result);
    }

    [Fact]
    public void GetMinimumDate_DisablesWindow_WhenTaskIsMissing()
    {
        var result = AxataSynchronizationImportWindow.GetMinimumDate(
            new AxataSynchronizationOptions(),
            "g02-inbound-delivery-import",
            new DateTime(2026, 10, 21));

        Assert.Null(result);
    }

    private static AxataSynchronizationOptions CreateOptions(int importLookbackDays) =>
        new()
        {
            Tasks = new Dictionary<string, AxataSynchronizationTaskOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["g02-inbound-delivery-import"] = new()
                {
                    ImportLookbackDays = importLookbackDays
                }
            }
        };
}
