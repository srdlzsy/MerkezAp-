using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri;

public sealed class CentralWarehousePackageQuantityRuleTests
{
    [Theory]
    [InlineData(18d, 9d)]
    [InlineData(12d, -6d)]
    [InlineData(1d, 1d)]
    [InlineData(3.6d, 1.2d)]
    public void Validate_AcceptsPackageMultiples(double quantity, double packageFactor)
    {
        var violation = CentralWarehousePackageQuantityRule.Validate("STK-1", quantity, packageFactor);

        Assert.Null(violation);
    }

    [Fact]
    public void Validate_ReturnsNearestPackageMultiples()
    {
        var violation = CentralWarehousePackageQuantityRule.Validate("STK-1", 10d, 9d);

        Assert.NotNull(violation);
        Assert.Equal(9d, violation.LowerQuantity);
        Assert.Equal(18d, violation.UpperQuantity);
    }

    [Fact]
    public void Validate_OmitsZeroAsLowerSuggestion()
    {
        var violation = CentralWarehousePackageQuantityRule.Validate("STK-1", 1d, 20d);

        Assert.NotNull(violation);
        Assert.Null(violation.LowerQuantity);
        Assert.Equal(20d, violation.UpperQuantity);
    }
}
