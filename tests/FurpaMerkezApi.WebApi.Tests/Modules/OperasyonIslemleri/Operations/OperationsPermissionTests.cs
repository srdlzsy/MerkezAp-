using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.OperasyonIslemleri.Operations;

public sealed class OperationsPermissionTests
{
    [Fact]
    public void PermissionCatalog_AddsOperationsPermissions()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "operasyon-islemleri" &&
                definition.MenuCode == "operations")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["all-warehouses", "create", "detail", "list", "page", "update"], actions);
    }

    [Fact]
    public void CustomerFile_KeepsCreatePolicy()
    {
        var authorizeAttribute = typeof(OperationsController)
            .GetMethods()
            .Single(method => method.Name == nameof(OperationsController.CustomerFile))
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("operasyon-islemleri.operations.create", authorizeAttribute.Policy);
    }

    [Fact]
    public void CustomerFile_KeepsCanonicalRoutes()
    {
        var method = typeof(OperationsController)
            .GetMethods()
            .Single(method => method.Name == nameof(OperationsController.CustomerFile));

        var getRoutes = method
            .GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>()
            .Select(attribute => attribute.Template ?? string.Empty)
            .Order()
            .ToArray();

        Assert.Equal(["customerfile", "einvoicevnofile"], getRoutes);
    }
}
