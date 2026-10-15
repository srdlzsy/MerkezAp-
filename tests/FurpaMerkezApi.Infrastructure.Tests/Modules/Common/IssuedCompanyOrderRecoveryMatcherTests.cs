using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.OfflineSync;
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

    [Fact]
    public void MikroApiPayload_WritesClientRequestTraceToEveryLine()
    {
        var clientRequestId = Guid.Parse("33b460c0-a1c3-4f11-a2c6-26c355704ac8");
        var request = Request() with { ClientRequestId = clientRequestId };
        var traceKey = MobileOfflineSyncService.ToTraceKey(clientRequestId);

        var payload = IssuedCompanyOrderMikroApiPayloadFactory.Create(
            request,
            request.Lines,
            customerPaymentPlanNo: 7,
            customerCanBeCalled: true,
            OrderDate,
            DeliveryDate,
            documentSerie: "F110",
            documentOrderNo: 2514,
            traceKey);

        var lines = Assert.Single(payload.evraklar).satirlar;
        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Equal(traceKey, line.sip_eticaret_kanal_kodu));
        Assert.Equal(24, traceKey.Length);
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
