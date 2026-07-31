using System.Security.Claims;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FurpaMerkezApi.WebApi.Security;

public sealed class PermissionAuthorizationHandler(
    AuthDbContext dbContext,
    IMemoryCache cache) : AuthorizationHandler<PermissionRequirement>
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (HasAdministratorRole(context.User) || HasPermissionClaim(context.User, requirement.PermissionCode))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var permissionCodes = await cache.GetOrCreateAsync(
            CreateCacheKey(userId),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;

                return await dbContext.UserRoles
                    .AsNoTracking()
                    .Where(userRole =>
                        userRole.UserId == userId &&
                        userRole.User.IsActive &&
                        userRole.Role.IsActive)
                    .SelectMany(userRole => userRole.Role.RolePermissions
                        .Select(rolePermission => rolePermission.Permission.Code))
                    .Distinct()
                    .ToArrayAsync();
            }) ?? [];

        if (permissionCodes.Contains(requirement.PermissionCode, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }

    private static bool HasAdministratorRole(ClaimsPrincipal user) =>
        user.Claims.Any(claim =>
            string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) &&
            AuthorizationConstants.IsAdministratorRole(claim.Value));

    private static bool HasPermissionClaim(ClaimsPrincipal user, string permissionCode) =>
        user.Claims.Any(claim =>
            string.Equals(claim.Type, AuthorizationConstants.PermissionClaimType, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, permissionCode, StringComparison.OrdinalIgnoreCase));

    private static string CreateCacheKey(Guid userId) => $"permissions:user:{userId:N}";
}
