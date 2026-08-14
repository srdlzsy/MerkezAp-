using FurpaMerkezApi.Application.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

public sealed class ManavMalKabulVeEtiketCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsNetAverageAndLabelBarcode()
    {
        var result = ManavMalKabulVeEtiketCalculator.Calculate(
            new ManavMalKabulVeEtiketCalculationRequest(
                100m,
                1.2m,
                10,
                5m,
                "1234567"));

        Assert.Equal(12m, result.CaseTotalTare);
        Assert.Equal(83m, result.NetReceivedWeight);
        Assert.Equal(8.3m, result.AverageCaseWeight);
        Assert.Equal("123456708300", result.LabelBarcodeRaw);
        Assert.Equal("1234567083001", result.LabelBarcode);
        Assert.Equal("EAN13", result.BarcodeSymbology);
    }

    [Fact]
    public void Calculate_UsesDefaultCaseCountAndPalletTare()
    {
        var result = ManavMalKabulVeEtiketCalculator.Calculate(
            new ManavMalKabulVeEtiketCalculationRequest(
                20m,
                1m,
                null,
                null,
                "1234567"));

        Assert.Equal(1m, result.CaseTotalTare);
        Assert.Equal(19m, result.NetReceivedWeight);
        Assert.Equal(19m, result.AverageCaseWeight);
    }

    [Fact]
    public void Calculate_RejectsAverageCaseWeightGreaterThan99()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ManavMalKabulVeEtiketCalculator.Calculate(
                new ManavMalKabulVeEtiketCalculationRequest(
                    120m,
                    0m,
                    1,
                    0m,
                    "1234567")));

        Assert.Contains("99", exception.Message);
    }

    [Fact]
    public void BuildPrintableLabelBarcode_RecalculatesEan13CheckDigit()
    {
        var result = ManavMalKabulVeEtiketCalculator.BuildPrintableLabelBarcode("123456708300");

        Assert.Equal("1234567083001", result);
    }

    [Theory]
    [InlineData("rehinli", "REH\u0130NL\u0130")]
    [InlineData("REHINLI", "REH\u0130NL\u0130")]
    [InlineData("rehinsiz", "REH\u0130NS\u0130Z")]
    [InlineData("REHINSIZ", "REH\u0130NS\u0130Z")]
    public void NormalizeCaseType_ReturnsCanonicalCaseType(string value, string expected)
    {
        var result = ManavMalKabulVeEtiketCalculator.NormalizeCaseType(value);

        Assert.Equal(expected, result);
    }
}