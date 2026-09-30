using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.SevkIslemleri.DepolarArasiSevkler;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.SevkIslemleri.DepolarArasiSevkler;

public sealed class OutgoingWarehouseShipmentDeletePermissionTests
{
    private const string DeletePermission = "sevk-islemleri.giden-depolar-arasi-sevkler.delete";

    [Fact]
    public void PermissionCatalog_IncludesDeletePermission()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "sevk-islemleri" &&
                definition.MenuCode == "giden-depolar-arasi-sevkler")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["all-warehouses", "create", "delete", "detail", "list", "page", "update"], actions);
    }

    [Theory]
    [InlineData(nameof(DepolarArasiSevklerController.Delete))]
    [InlineData(nameof(DepolarArasiSevklerController.DeleteOutgoing))]
    public void DeleteActions_RequireDeletePermission(string methodName)
    {
        var authorizeAttribute = typeof(DepolarArasiSevklerController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(DeletePermission, authorizeAttribute.Policy);
    }
}
