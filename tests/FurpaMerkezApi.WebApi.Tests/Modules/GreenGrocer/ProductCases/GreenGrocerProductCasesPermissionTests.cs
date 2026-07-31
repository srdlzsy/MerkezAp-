using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.GreenGrocer.ProductCases;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.GreenGrocer.ProductCases;

public sealed class GreenGrocerProductCasesPermissionTests
{
    [Fact]
    public void PermissionCatalog_AddsManagePermissionForProfilePage()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "green-grocer" &&
                definition.MenuCode == "product-case-profiles")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["all-warehouses", "create", "delete", "detail", "list", "manage", "update"], actions);
    }

    [Theory]
    [InlineData(nameof(GreenGrocerProductCasesController.List), "green-grocer.product-case-profiles.list")]
    [InlineData(nameof(GreenGrocerProductCasesController.Detail), "green-grocer.product-case-profiles.detail")]
    [InlineData(nameof(GreenGrocerProductCasesController.Save), "green-grocer.product-case-profiles.update")]
    [InlineData(nameof(GreenGrocerProductCasesController.Delete), "green-grocer.product-case-profiles.delete")]
    [InlineData(nameof(GreenGrocerProductCasesController.ResolutionPreview), "green-grocer.product-case-profiles.list")]
    public void ControllerActions_KeepApiActionPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(GreenGrocerProductCasesController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }
}
