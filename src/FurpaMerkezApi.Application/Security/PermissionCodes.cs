namespace FurpaMerkezApi.Application.Security;

public static class PermissionCodes
{
    public const string RolesManage = "kullanici-islemleri.roller.manage";
    public const string PermissionsManage = "kullanici-islemleri.yetkiler.manage";
    public const string UsersManage = "kullanici-islemleri.kullanicilar.manage";

    public static IReadOnlyCollection<string> All => PermissionCatalog.Codes;
}
