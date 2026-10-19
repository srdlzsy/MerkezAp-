using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.ManavMalKabulVeEtiket;
using FurpaMerkezApi.WebApi.Controllers.Modules.MalKabulIslemleri.FirmaMalKabulleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.VerilenDepoSiparisleri;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Infrastructure;

public sealed class CreateQuantityLimitContractTests
{
    [Theory]
    [InlineData(100_000d, true)]
    [InlineData(100_000.000001d, false)]
    public void WarehouseOrderQuantity_UsesCommonCreateLimit(double quantity, bool expectedValid)
    {
        var request = new CreateIssuedWarehouseOrderLineHttpRequest
        {
            StockCode = "STK-1",
            Quantity = quantity
        };

        Assert.Equal(expectedValid, IsValid(request));
    }

    [Theory]
    [InlineData(100_000d, true)]
    [InlineData(100_000.000001d, false)]
    public void CompanyReceivingQuantity_UsesCommonCreateLimit(double quantity, bool expectedValid)
    {
        var request = new CreateCompanyReceivingLineHttpRequest
        {
            StockCode = "STK-1",
            Quantity = quantity
        };

        Assert.Equal(expectedValid, IsValid(request));
    }

    [Theory]
    [InlineData("100000", true)]
    [InlineData("100000.000001", false)]
    public void GreenGrocerReceivingQuantity_UsesCommonCreateLimit(string quantity, bool expectedValid)
    {
        var request = new ManavMalKabulVeEtiketCreateMicroGoodsReceiptLineHttpRequest
        {
            StockCode = "STK-1",
            Quantity = decimal.Parse(quantity, System.Globalization.CultureInfo.InvariantCulture)
        };

        Assert.Equal(expectedValid, IsValid(request));
    }

    private static bool IsValid(object request)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true);
    }
}
