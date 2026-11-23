using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.AyarIslemleri.YeniKasaYonetimi;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.AyarIslemleri.YeniKasaYonetimi;

public sealed class YeniKasaYonetimiPermissionTests
{
    public static TheoryData<string> SectionPermissionCodes => new()
    {
        "ayar-islemleri.yeni-kasa-yonetimi.genel-ayarlar",
        "ayar-islemleri.yeni-kasa-yonetimi.odeme-yontemleri",
        "ayar-islemleri.yeni-kasa-yonetimi.yetki-matrisi",
        "ayar-islemleri.yeni-kasa-yonetimi.musteri-ekrani",
        "ayar-islemleri.yeni-kasa-yonetimi.hizli-tuslar"
    };

    public static TheoryData<string, string> ActionSectionPolicies => new()
    {
        { nameof(YeniKasaYonetimiController.GetAppSettings), "ayar-islemleri.yeni-kasa-yonetimi.genel-ayarlar" },
        { nameof(YeniKasaYonetimiController.TriggerSyncPush), "ayar-islemleri.yeni-kasa-yonetimi.genel-ayarlar" },
        { nameof(YeniKasaYonetimiController.GetPaymentMethods), "ayar-islemleri.yeni-kasa-yonetimi.odeme-yontemleri" },
        { nameof(YeniKasaYonetimiController.GetRolePermissionMatrix), "ayar-islemleri.yeni-kasa-yonetimi.yetki-matrisi" },
        { nameof(YeniKasaYonetimiController.GetDisplayContents), "ayar-islemleri.yeni-kasa-yonetimi.musteri-ekrani" },
        { nameof(YeniKasaYonetimiController.GetFastAccessCategories), "ayar-islemleri.yeni-kasa-yonetimi.hizli-tuslar" },
        { nameof(YeniKasaYonetimiController.SearchStocks), "ayar-islemleri.yeni-kasa-yonetimi.hizli-tuslar" }
    };

    [Theory]
    [MemberData(nameof(SectionPermissionCodes))]
    public void PermissionCatalog_ContainsSectionPermission(string permissionCode)
    {
        Assert.Contains(permissionCode, PermissionCatalog.Codes);
    }

    [Theory]
    [MemberData(nameof(ActionSectionPolicies))]
    public void ControllerAction_RequiresItsSectionPermission(string methodName, string sectionPolicy)
    {
        var method = typeof(YeniKasaYonetimiController).GetMethod(methodName);

        Assert.NotNull(method);
        var policies = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .ToArray();

        Assert.Contains(sectionPolicy, policies);
    }
}
