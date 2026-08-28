using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.BanknotTakipleri;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.KasaIslemleri.BanknotTakipleri;

public sealed class BanknotTakipleriPermissionTests
{
    [Theory]
    [InlineData(nameof(BanknotTakipleriController.List), "kasa-islemleri.banknot-takipleri.list")]
    [InlineData(nameof(BanknotTakipleriController.DailySummaryTotal), "kasa-islemleri.banknot-takipleri.list")]
    [InlineData(nameof(BanknotTakipleriController.Detail), "kasa-islemleri.banknot-takipleri.detail")]
    [InlineData(nameof(BanknotTakipleriController.Create), "kasa-islemleri.banknot-takipleri.create")]
    public void Actions_UseExpectedPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(BanknotTakipleriController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }

    [Theory]
    [InlineData("kasa-islemleri.banknot-takipleri.update")]
    [InlineData("kasa-islemleri.banknot-takipleri.delete")]
    public void PermissionCatalog_IncludesMutationPermissions(string permissionCode)
    {
        Assert.Contains(PermissionCatalog.Codes, code => code == permissionCode);
    }
}
