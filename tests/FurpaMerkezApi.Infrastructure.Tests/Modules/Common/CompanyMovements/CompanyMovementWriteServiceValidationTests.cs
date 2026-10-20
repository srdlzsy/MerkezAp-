using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common.CompanyMovements;

public sealed class CompanyMovementWriteServiceValidationTests
{
    [Fact]
    public void EnsurePrimaryUnitPointer_AcceptsPrimaryUnit()
    {
        CompanyMovementWriteService.EnsurePrimaryUnitPointer(1, "lines");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(10)]
    public void EnsurePrimaryUnitPointer_RejectsPackageMultiplierOrAlternativeUnit(int unitPointer)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CompanyMovementWriteService.EnsurePrimaryUnitPointer(unitPointer, "lines"));

        Assert.Equal("lines", exception.ParamName);
        Assert.Contains("Unit multiplier", exception.Message, StringComparison.Ordinal);
    }
}
