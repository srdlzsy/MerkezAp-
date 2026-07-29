using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Files;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaSayimlari;

public sealed class GetCashSummaryZReportTotalUseCaseTests
{
    [Theory]
    [InlineData(110, null, 110)]
    [InlineData(110, "", 110)]
    [InlineData(110, "0", 110)]
    [InlineData(1, "F110.1", 110)]
    [InlineData(1, "F110", 110)]
    [InlineData(1, "KS110", 110)]
    [InlineData(1, "ks110", 110)]
    public void ResolveBranchNoForZReport_UsesSupportedSerieFormatsOrWarehouseFallback(
        int warehouseNo,
        string? documentSerie,
        int expectedBranchNo)
    {
        var branchNo = GetCashSummaryZReportTotalUseCase.ResolveBranchNoForZReport(
            warehouseNo,
            documentSerie);

        Assert.Equal(expectedBranchNo, branchNo);
    }
}
