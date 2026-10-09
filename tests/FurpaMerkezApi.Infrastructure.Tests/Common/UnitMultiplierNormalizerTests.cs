using FurpaMerkezApi.Infrastructure.Common;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Common;

public sealed class UnitMultiplierNormalizerTests
{
    [Theory]
    [InlineData(-4d, 4d)]
    [InlineData(4d, 4d)]
    [InlineData(0d, 1d)]
    public void Normalize_ReturnsPositiveMultiplierOrFallback(double value, double expected)
    {
        var actual = UnitMultiplierNormalizer.Normalize(value, 1d);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolvePackageMultiplier_UsesNegativeSecondaryFactorAsPositivePackageSize()
    {
        var actual = UnitMultiplierNormalizer.ResolvePackageMultiplier(1d, "KOLI", -4d);

        Assert.Equal(4d, actual);
    }

    [Fact]
    public void ResolvePackageMultiplier_FallsBackToPrimaryUnitWithoutUsableSecondaryUnit()
    {
        var actual = UnitMultiplierNormalizer.ResolvePackageMultiplier(-1d, string.Empty, 0d);

        Assert.Equal(1d, actual);
    }

    [Theory]
    [InlineData(1, "ADET", 1d, false)]
    [InlineData(1, "ADET", 12d, false)]
    [InlineData(2, "KOLI", 1d, false)]
    [InlineData(2, "", -4d, false)]
    [InlineData(2, "KOLI", -4d, true)]
    [InlineData(3, "PAKET", 12d, true)]
    public void IsPackageUnit_RequiresSecondaryPointerAndUsableMultiplier(
        int unitPointer,
        string unitName,
        double unitMultiplier,
        bool expected)
    {
        var actual = UnitMultiplierNormalizer.IsPackageUnit(unitPointer, unitName, unitMultiplier);

        Assert.Equal(expected, actual);
    }
}
