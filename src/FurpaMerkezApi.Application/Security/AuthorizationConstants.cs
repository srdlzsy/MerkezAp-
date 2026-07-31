namespace FurpaMerkezApi.Application.Security;

public static class AuthorizationConstants
{
    public const string PermissionClaimType = "permission";
    public const string AdministratorRoleName = "Administrator";

    public static bool IsAdministratorRole(string? role) =>
        string.Equals(role, AdministratorRoleName, StringComparison.OrdinalIgnoreCase);

    public static bool ShouldEmitPermissionClaim(string? permissionCode) =>
        !string.IsNullOrWhiteSpace(permissionCode) &&
        (permissionCode.EndsWith(".all-warehouses", StringComparison.OrdinalIgnoreCase) ||
         permissionCode.EndsWith(".list-all", StringComparison.OrdinalIgnoreCase));
}
