using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationLiveTransportServiceTests
{
    [Fact]
    public void MatchesExistingOutboundOrder_ReturnsTrue_WhenHeaderAndLinesAreIdentical()
    {
        var payload = CreatePayload("3312384204", [(0, "018854", 10d)]);
        var headers = new[]
        {
            new AxataExistingOutboundOrderHeader("01    ", "3312384204   ", "3312384204")
        };
        var lines = new[]
        {
            new AxataExistingOutboundOrderLine("0   ", "018854   ", 10m, "01   ")
        };

        var result = AxataSynchronizationLiveTransportService.MatchesExistingOutboundOrder(
            payload,
            headers,
            lines);

        Assert.True(result);
    }

    [Fact]
    public void MatchesExistingOutboundOrder_ReturnsFalse_WhenDocumentBelongsToAnotherCustomer()
    {
        var payload = CreatePayload("3312384204", [(0, "018854", 10d)]);
        var headers = new[]
        {
            new AxataExistingOutboundOrderHeader("01", "4730004089", "4730004089")
        };
        var lines = new[]
        {
            new AxataExistingOutboundOrderLine("0", "018854", 10m, "01")
        };

        var result = AxataSynchronizationLiveTransportService.MatchesExistingOutboundOrder(
            payload,
            headers,
            lines);

        Assert.False(result);
    }

    [Fact]
    public void MatchesExistingOutboundOrder_ReturnsFalse_WhenAnyLineDiffers()
    {
        var payload = CreatePayload(
            "3312384204",
            [(0, "018854", 10d), (1, "000612", 2d)]);
        var headers = new[]
        {
            new AxataExistingOutboundOrderHeader("01", "3312384204", "3312384204")
        };
        var lines = new[]
        {
            new AxataExistingOutboundOrderLine("0", "018854", 10m, "01"),
            new AxataExistingOutboundOrderLine("1", "000612", 3m, "01")
        };

        var result = AxataSynchronizationLiveTransportService.MatchesExistingOutboundOrder(
            payload,
            headers,
            lines);

        Assert.False(result);
    }

    private static AxataLegacyOutboundOrderPayload CreatePayload(
        string customerCode,
        IReadOnlyCollection<(int LineNo, string StockCode, double Quantity)> lines) =>
        new(
            "A.1979",
            "C02",
            new AxataLegacyOutboundOrderMaster(
                "01",
                "A.1979",
                "01",
                customerCode,
                customerCode,
                "01",
                "0",
                string.Empty,
                "C02",
                "C02"),
            lines.Select(line => new AxataLegacyOutboundOrderLine(
                    "01",
                    "A.1979",
                    line.LineNo,
                    line.StockCode,
                    line.Quantity,
                    "01"))
                .ToArray());
}
