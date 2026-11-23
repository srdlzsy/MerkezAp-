using FurpaMerkezApi.Application.Common.Errors;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class CompanyProductAvailabilityExceptionTests
{
    [Fact]
    public void Constructor_NormalizesProductsAndBuildsActionableMessage()
    {
        var exception = new CompanyProductAvailabilityException(
            "Firma mal kabul",
            106,
            [" 022786 ", "022786", "009999"]);

        Assert.Equal(["009999", "022786"], exception.StockCodes);
        Assert.Contains("pasif veya DLS", exception.Message);
        Assert.Contains("Mikro'ya evrak yazilmadi", exception.Message);
    }
}
