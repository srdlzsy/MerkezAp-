using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.StokAnomaliMerkezi;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.StokAnomaliMerkezi;

public sealed class StockAnomalyCenterServiceTests
{
    [Fact]
    public void ValidateInteractiveScanDateRange_AcceptsMaximumRange()
    {
        var startDate = new DateTime(2026, 11, 1);
        var endDate = startDate.AddDays(StockAnomalyCenterService.MaxInteractiveScanDays - 1);

        StockAnomalyCenterService.ValidateInteractiveScanDateRange(startDate, endDate);
    }

    [Fact]
    public void ValidateInteractiveScanDateRange_RejectsRangeAboveMaximum()
    {
        var startDate = new DateTime(2026, 11, 1);
        var endDate = startDate.AddDays(StockAnomalyCenterService.MaxInteractiveScanDays);

        var exception = Assert.Throws<ArgumentException>(() =>
            StockAnomalyCenterService.ValidateInteractiveScanDateRange(startDate, endDate));

        Assert.Contains(
            StockAnomalyCenterService.MaxInteractiveScanDays.ToString(),
            exception.Message,
            StringComparison.Ordinal);
    }
}
