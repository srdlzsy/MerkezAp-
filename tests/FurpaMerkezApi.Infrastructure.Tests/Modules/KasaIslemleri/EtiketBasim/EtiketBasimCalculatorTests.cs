using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBasim;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.EtiketBasim;

public sealed class EtiketBasimCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsNetAverageAndLabelBarcode()
    {
        var result = EtiketBasimCalculator.Calculate(
            new EtiketBasimCalculationRequest(
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
        var result = EtiketBasimCalculator.Calculate(
            new EtiketBasimCalculationRequest(
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
            EtiketBasimCalculator.Calculate(
                new EtiketBasimCalculationRequest(
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
        var result = EtiketBasimCalculator.BuildPrintableLabelBarcode("123456708300");

        Assert.Equal("1234567083001", result);
    }

    [Theory]
    [InlineData("rehinli", "REH\u0130NL\u0130")]
    [InlineData("REHINLI", "REH\u0130NL\u0130")]
    [InlineData("rehinsiz", "REH\u0130NS\u0130Z")]
    [InlineData("REHINSIZ", "REH\u0130NS\u0130Z")]
    public void NormalizeCaseType_ReturnsCanonicalCaseType(string value, string expected)
    {
        var result = EtiketBasimCalculator.NormalizeCaseType(value);

        Assert.Equal(expected, result);
    }
}