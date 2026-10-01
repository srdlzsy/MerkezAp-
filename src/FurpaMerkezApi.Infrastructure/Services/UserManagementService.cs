using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class UserManagementService(
    AuthDbContext dbContext,
    IClock clock,
    IPasswordHasher passwordHasher,
    IMemoryCache cache) : IUserManagementService
{
    public async Task<IReadOnlyCollection<UserDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = await QueryUsers()
            .AsNoTracking()
            .OrderBy(user => user.Username)
            .ToArrayAsync(cancellationToken);

        return users.Select(user => user.ToDto()).ToArray();
    }

    public async Task<UserDto> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await QueryUsers()
            .AsNoTracking()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId, cancellationToken);

        return user?.ToDto() ?? throw new KeyNotFoundException("User was not found.");
    }

    public async Task<UserDto> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await QueryUsers()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User was not found.");
        }

        var normalizedUsername = NormalizeLookup(request.Username);
        var normalizedEmail = NormalizeLookup(request.Email);

        if (await dbContext.Users.AnyAsync(
                currentUser => currentUser.Id != userId && currentUser.NormalizedUsername == normalizedUsername,
                cancellationToken))
        {
            throw new InvalidOperationException("Username already exists.");
        }

        if (await dbContext.Users.AnyAsync(
                currentUser => currentUser.Id != userId && currentUser.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var now = clock.UtcNow;
        user.RenameUsername(request.Username, now);
        user.UpdateProfile(
            request.Email,
            request.FirstName,
            request.LastName,
            request.WarehouseNo,
            request.WarehouseName,
            request.IsActive,
            now);

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var normalizedPassword = request.NewPassword.Trim();

            if (normalizedPassword.Length < 6)
            {
                throw new ArgumentException("New password must be at least 6 characters.", nameof(request.NewPassword));
            }

            user.ChangePassword(passwordHasher.Hash(normalizedPassword), now);

            var activeRefreshTokens = await dbContext.RefreshTokens
                .Where(token =>
                    token.UserId == userId &&
                    token.RevokedAtUtc == null &&
                    token.ExpiresAtUtc > now)
                .ToArrayAsync(cancellationToken);

            foreach (var refreshToken in activeRefreshTokens)
            {
                refreshToken.Revoke(now);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetEntityByIdAsync(userId, cancellationToken)).ToDto();
    }

    public async Task<UserDto> AssignRolesAsync(Guid userId, AssignUserRolesRequest request, CancellationToken cancellationToken)
    {
        var user = await QueryUsers()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User was not found.");
        }

        var roleIds = request.RoleIds
            .Where(roleId => roleId != Guid.Empty)
            .Distinct()
            .ToArray();

        var roles = await dbContext.Roles
            .Where(role => roleIds.Contains(role.Id) && role.IsActive)
            .Select(role => role.Id)
            .ToArrayAsync(cancellationToken);

        if (roles.Length != roleIds.Length)
        {
            throw new ArgumentException("One or more roles are invalid or inactive.", nameof(request.RoleIds));
        }

        dbContext.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles.Clear();

        foreach (var roleId in roleIds)
        {
            user.UserRoles.Add(new AppUserRole(user.Id, roleId, clock.UtcNow));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetEntityByIdAsync(userId, cancellationToken)).ToDto();
    }

    public async Task<IReadOnlyCollection<UserClientRoleDto>> GetClientRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            throw new KeyNotFoundException("User was not found.");
        }

        return await QueryClientRoles(userId)
            .AsNoTracking()
            .OrderBy(mapping => mapping.ClientType)
            .ThenBy(mapping => mapping.Role.Name)
            .Select(mapping => new UserClientRoleDto(
                mapping.ClientType,
                mapping.RoleId,
                mapping.Role.Name))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserClientRoleDto>> AssignClientRolesAsync(
        Guid userId,
        AssignUserClientRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationClientTypes.IsSupported(request.ClientType))
        {
            throw new ArgumentException("Client type must be 'web' or 'terminal'.", nameof(request.ClientType));
        }

        var clientType = AuthenticationClientTypes.Normalize(request.ClientType);
        var user = await dbContext.Users
            .Include(currentUser => currentUser.ClientRoles)
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User was not found.");
        }

        var roleIds = request.RoleIds
            .Where(roleId => roleId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (roleIds.Length == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(request.RoleIds));
        }

        var activeRoleIds = await dbContext.Roles
            .Where(role => roleIds.Contains(role.Id) && role.IsActive)
            .Select(role => role.Id)
            .ToArrayAsync(cancellationToken);

        if (activeRoleIds.Length != roleIds.Length)
        {
            throw new ArgumentException("One or more roles are invalid or inactive.", nameof(request.RoleIds));
        }

        var existingMappings = user.ClientRoles
            .Where(mapping => string.Equals(mapping.ClientType, clientType, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        dbContext.UserClientRoles.RemoveRange(
            existingMappings.Where(mapping => !roleIds.Contains(mapping.RoleId)));

        var existingRoleIds = existingMappings
            .Select(mapping => mapping.RoleId)
            .ToHashSet();

        foreach (var roleId in roleIds.Where(roleId => !existingRoleIds.Contains(roleId)))
        {
            user.ClientRoles.Add(new AppUserClientRole(user.Id, clientType, roleId, clock.UtcNow));
        }

        var now = clock.UtcNow;
        var activeRefreshTokens = await dbContext.RefreshTokens
            .Where(token =>
                token.UserId == userId &&
                token.ClientType == clientType &&
                token.RevokedAtUtc == null &&
                token.ExpiresAtUtc > now)
            .ToArrayAsync(cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.Revoke(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(SessionAccessProfileCacheKeys.Create(userId, clientType));
        return await GetClientRolesAsync(userId, cancellationToken);
    }

    private IQueryable<AppUser> QueryUsers() =>
        dbContext.Users
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission);

    private IQueryable<AppUserClientRole> QueryClientRoles(Guid userId) =>
        dbContext.UserClientRoles
            .Include(mapping => mapping.Role)
            .Where(mapping => mapping.UserId == userId);

    private async Task<AppUser> GetEntityByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await QueryUsers()
            .AsNoTracking()
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId, cancellationToken);

        return user ?? throw new KeyNotFoundException("User was not found.");
    }

    private static string NormalizeLookup(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        return value.Trim().ToUpperInvariant();
    }
}
