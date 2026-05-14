using System.Security.Claims;

namespace FurpaMerkezApi.WebApi.Extensions;

internal static class ClaimsPrincipalExtensions
{
    public static int GetRequiredWarehouseNo(this ClaimsPrincipal user)
    {
        var warehouseNoValue = user.FindFirstValue("warehouse_no");

        if (!int.TryParse(warehouseNoValue, out var warehouseNo))
        {
            throw new UnauthorizedAccessException("Warehouse information was not found on the current user.");
        }

        return warehouseNo;
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
}
