using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaSayimlari;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.KasaIslemleri.KasaSayimlari;

public sealed class KasaSayimlariPermissionTests
{
    [Fact]
    public void PermissionCatalog_SplitsCashSummaryViewingAndEntryMenus()
    {
        var kasaSayimlariActions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "kasa-islemleri" &&
                definition.MenuCode == "kasa-sayimlari")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        var icmalKaydiGirisiActions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "kasa-islemleri" &&
                definition.MenuCode == "icmal-kaydi-girisi")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["detail", "list"], kasaSayimlariActions);
        Assert.Equal(["create", "delete", "list", "update"], icmalKaydiGirisiActions);
    }

    [Theory]
    [InlineData(nameof(KasaSayimlariController.Create), "kasa-islemleri.icmal-kaydi-girisi.create")]
    [InlineData(nameof(KasaSayimlariController.UpdateDetails), "kasa-islemleri.icmal-kaydi-girisi.update")]
    [InlineData(nameof(KasaSayimlariController.UpdateBanknotes), "kasa-islemleri.icmal-kaydi-girisi.update")]
    [InlineData(nameof(KasaSayimlariController.Delete), "kasa-islemleri.icmal-kaydi-girisi.delete")]
    public void WriteActions_UseCashSummaryEntryPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(KasaSayimlariController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }

    [Theory]
    [InlineData(nameof(KasaSayimlariController.List), "kasa-islemleri.kasa-sayimlari.list")]
    [InlineData(nameof(KasaSayimlariController.Report), "kasa-islemleri.kasa-sayimlari.list")]
    [InlineData(nameof(KasaSayimlariController.Detail), "kasa-islemleri.kasa-sayimlari.detail")]
    [InlineData(nameof(KasaSayimlariController.DetailLines), "kasa-islemleri.kasa-sayimlari.detail")]
    public void ViewingActions_KeepCashSummaryViewingPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(KasaSayimlariController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }
}
