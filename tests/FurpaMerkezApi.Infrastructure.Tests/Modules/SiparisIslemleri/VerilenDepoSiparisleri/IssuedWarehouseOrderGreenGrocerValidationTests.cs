using FurpaMerkezApi.Application.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.SiparisIslemleri.VerilenDepoSiparisleri;

public sealed class IssuedWarehouseOrderGreenGrocerValidationTests
{
    [Fact]
    public void ManualApprovalRequired_WithoutConfirmation_IsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CreateIssuedWarehouseOrderUseCase.EnsureGreenGrocerManualApprovalConfirmed(
                "016181",
                "MNV ELMA GOLDEN LUX KG",
                requiresManualApproval: true,
                manualApprovalConfirmed: false));

        Assert.Contains("manuel onay", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ManualApprovalRule_AllowsSafeOrConfirmedResolution(
        bool requiresManualApproval,
        bool manualApprovalConfirmed)
    {
        CreateIssuedWarehouseOrderUseCase.EnsureGreenGrocerManualApprovalConfirmed(
            "016181",
            "MNV ELMA GOLDEN LUX KG",
            requiresManualApproval,
            manualApprovalConfirmed);
    }

    [Fact]
    public void QuantityDrift_DirectFallback_DoesNotBlockOrder()
    {
        var rejected = CreateIssuedWarehouseOrderUseCase.ShouldRejectGreenGrocerQuantityDrift(
            GreenGrocerProductCaseModes.ConversionDirectQuantity,
            GreenGrocerProductCaseModes.AverageSourceNone,
            serverQuantity: 5,
            clientQuantity: 75);

        Assert.False(rejected);
    }

    [Fact]
    public void QuantityDrift_RegularCalculation_StillProtectsAgainstStaleValue()
    {
        var rejected = CreateIssuedWarehouseOrderUseCase.ShouldRejectGreenGrocerQuantityDrift(
            GreenGrocerProductCaseModes.ConversionLabelAverageKgPerCase,
            GreenGrocerProductCaseModes.AverageSourceLabelHistory,
            serverQuantity: 50,
            clientQuantity: 75);

        Assert.True(rejected);
    }
}
