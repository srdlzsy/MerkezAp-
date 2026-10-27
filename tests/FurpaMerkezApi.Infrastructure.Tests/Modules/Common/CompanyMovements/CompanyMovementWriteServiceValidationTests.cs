using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
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

    [Fact]
    public void NormalizeRequestForKind_ZeroesCompanyReturnPrices()
    {
        var request = CreateRequest(149.50d);

        var normalized = CompanyMovementWriteService.NormalizeRequestForKind(
            request,
            CompanyMovementKind.PurchaseReturn);

        Assert.Equal(149.50d, request.Lines.Single().UnitPrice);
        Assert.Equal(0d, normalized.Lines.Single().UnitPrice);
    }

    [Fact]
    public void NormalizeRequestForKind_PreservesCompanyShipmentPrices()
    {
        var request = CreateRequest(149.50d);

        var normalized = CompanyMovementWriteService.NormalizeRequestForKind(
            request,
            CompanyMovementKind.OutgoingShipment);

        Assert.Same(request, normalized);
        Assert.Equal(149.50d, normalized.Lines.Single().UnitPrice);
    }

    private static CreateCompanyMovementRequest CreateRequest(double unitPrice) =>
        new(
            50,
            "32012755",
            new DateTime(2026, 10, 27),
            new DateTime(2026, 10, 27),
            null,
            null,
            [new CreateCompanyMovementLineRequest("015708", 2d, unitPrice)]);
}
