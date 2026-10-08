using FurpaMerkezApi.Application.Security;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.AyarIslemleri.TerminalCihazlari;

public sealed class TerminalCihazlariPermissionTests
{
    [Theory]
    [InlineData("ayar-islemleri.terminal-cihazlari.manage")]
    [InlineData("ayar-islemleri.terminal-cihazlari.list")]
    [InlineData("ayar-islemleri.terminal-cihazlari.detail")]
    [InlineData("ayar-islemleri.terminal-cihazlari.all-warehouses")]
    public void PermissionCatalog_ContainsTerminalInstallationPermissions(string permissionCode)
    {
        Assert.Contains(permissionCode, PermissionCatalog.Codes);
    }
}
