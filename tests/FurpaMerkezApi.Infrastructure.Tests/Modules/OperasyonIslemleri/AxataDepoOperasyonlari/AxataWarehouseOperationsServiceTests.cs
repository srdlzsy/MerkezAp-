using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;

public sealed class AxataWarehouseOperationsServiceTests
{
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
}
