using FurpaMerkezApi.Application.Security;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.OperasyonIslemleri.FirmaEvrakTakibi;

public sealed class FirmaEvrakTakibiPermissionTests
{
    [Fact]
    public void PermissionCatalog_AddsCompanyDocumentTrackingPermissions()
    {
        var actions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "operasyon-islemleri" &&
                definition.MenuCode == "firma-evrak-takibi")
            .Select(definition => definition.ActionCode)
            .OrderBy(action => action)
            .ToArray();

        Assert.Equal(["all-warehouses", "list", "page"], actions);
    }
}
