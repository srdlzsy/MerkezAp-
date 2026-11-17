using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;

public sealed class AxataWarehouseOperationsServiceTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("all", null)]
    [InlineData("order", "Order")]
    [InlineData("Shipment", "Shipment")]
    [InlineData("receiving", "Receiving")]
    public void NormalizeDocumentType_NormalizesSupportedFilters(string? input, string? expected)
    {
        Assert.Equal(expected, AxataWarehouseOperationsService.NormalizeDocumentType(input));
    }

    [Fact]
    public void NormalizeDocumentType_RejectsUnknownFilter()
    {
        Assert.Throws<ArgumentException>(() =>
            AxataWarehouseOperationsService.NormalizeDocumentType("unknown"));
    }

    [Theory]
    [InlineData("F147/5389", "F147.5389")]
    [InlineData("F147.5389", "F147.5389")]
    [InlineData(" 12345 ", "12345")]
    public void NormalizeOrderSearchTerm_NormalizesOnlyDocumentSeparators(
        string input,
        string expected)
    {
        Assert.Equal(expected, AxataWarehouseOperationsService.NormalizeOrderSearchTerm(input));
    }

    [Theory]
    [InlineData("2026/12345", 2026, 12345)]
    [InlineData("12345", null, 12345)]
    public void TryParseShipmentReference_ParsesIndexedShipmentKeys(
        string input,
        int? expectedYear,
        int expectedShipmentNo)
    {
        var parsed = AxataWarehouseOperationsService.TryParseShipmentReference(
            input,
            out var year,
            out var shipmentNo);

        Assert.True(parsed);
        Assert.Equal(expectedYear, year);
        Assert.Equal(expectedShipmentNo, shipmentNo);
    }

    [Theory]
    [InlineData("F147.5389")]
    [InlineData("2026/ABC")]
    [InlineData("0")]
    public void TryParseShipmentReference_RejectsNonShipmentTerms(string input)
    {
        Assert.False(AxataWarehouseOperationsService.TryParseShipmentReference(
            input,
            out _,
            out _));
    }

    [Theory]
    [InlineData("F147.5389", "F147", 5389)]
    [InlineData("F50-17511", "F50", 17511)]
    [InlineData("f133/6551", "F133", 6551)]
    [InlineData(" F50 17511 ", "F50", 17511)]
    public void TryParseMikroOrderReference_ParsesSerieAndOrderNo(string input, string expectedSerie, int expectedOrderNo)
    {
        Assert.True(AxataWarehouseOperationsService.TryParseMikroOrderReference(input, out var serie, out var orderNo));
        Assert.Equal(expectedSerie, serie);
        Assert.Equal(expectedOrderNo, orderNo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("2026/12345")]
    [InlineData("F50.0")]
    public void TryParseMikroOrderReference_RejectsNonOrderTerms(string? input)
    {
        Assert.False(AxataWarehouseOperationsService.TryParseMikroOrderReference(input, out _, out _));
    }

    [Theory]
    [InlineData(false, 0, 0, 0, 0, "OrderNotFound")]
    [InlineData(true, 5, 0, 5, 1, "Completed")]
    [InlineData(true, 1, 1, 0, 1, "ShipmentExistsOrderOpen")]
    [InlineData(true, 3, 0, 3, 0, "ClosedWithoutShipment")]
    [InlineData(true, 3, 3, 0, 0, "ShipmentPending")]
    public void ResolveMikroDecision_MapsOrderAndShipmentState(
        bool orderFound,
        int lineCount,
        int openLineCount,
        int closedLineCount,
        int shipmentCount,
        string expectedCode)
    {
        var decision = AxataWarehouseOperationsService.ResolveMikroDecision(
            orderFound, lineCount, openLineCount, closedLineCount, shipmentCount);

        Assert.Equal(expectedCode, decision.Code);
        Assert.False(string.IsNullOrWhiteSpace(decision.Text));
    }
}
