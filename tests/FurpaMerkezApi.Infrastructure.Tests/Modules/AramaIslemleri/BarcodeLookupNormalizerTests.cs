using FurpaMerkezApi.Application.Modules.AramaIslemleri.Common;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AramaIslemleri;

public sealed class BarcodeLookupNormalizerTests
{
    [Fact]
    public void Normalize_ReturnsScaleLookupBarcodeAndQuantity_ForVariableWeightBarcode()
    {
        var result = BarcodeLookupNormalizer.Normalize("2700174041103");

        Assert.Equal("2700174041103", result.OriginalBarcode);
        Assert.Equal("2700174", result.LookupBarcode);
        Assert.True(result.IsVariableWeightBarcode);
        Assert.Equal(4.11d, result.EmbeddedQuantity);
        Assert.Equal("KG", result.EmbeddedQuantityUnit);
        Assert.True(result.IsCheckDigitValid);
    }

    [Fact]
    public void GetLookupCandidates_AddsAlternate29Prefix_For27ScaleBarcode()
    {
        var result = BarcodeLookupNormalizer.Normalize("2700740000008");

        Assert.Equal(
            ["2700740", "2900740", "2700740000008"],
            BarcodeLookupNormalizer.GetLookupCandidates(result));
    }

    [Fact]
    public void GetLookupCandidates_AddsAlternate27Prefix_For29ScaleBarcode()
    {
        var result = BarcodeLookupNormalizer.Normalize("2900740000002");

        Assert.Equal(
            ["2900740", "2700740", "2900740000002"],
            BarcodeLookupNormalizer.GetLookupCandidates(result));
    }

    [Fact]
    public void Normalize_KeepsBarcode_ForRegularBarcode()
    {
        var result = BarcodeLookupNormalizer.Normalize("8690000000000");

        Assert.Equal("8690000000000", result.OriginalBarcode);
        Assert.Equal("8690000000000", result.LookupBarcode);
        Assert.False(result.IsVariableWeightBarcode);
        Assert.Null(result.EmbeddedQuantity);
        Assert.Null(result.EmbeddedQuantityUnit);
    }
}
