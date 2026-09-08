using FurpaMerkezApi.WebApi.Controllers.Modules.IadeIslemleri.DepoIadeleri;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.IadeIslemleri.DepoIadeleri;

public sealed class DepoIadeleriEligibleProductsControllerTests
{
    [Fact]
    public void ListEligibleProducts_UsesCreatePolicy()
    {
        var method = typeof(DepoIadeleriController)
            .GetMethod(nameof(DepoIadeleriController.ListEligibleProducts))!;

        var authorizeAttribute = method
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("iade-islemleri.giden-depo-iadeleri.create", authorizeAttribute.Policy);
    }

    [Fact]
    public void ListEligibleProducts_UsesCanonicalRoute()
    {
        var method = typeof(DepoIadeleriController)
            .GetMethod(nameof(DepoIadeleriController.ListEligibleProducts))!;

        var route = method
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Single();

        Assert.Equal("iade-edilebilir-urunler", route.Template);
    }

    [Fact]
    public void EligibleProductsRequest_HasNoTakeOrPagingFields()
    {
        var propertyNames = typeof(WarehouseReturnEligibleProductsHttpRequest)
            .GetProperties()
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(["Search", "TargetWarehouseNo", "WarehouseNo"], propertyNames);
    }
}
