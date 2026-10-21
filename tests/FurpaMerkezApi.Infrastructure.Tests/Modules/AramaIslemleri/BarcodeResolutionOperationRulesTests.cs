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

    [Theory]
    [InlineData("receiving", null, true)]
    [InlineData("order", null, true)]
    [InlineData("return", "CR001", true)]
    [InlineData("return", null, false)]
    [InlineData("shipment", "CR001", false)]
    public void ShouldEnforcePurchaseRequirement_UsesOperationAndSelectedCompany(
        string? operationType,
        string? supplierCode,
        bool expected)
    {
        var result = BarcodeResolutionOperationRules.ShouldEnforcePurchaseRequirement(
            operationType,
            supplierCode);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldEnforceSalesBlock_ReturnsFalse_ForShipment()
    {
        var result = BarcodeResolutionOperationRules.ShouldEnforceSalesBlock("shipment");

        Assert.False(result);
    }

    [Theory]
    [InlineData("return")]
    [InlineData("waste")]
    public void ShouldEnforceSalesBlock_ReturnsTrue_ForReturnAndWaste(string operationType)
    {
        var result = BarcodeResolutionOperationRules.ShouldEnforceSalesBlock(operationType);

        Assert.True(result);
    }

    [Fact]
    public void SelectStockReference_PrefersExactStockCode_OverAnotherStocksGtin()
    {
        BarcodeStockReference[] references =
        [
            new("015708", null),
            new("013891", "015708")
        ];

        var result = BarcodeResolutionLookupRules.SelectStockReference(references, ["015708"]);

        Assert.NotNull(result);
        Assert.Equal("015708", result.StockCode);
        Assert.Equal("stock-code", result.Source);
    }

    [Fact]
    public void SelectStockReference_UsesGtin_WhenExactStockCodeDoesNotExist()
    {
        BarcodeStockReference[] references = [new("013891", "8693395015708")];

        var result = BarcodeResolutionLookupRules.SelectStockReference(references, ["8693395015708"]);

        Assert.NotNull(result);
        Assert.Equal("013891", result.StockCode);
        Assert.Equal("gtin", result.Source);
    }
}
