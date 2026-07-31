using System.Security.Claims;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Security;

namespace FurpaMerkezApi.WebApi.Extensions;

internal static class ClaimsPrincipalExtensions
{
    private const string AllWarehousesActionCode = "all-warehouses";

    public static int GetRequiredWarehouseNo(this ClaimsPrincipal user)
    {
        var warehouseNoValue = user.FindFirstValue("warehouse_no");

        if (!int.TryParse(warehouseNoValue, out var warehouseNo))
        {
            throw new UnauthorizedAccessException("Warehouse information was not found on the current user.");
        }

        return warehouseNo;
    }

    public static bool HasPermission(this ClaimsPrincipal user, string permissionCode) =>
        user.HasClaim(claim =>
            string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) &&
            AuthorizationConstants.IsAdministratorRole(claim.Value)) ||
        user.HasClaim(claim =>
            string.Equals(claim.Type, AuthorizationConstants.PermissionClaimType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, permissionCode, StringComparison.OrdinalIgnoreCase));

    public static string ToAllWarehousesPermissionCode(string actionPermissionCode)
    {
        if (string.IsNullOrWhiteSpace(actionPermissionCode))
        {
            throw new ArgumentException("Permission code is required.", nameof(actionPermissionCode));
        }

        var lastSeparatorIndex = actionPermissionCode.LastIndexOf(".", StringComparison.Ordinal);

        return lastSeparatorIndex > 0
            ? $"{actionPermissionCode[..lastSeparatorIndex]}.{AllWarehousesActionCode}"
            : $"{actionPermissionCode}.{AllWarehousesActionCode}";
    }

    public static int ResolveWarehouseNo(this ClaimsPrincipal user, int? requestedWarehouseNo = null)
    {
        var currentWarehouseNo = user.GetRequiredWarehouseNo();

        EnsureWarehouseAccess(currentWarehouseNo, requestedWarehouseNo);

        return currentWarehouseNo;
    }

    public static int ResolveWarehouseNo(
        this ClaimsPrincipal user,
        int? requestedWarehouseNo,
        string allWarehousesPermissionCode)
    {
        var currentWarehouseNo = user.GetRequiredWarehouseNo();

        if (user.CanAccessAllWarehouses(allWarehousesPermissionCode))
        {
            return requestedWarehouseNo ?? currentWarehouseNo;
        }

        EnsureWarehouseAccess(currentWarehouseNo, requestedWarehouseNo);

        return currentWarehouseNo;
    }

    public static int ResolveWarehouseNoForPolicy(
        this ClaimsPrincipal user,
        int? requestedWarehouseNo,
        string actionPermissionCode) =>
        user.ResolveWarehouseNo(requestedWarehouseNo, ToAllWarehousesPermissionCode(actionPermissionCode));

    public static int? ResolveWarehouseScope(this ClaimsPrincipal user, int? requestedWarehouseNo = null)
    {
        return user.ResolveWarehouseNo(requestedWarehouseNo);
    }

    public static int? ResolveWarehouseScope(
        this ClaimsPrincipal user,
        int? requestedWarehouseNo,
        string allWarehousesPermissionCode)
    {
        if (user.CanAccessAllWarehouses(allWarehousesPermissionCode))
        {
            return requestedWarehouseNo;
        }

        return user.ResolveWarehouseNo(requestedWarehouseNo);
    }

    public static int? ResolveWarehouseScopeForPolicy(
        this ClaimsPrincipal user,
        int? requestedWarehouseNo,
        string actionPermissionCode) =>
        user.ResolveWarehouseScope(requestedWarehouseNo, ToAllWarehousesPermissionCode(actionPermissionCode));

    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("User id was not found on the current user.");
        }

        return userId;
    }

    private static bool CanAccessAllWarehouses(this ClaimsPrincipal user, string allWarehousesPermissionCode) =>
        user.HasPermission(allWarehousesPermissionCode);

    private static void EnsureWarehouseAccess(int currentWarehouseNo, int? requestedWarehouseNo)
    {
        if (requestedWarehouseNo.HasValue && requestedWarehouseNo.Value != currentWarehouseNo)
        {
            throw new ForbiddenAccessException("Current user is not allowed to access the requested warehouse.");
        }
    }
}
