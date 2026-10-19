using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.VeritabaniIzleme;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class VeritabaniIzlemePermissionTests
{
    [Fact]
    public void PermissionCatalog_SeparatesReadAndSessionTerminationPermissions()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "ayar-islemleri" &&
                definition.MenuCode == "veritabani-izleme")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["detail", "list", "manage", "resolve-safe-write", "terminate-session"], actions);
    }

    [Theory]
    [InlineData(nameof(VeritabaniIzlemeController.Overview), "ayar-islemleri.veritabani-izleme.list")]
    [InlineData(nameof(VeritabaniIzlemeController.Snapshot), "ayar-islemleri.veritabani-izleme.list")]
    [InlineData(nameof(VeritabaniIzlemeController.Workers), "ayar-islemleri.veritabani-izleme.list")]
    [InlineData(nameof(VeritabaniIzlemeController.Incidents), "ayar-islemleri.veritabani-izleme.detail")]
    [InlineData(nameof(VeritabaniIzlemeController.SafeWriteReviewHistory), "ayar-islemleri.veritabani-izleme.detail")]
    [InlineData(nameof(VeritabaniIzlemeController.RecheckSafeWrite), "ayar-islemleri.veritabani-izleme.resolve-safe-write")]
    [InlineData(nameof(VeritabaniIzlemeController.AllowSafeWriteRetry), "ayar-islemleri.veritabani-izleme.resolve-safe-write")]
    [InlineData(nameof(VeritabaniIzlemeController.CancelSafeWrite), "ayar-islemleri.veritabani-izleme.resolve-safe-write")]
    [InlineData(nameof(VeritabaniIzlemeController.TerminationHistory), "ayar-islemleri.veritabani-izleme.detail")]
    [InlineData(nameof(VeritabaniIzlemeController.Terminate), "ayar-islemleri.veritabani-izleme.terminate-session")]
    [InlineData(nameof(VeritabaniIzlemeController.RollbackStatus), "ayar-islemleri.veritabani-izleme.detail")]
    public void ControllerActions_UseExpectedPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(VeritabaniIzlemeController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }
}
