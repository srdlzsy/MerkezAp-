using System.Security.Claims;
using FurpaMerkezApi.WebApi.Security;

namespace FurpaMerkezApi.WebApi.Extensions;

internal static class ClaimsPrincipalExtensions
{
    private const string AdminRoleName = "Admin";
    private const string AdministratorRoleName = "Administrator";

    public static int GetRequiredWarehouseNo(this ClaimsPrincipal user)
    {
        var warehouseNoValue = user.FindFirstValue("warehouse_no");

        if (!int.TryParse(warehouseNoValue, out var warehouseNo))
        {
            throw new UnauthorizedAccessException("Warehouse information was not found on the current user.");
        }

        return warehouseNo;
    }

    public static bool IsAdministrator(this ClaimsPrincipal user) =>
        user.IsInRole(AdminRoleName) || user.IsInRole(AdministratorRoleName);

    public static int ResolveWarehouseNo(this ClaimsPrincipal user, int? requestedWarehouseNo = null)
    {
        var currentWarehouseNo = user.GetRequiredWarehouseNo();

        if (user.IsAdministrator())
        {
            return requestedWarehouseNo ?? currentWarehouseNo;
        }

        EnsureWarehouseAccess(currentWarehouseNo, requestedWarehouseNo);

        return currentWarehouseNo;
    }

    public static int? ResolveWarehouseScope(this ClaimsPrincipal user, int? requestedWarehouseNo = null)
    {
        if (user.IsAdministrator())
        {
            return requestedWarehouseNo;
        }

        return user.ResolveWarehouseNo(requestedWarehouseNo);
    }

    public static Guid GetRequiredUserId(this ClaimsPrincipal user)
    {
        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            throw new UnauthorizedAccessException("User id was not found on the current user.");
        }

        return userId;
    }

    private static void EnsureWarehouseAccess(int currentWarehouseNo, int? requestedWarehouseNo)
    {
        if (requestedWarehouseNo.HasValue && requestedWarehouseNo.Value != currentWarehouseNo)
        {
            throw new ForbiddenAccessException("Current user is not allowed to access the requested warehouse.");
        }
    }
}
