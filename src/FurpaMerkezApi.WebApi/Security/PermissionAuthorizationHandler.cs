using System.Security.Claims;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;

namespace FurpaMerkezApi.WebApi.Security;

public sealed class PermissionAuthorizationHandler(
    ISessionAccessProfileResolver sessionAccessProfileResolver,
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

        if (HasAdministratorRole(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var isLegacyUnifiedToken = context.User.IsInRole(SessionAccessProfileResolver.UnifiedWarehouseRoleName);
        if (!isLegacyUnifiedToken && HasPermissionClaim(context.User, requirement.PermissionCode))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return;
        }

        var clientType = AuthenticationClientTypes.Normalize(
            context.User.FindFirstValue("client_type"));
        var permissionCodes = await cache.GetOrCreateAsync(
            SessionAccessProfileCacheKeys.Create(userId, clientType),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await sessionAccessProfileResolver.ResolvePermissionCodesAsync(
                    userId,
                    clientType,
                    CancellationToken.None);
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

}
