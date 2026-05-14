using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class RoleService(AuthDbContext dbContext, IClock clock) : IRoleService
{
    public async Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var roles = await QueryRoles()
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ToArrayAsync(cancellationToken);

        return roles.Select(role => role.ToDto()).ToArray();
    }

    public async Task<RoleDto> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeLookup(request.Name);

        if (await dbContext.Roles.AnyAsync(role => role.Name.ToUpper() == normalizedName, cancellationToken))
        {
            throw new InvalidOperationException("Role name already exists.");
        }

        var role = new AppRole(Guid.NewGuid(), request.Name, request.Description, request.IsActive, clock.UtcNow);

        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetRoleAsync(role.Id, cancellationToken)).ToDto();
    }

    public async Task<RoleDto> UpdateAsync(Guid roleId, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await QueryRoles().FirstOrDefaultAsync(currentRole => currentRole.Id == roleId, cancellationToken);

        if (role is null)
        {
            throw new KeyNotFoundException("Role was not found.");
        }

        var normalizedName = NormalizeLookup(request.Name);

        if (await dbContext.Roles.AnyAsync(
                currentRole => currentRole.Id != roleId && currentRole.Name.ToUpper() == normalizedName,
                cancellationToken))
        {
            throw new InvalidOperationException("Role name already exists.");
        }

        role.Update(request.Name, request.Description, request.IsActive, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetRoleAsync(roleId, cancellationToken)).ToDto();
    }

    public async Task<RoleDto> AssignPermissionsAsync(Guid roleId, AssignRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await QueryRoles().FirstOrDefaultAsync(currentRole => currentRole.Id == roleId, cancellationToken);

        if (role is null)
        {
            throw new KeyNotFoundException("Role was not found.");
        }

        var permissionIds = request.PermissionIds
            .Where(permissionId => permissionId != Guid.Empty)
            .Distinct()
            .ToArray();

        var permissions = await dbContext.Permissions
            .Where(permission => permissionIds.Contains(permission.Id))
            .Select(permission => permission.Id)
            .ToArrayAsync(cancellationToken);

        if (permissions.Length != permissionIds.Length)
        {
            throw new ArgumentException("One or more permissions are invalid.", nameof(request.PermissionIds));
        }

        dbContext.RolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions.Clear();

        foreach (var permissionId in permissionIds)
        {
            role.RolePermissions.Add(new AppRolePermission(role.Id, permissionId, clock.UtcNow));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetRoleAsync(roleId, cancellationToken)).ToDto();
    }

    private IQueryable<AppRole> QueryRoles() =>
        dbContext.Roles
            .Include(role => role.RolePermissions)
                .ThenInclude(rolePermission => rolePermission.Permission);

    private async Task<AppRole> GetRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await QueryRoles()
            .AsNoTracking()
            .FirstOrDefaultAsync(currentRole => currentRole.Id == roleId, cancellationToken);

        return role ?? throw new KeyNotFoundException("Role was not found.");
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
