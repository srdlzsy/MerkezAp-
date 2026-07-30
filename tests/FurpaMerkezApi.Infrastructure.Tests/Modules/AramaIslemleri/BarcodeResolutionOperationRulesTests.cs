using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ResolveBarcode;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AramaIslemleri;

public sealed class BarcodeResolutionOperationRulesTests
{
    [Fact]
    public void ShouldEnforceTargetWarehouse_ReturnsFalse_ForShipment()
    {
        var result = BarcodeResolutionOperationRules.ShouldEnforceTargetWarehouse("shipment");

        Assert.False(result);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("receiving")]
    [InlineData("return")]
    [InlineData(null)]
    public void ShouldEnforceTargetWarehouse_ReturnsTrue_ForNonShipmentOperations(string? operationType)
    {
        var result = BarcodeResolutionOperationRules.ShouldEnforceTargetWarehouse(operationType);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCheckPurchaseRequirement_ReturnsFalse_ForShipmentWithoutSupplier()
    {
        var result = BarcodeResolutionOperationRules.ShouldCheckPurchaseRequirement("shipment", null);

        Assert.False(result);
    }

    [Theory]
    [InlineData("shipment", "CR001")]
    [InlineData("order", null)]
    [InlineData("receiving", null)]
    public void ShouldCheckPurchaseRequirement_ReturnsTrue_WhenSupplierOrPurchaseOperationExists(
        string? operationType,
        string? supplierCode)
    {
        var result = BarcodeResolutionOperationRules.ShouldCheckPurchaseRequirement(operationType, supplierCode);

        Assert.True(result);
    }
}
