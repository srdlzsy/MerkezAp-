using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.Soforler;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.AyarIslemleri.Soforler;

public sealed class SoforlerPermissionTests
{
    [Fact]
    public void PermissionCatalog_AddsManageAndActionPermissionsForDrivers()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "ayar-islemleri" &&
                definition.MenuCode == "soforler")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["all-warehouses", "create", "delete", "detail", "list", "manage", "update"], actions);
    }

    [Theory]
    [InlineData(nameof(SoforlerController.List), "ayar-islemleri.soforler.list")]
    [InlineData(nameof(SoforlerController.Detail), "ayar-islemleri.soforler.detail")]
    [InlineData(nameof(SoforlerController.Create), "ayar-islemleri.soforler.create")]
    [InlineData(nameof(SoforlerController.Update), "ayar-islemleri.soforler.update")]
    [InlineData(nameof(SoforlerController.Delete), "ayar-islemleri.soforler.delete")]
    public void ControllerActions_UseActionPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(SoforlerController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }
}
