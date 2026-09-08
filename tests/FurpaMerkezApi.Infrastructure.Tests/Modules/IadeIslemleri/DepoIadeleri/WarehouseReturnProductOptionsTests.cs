using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.EligibleProducts;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.IadeIslemleri.DepoIadeleri;

public sealed class WarehouseReturnProductOptionsTests
{
    [Fact]
    public void Defaults_MapProductSourcesToReturnWarehouses()
    {
        var routes = new WarehouseReturnProductOptions().Routes
            .Select(route => (route.ProductSourceWarehouseNo, route.ReturnWarehouseNo))
            .ToArray();

        Assert.Equal(
            [(50, 51), (53, 53), (55, 55), (56, 56), (58, 58)],
            routes);
    }
}
