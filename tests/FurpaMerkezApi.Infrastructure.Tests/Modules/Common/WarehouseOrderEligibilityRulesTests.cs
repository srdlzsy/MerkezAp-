using FurpaMerkezApi.Infrastructure.Modules.Common;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class WarehouseOrderEligibilityRulesTests
{
    [Theory]
    [InlineData("order")]
    [InlineData("warehouse-order")]
    [InlineData(" ORDER ")]
    public void IsWarehouseOrderContext_RecognizesSupportedValues(string operationType)
    {
        Assert.True(WarehouseOrderEligibilityRules.IsWarehouseOrderContext(operationType));
    }

    [Fact]
    public void Evaluate_BlocksTargetWarehousePassiveProductWithExplicitReason()
    {
        var decision = WarehouseOrderEligibilityRules.Evaluate(
            targetWarehouseNo: 110,
            sourceWarehouseNo: 50,
            isPassive: true,
            isDelisted: false,
            isOrderBlocked: false,
            isAllowedForSourceWarehouse: true);

        Assert.False(decision.IsEligible);
        Assert.Equal("ProductPassive", decision.Code);
        Assert.Contains("hedef depo 110", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_BlocksProductMissingFromSourceCatalog()
    {
        var decision = WarehouseOrderEligibilityRules.Evaluate(
            targetWarehouseNo: 146,
            sourceWarehouseNo: 50,
            isPassive: false,
            isDelisted: false,
            isOrderBlocked: false,
            isAllowedForSourceWarehouse: false);

        Assert.False(decision.IsEligible);
        Assert.Equal("SourceCatalogUnavailable", decision.Code);
        Assert.Contains("kaynak depo 50", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_AllowsActiveOrderableProduct()
    {
        var decision = WarehouseOrderEligibilityRules.Evaluate(
            targetWarehouseNo: 146,
            sourceWarehouseNo: 50,
            isPassive: false,
            isDelisted: false,
            isOrderBlocked: false,
            isAllowedForSourceWarehouse: true);

        Assert.True(decision.IsEligible);
        Assert.Null(decision.Code);
        Assert.Null(decision.Reason);
    }
}
