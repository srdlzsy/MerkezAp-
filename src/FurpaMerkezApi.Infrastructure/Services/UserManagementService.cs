using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class UserManagementService(AuthDbContext dbContext, IClock clock) : IUserManagementService
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

        user.RenameUsername(request.Username, clock.UtcNow);
        user.UpdateProfile(
            request.Email,
            request.FirstName,
            request.LastName,
            request.WarehouseNo,
            request.WarehouseName,
            request.IsActive,
            clock.UtcNow);

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

    private IQueryable<AppUser> QueryUsers() =>
        dbContext.Users
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission);

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
