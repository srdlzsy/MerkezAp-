using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class IssuedCompanyOrderRecoveryMatcherTests
{
    private static readonly DateTime OrderDate = new(2026, 10, 15);
    private static readonly DateTime DeliveryDate = new(2026, 10, 16);

    [Fact]
    public void Match_RequiresEveryRequestedLineAndItsContent()
    {
        var request = Request();
        var rows = new[] { Row(0, "STOK-1", 2d), Row(1, "STOK-2", 3d) };

        Assert.Equal(
            IssuedCompanyOrderMatch.Incomplete,
            IssuedCompanyOrderRecoveryMatcher.Match(request, request.Lines.ToArray(), [rows[0]]));
        Assert.Equal(
            IssuedCompanyOrderMatch.Complete,
            IssuedCompanyOrderRecoveryMatcher.Match(request, request.Lines.ToArray(), rows));

        var wrongStock = rows.ToArray();
        wrongStock[1] = wrongStock[1] with { StockCode = "BASKA-STOK" };
        Assert.Equal(
            IssuedCompanyOrderMatch.Mismatch,
            IssuedCompanyOrderRecoveryMatcher.Match(request, request.Lines.ToArray(), wrongStock));
    }

    [Fact]
    public void Match_RejectsSameTotalWithDifferentLineQuantities()
    {
        var request = Request();
        var rows = new[]
        {
            Row(0, "STOK-1", 1d) with { Amount = 10d },
            Row(1, "STOK-2", 4d) with { Amount = 40d }
        };

        Assert.Equal(
            IssuedCompanyOrderMatch.Mismatch,
            IssuedCompanyOrderRecoveryMatcher.Match(request, request.Lines.ToArray(), rows));
    }

    private static CreateIssuedCompanyOrderRequest Request() =>
        new(
            110,
            "320.01.001",
            OrderDate,
            DeliveryDate,
            "Baslik",
            null,
            null,
            null,
            [
                new CreateIssuedCompanyOrderLineRequest("STOK-1", 2d, UnitPrice: 10d),
                new CreateIssuedCompanyOrderLineRequest("STOK-2", 3d, UnitPrice: 10d)
            ]);

    private static IssuedCompanyOrderRecoveryRow Row(int rowNo, string stockCode, double quantity) =>
        new(
            OrderDate,
            DeliveryDate,
            "F110",
            100,
            rowNo,
            110,
            "320.01.001",
            stockCode,
            10d,
            quantity,
            quantity * 10d,
            1,
            "Baslik",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0d,
            false);
}
