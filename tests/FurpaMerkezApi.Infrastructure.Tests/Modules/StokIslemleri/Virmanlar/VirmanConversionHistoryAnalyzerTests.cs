using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.ConversionSuggestion;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.StokIslemleri.Virmanlar;

public sealed class VirmanConversionHistoryAnalyzerTests
{
    [Fact]
    public void Analyze_SelectsDominantTargetAndMultiplier()
    {
        var rows = Enumerable.Range(0, 20)
            .Select(_ => new PairedMovementRow("015733", 2d, 12d))
            .Append(new PairedMovementRow("OTHER", 1d, 3d));

        var result = VirmanConversionHistoryAnalyzer.Analyze(rows);

        Assert.NotNull(result);
        Assert.Equal("015733", result.TargetStockCode);
        Assert.Equal(6d, result.Multiplier);
        Assert.Equal(21, result.SampleCount);
        Assert.Equal(20, result.TargetMatchCount);
        Assert.Equal(20, result.MultiplierMatchCount);
        Assert.Equal(95.24d, result.TargetConfidencePercent);
        Assert.Equal(100d, result.MultiplierConfidencePercent);
    }

    [Fact]
    public void Analyze_MeasuresMultiplierConfidenceWithinDominantTarget()
    {
        var rows = Enumerable.Range(0, 19)
            .Select(_ => new PairedMovementRow("015733", 1d, 6d))
            .Append(new PairedMovementRow("015733", 1d, 5d));

        var result = VirmanConversionHistoryAnalyzer.Analyze(rows);

        Assert.NotNull(result);
        Assert.Equal(100d, result.TargetConfidencePercent);
        Assert.Equal(95d, result.MultiplierConfidencePercent);
        Assert.Equal(6d, result.Multiplier);
    }

    [Fact]
    public void Analyze_IgnoresInvalidRowsAndRoundsEquivalentRatios()
    {
        var rows = new[]
        {
            new PairedMovementRow("015733", 3d, 18d),
            new PairedMovementRow("015733", 0.3d, 1.80000001d),
            new PairedMovementRow("", 1d, 6d),
            new PairedMovementRow("015733", 0d, 6d),
            new PairedMovementRow("015733", 1d, double.NaN)
        };

        var result = VirmanConversionHistoryAnalyzer.Analyze(rows);

        Assert.NotNull(result);
        Assert.Equal(2, result.SampleCount);
        Assert.Equal(6d, result.Multiplier);
        Assert.Equal(100d, result.TargetConfidencePercent);
        Assert.Equal(100d, result.MultiplierConfidencePercent);
    }

    [Fact]
    public void Analyze_ReturnsNullWithoutValidObservation()
    {
        var result = VirmanConversionHistoryAnalyzer.Analyze(
            [new PairedMovementRow(string.Empty, 0d, 0d)]);

        Assert.Null(result);
    }
}
