using FurpaMerkezApi.Infrastructure.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence;

public static class AuthCatalogSynchronizationExtensions
{
    public static async Task SynchronizePermissionCatalogAsync(
        this AuthDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var changed = false;
        var seededPermissions = AuthSeedData.Permissions
            .ToDictionary(permission => permission.Code, StringComparer.OrdinalIgnoreCase);
        var existingPermissions = (await dbContext.Permissions.ToListAsync(cancellationToken))
            .ToDictionary(permission => permission.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var seededPermission in seededPermissions.Values)
        {
            if (!existingPermissions.TryGetValue(seededPermission.Code, out var existingPermission))
            {
                dbContext.Permissions.Add(new Domain.Entities.AppPermission(
                    seededPermission.Id,
                    seededPermission.Code,
                    seededPermission.Name,
                    seededPermission.Description,
                    seededPermission.CreatedAtUtc));
                changed = true;
                continue;
            }

            if (!string.Equals(existingPermission.Name, seededPermission.Name, StringComparison.Ordinal) ||
                !string.Equals(existingPermission.Description, seededPermission.Description, StringComparison.Ordinal))
            {
                existingPermission.Update(
                    seededPermission.Code,
                    seededPermission.Name,
                    seededPermission.Description,
                    now);
                changed = true;
            }
        }

        var administratorRoleExists = await dbContext.Roles
            .AsNoTracking()
            .AnyAsync(role => role.Id == AuthSeedData.AdministratorRoleId, cancellationToken);

        if (administratorRoleExists)
        {
            var existingAdministratorPermissionIds = await dbContext.RolePermissions
                .Where(rolePermission => rolePermission.RoleId == AuthSeedData.AdministratorRoleId)
                .Select(rolePermission => rolePermission.PermissionId)
                .ToListAsync(cancellationToken);
            var missingPermissionIds = seededPermissions.Values
                .Select(permission => permission.Id)
                .Except(existingAdministratorPermissionIds)
                .ToArray();

            if (missingPermissionIds.Length > 0)
            {
                dbContext.RolePermissions.AddRange(
                    missingPermissionIds.Select(permissionId =>
                        new Domain.Entities.AppRolePermission(
                            AuthSeedData.AdministratorRoleId,
                            permissionId,
                            now)));
                changed = true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
