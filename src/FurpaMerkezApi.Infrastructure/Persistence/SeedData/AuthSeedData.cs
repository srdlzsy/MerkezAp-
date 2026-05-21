using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace FurpaMerkezApi.Infrastructure.Persistence.SeedData;

internal static class AuthSeedData
{
    public static readonly DateTime SeededAtUtc = new(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Guid AdministratorRoleId = Guid.Parse("2ffb4f7d-b63d-4b12-8d74-e2a0aee2798a");
    public static readonly Guid AdministratorUserId = Guid.Parse("af8e4919-5d2e-4c3a-981f-bafe1c1988ff");

    public const string AdministratorUsername = "admin";
    public const string AdministratorEmail = "admin@furpamerkez.local";
    public const string AdministratorPasswordHash = "PBKDF2$100000$AAECAwQFBgcICQoLDA0ODw==$FdMPxR1Ml1GQMslxpUzbpxpAI5NoO/6gzn9FA8Rqaio=";

    private static readonly IReadOnlyDictionary<string, Guid> FixedPermissionIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
    {
        [PermissionCodes.RolesManage] = Guid.Parse("119a5e97-4947-4c87-9ffd-2d35e343ef53"),
        [PermissionCodes.PermissionsManage] = Guid.Parse("79925722-5c18-4db4-9c7d-c44d6f6fd779"),
        [PermissionCodes.UsersManage] = Guid.Parse("fdf63a66-e9b4-4ca5-8700-2a6a34231c01"),
        ["kasa-islemleri.etiket-belgeleri.list"] = Guid.Parse("07f77149-c49d-0e3b-f04a-9a24698d1a05"),
        ["kasa-islemleri.etiket-belgeleri.detail"] = Guid.Parse("fbc155f3-f37d-a25e-3a0b-74628238f508"),
        ["kasa-islemleri.etiket-belgeleri.create"] = Guid.Parse("625b8bb0-4009-8ebe-8071-300c19ec095e"),
        ["kasa-islemleri.etiket-belgeleri.update"] = Guid.Parse("ef760b73-ee59-9072-c7b3-f8d7d1efb11d"),
        ["kasa-islemleri.kunye-etiket-yazdirma.list"] = Guid.Parse("f1172502-9cf7-22b2-3362-8dbcfc8f2dcc"),
        ["kasa-islemleri.kunye-etiket-yazdirma.detail"] = Guid.Parse("c80495b3-55cf-b620-df21-3b052d3bfdcb"),
        ["kasa-islemleri.kunye-etiket-yazdirma.create"] = Guid.Parse("f27dae0a-72af-1ecd-5327-eeb1f44ad93a"),
        ["kasa-islemleri.kunye-etiket-yazdirma.update"] = Guid.Parse("91f4518e-007d-ca11-e8b4-03967a2b4624")
    };

    public static IReadOnlyCollection<AppPermission> Permissions { get; } =
        PermissionCatalog.Definitions
            .Select(definition => new AppPermission(
                GetPermissionId(definition.Code),
                definition.Code,
                definition.Name,
                definition.Description,
                SeededAtUtc))
            .ToArray();

    public static AppRole AdministratorRole { get; } =
        new(AdministratorRoleId, "Administrator", "System administrator role with all permissions.", true, SeededAtUtc);

    public static object AdministratorUser { get; } = new
    {
        Id = AdministratorUserId,
        Username = AdministratorUsername,
        NormalizedUsername = AdministratorUsername.ToUpperInvariant(),
        Email = AdministratorEmail,
        NormalizedEmail = AdministratorEmail.ToUpperInvariant(),
        FirstName = "System",
        LastName = "Administrator",
        WarehouseNo = "0",
        WarehouseName = "MERKEZ",
        PasswordHash = AdministratorPasswordHash,
        IsActive = true,
        CreatedAtUtc = SeededAtUtc,
        UpdatedAtUtc = (DateTime?)null
    };

    public static IReadOnlyCollection<AppRolePermission> AdministratorRolePermissions { get; } =
        Permissions
            .Select(permission => new AppRolePermission(AdministratorRoleId, permission.Id, SeededAtUtc))
            .ToArray();

    public static AppUserRole AdministratorUserRole { get; } =
        new(AdministratorUserId, AdministratorRoleId, SeededAtUtc);

    private static Guid GetPermissionId(string code) =>
        FixedPermissionIds.TryGetValue(code, out var permissionId)
            ? permissionId
            : CreateDeterministicGuid($"permission:{code}");

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }
}
