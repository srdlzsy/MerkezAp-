using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.AramaIslemleri;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.AramaIslemleri;

public sealed class VarYokPermissionTests
{
    [Fact]
    public void PermissionCatalog_AddsVarYokPermissions()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "arama-islemleri" &&
                definition.MenuCode == "var-yok")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["all-warehouses", "list", "page"], actions);
    }

    [Fact]
    public void ProductAvailability_UsesVarYokListPolicy()
    {
        var authorizeAttribute = typeof(AramaIslemleriController)
            .GetMethods()
            .Single(method => method.Name == nameof(AramaIslemleriController.GetProductAvailability))
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("arama-islemleri.var-yok.list", authorizeAttribute.Policy);
    }
}
