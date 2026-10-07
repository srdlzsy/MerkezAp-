using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Authentication;

public sealed record SessionAccessProfile(
    IReadOnlyCollection<string> RoleNames,
    IReadOnlyCollection<AppPermission> Permissions);

public static class SessionAccessProfileCacheKeys
{
    public static string Create(Guid userId, string clientType) =>
        $"permissions:user:{userId:N}:client:{AuthenticationClientTypes.Normalize(clientType)}";
}

public interface ISessionAccessProfileResolver
{
    Task<SessionAccessProfile> ResolveAsync(
        AppUser user,
        string clientType,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<string>> ResolvePermissionCodesAsync(
        Guid userId,
        string clientType,
        CancellationToken cancellationToken);
}

public sealed class SessionAccessProfileResolver(AuthDbContext dbContext) : ISessionAccessProfileResolver
{
    public const string UnifiedWarehouseRoleName = "SubeKullanicisi";

    public Task<SessionAccessProfile> ResolveAsync(
        AppUser user,
        string clientType,
        CancellationToken cancellationToken)
    {
        var assignedRoles = user.UserRoles
            .Select(userRole => userRole.Role)
            .ToArray();
        var activeAssignedRoles = assignedRoles
            .Where(role => role.IsActive)
            .ToArray();

        var normalizedClientType = AuthenticationClientTypes.Normalize(clientType);
        var effectiveRoles = user.ClientRoles
            .Where(mapping => string.Equals(
                mapping.ClientType,
                normalizedClientType,
                StringComparison.OrdinalIgnoreCase))
            .Select(mapping => mapping.Role)
            .Where(role => role.IsActive)
            .ToArray();

        if (effectiveRoles.Length > 0)
        {
            return Task.FromResult(CreateProfile(effectiveRoles));
        }

        var isUnifiedWarehouseUser = assignedRoles.Any(role =>
            IsRole(role.Name, UnifiedWarehouseRoleName));
        var usesClientRoleProfiles = user.ClientRoles.Count > 0;

        return Task.FromResult(CreateProfile(
            isUnifiedWarehouseUser || usesClientRoleProfiles ? [] : activeAssignedRoles));
    }

    public async Task<IReadOnlyCollection<string>> ResolvePermissionCodesAsync(
        Guid userId,
        string clientType,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(currentUser => currentUser.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
            .Include(currentUser => currentUser.ClientRoles)
                .ThenInclude(mapping => mapping.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
            .AsSplitQuery()
            .TagWith("Furpa:Auth:PermissionCodes")
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId && currentUser.IsActive, cancellationToken);

        if (user is null)
        {
            return [];
        }

        var profile = await ResolveAsync(user, clientType, cancellationToken);
        return profile.Permissions
            .Select(permission => permission.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static SessionAccessProfile CreateProfile(IEnumerable<AppRole> roles)
    {
        var activeRoles = roles
            .Where(role => role.IsActive)
            .DistinctBy(role => role.Id)
            .ToArray();

        var roleNames = activeRoles
            .Select(role => role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(roleName => roleName)
            .ToArray();

        var permissions = activeRoles
            .SelectMany(role => role.RolePermissions)
            .Select(rolePermission => rolePermission.Permission)
            .DistinctBy(permission => permission.Id)
            .OrderBy(permission => permission.Code)
            .ToArray();

        return new SessionAccessProfile(roleNames, permissions);
    }

    private static bool IsRole(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
