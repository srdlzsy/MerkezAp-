using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.OperasyonIslemleri.UrunDagilimlari;

public sealed class UrunDagilimlariPermissionTests
{
    [Fact]
    public void PermissionCatalog_AddsProductDistributionAsOperationMenu()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "operasyon-islemleri" &&
                definition.MenuCode == "urun-dagilimlari")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["create", "delete", "detail", "list", "update"], actions);
    }

    [Theory]
    [InlineData(nameof(UrunDagilimlariController.DistributionCenters), "operasyon-islemleri.urun-dagilimlari.list")]
    [InlineData(nameof(UrunDagilimlariController.List), "operasyon-islemleri.urun-dagilimlari.list")]
    [InlineData(nameof(UrunDagilimlariController.Detail), "operasyon-islemleri.urun-dagilimlari.detail")]
    [InlineData(nameof(UrunDagilimlariController.Proposal), "operasyon-islemleri.urun-dagilimlari.create")]
    [InlineData(nameof(UrunDagilimlariController.Save), "operasyon-islemleri.urun-dagilimlari.create")]
    [InlineData(nameof(UrunDagilimlariController.Update), "operasyon-islemleri.urun-dagilimlari.update")]
    [InlineData(nameof(UrunDagilimlariController.Notify), "operasyon-islemleri.urun-dagilimlari.update")]
    [InlineData(nameof(UrunDagilimlariController.Finalize), "operasyon-islemleri.urun-dagilimlari.update")]
    [InlineData(nameof(UrunDagilimlariController.Delete), "operasyon-islemleri.urun-dagilimlari.delete")]
    public void ControllerActions_UseProductDistributionPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(UrunDagilimlariController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }
}
