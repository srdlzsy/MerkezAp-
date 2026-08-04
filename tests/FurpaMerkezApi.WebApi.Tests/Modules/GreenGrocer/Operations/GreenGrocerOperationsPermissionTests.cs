using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.GreenGrocer.Operations;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.GreenGrocer.Operations;

public sealed class GreenGrocerOperationsPermissionTests
{
    [Fact]
    public void PermissionCatalog_AddsOperationsPermissions()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "green-grocer" &&
                definition.MenuCode == "operations")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["all-warehouses", "create", "list", "page"], actions);
    }

    [Theory]
    [InlineData(nameof(GreenGrocerOperationsController.Overview), "green-grocer.operations.list")]
    [InlineData(nameof(GreenGrocerOperationsController.PreviewAdjustment), "green-grocer.operations.list")]
    [InlineData(nameof(GreenGrocerOperationsController.ApplyAdjustment), "green-grocer.operations.create")]
    public void ControllerActions_KeepApiActionPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(GreenGrocerOperationsController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }
}
