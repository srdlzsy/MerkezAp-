using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class PermissionService(AuthDbContext dbContext, IClock clock) : IPermissionService
{
    public async Task<IReadOnlyCollection<PermissionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .OrderBy(permission => permission.Code)
            .ToArrayAsync(cancellationToken);

        return permissions.Select(permission => permission.ToDto()).ToArray();
    }

    public async Task<PermissionDto> CreateAsync(SavePermissionRequest request, CancellationToken cancellationToken)
    {
        var normalizedCode = NormalizeCode(request.Code);

        if (await dbContext.Permissions.AnyAsync(permission => permission.Code == normalizedCode, cancellationToken))
        {
            throw new InvalidOperationException("Permission code already exists.");
        }

        var permission = new AppPermission(Guid.NewGuid(), request.Code, request.Name, request.Description, clock.UtcNow);
        await dbContext.Permissions.AddAsync(permission, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return permission.ToDto();
    }

    public async Task<PermissionDto> UpdateAsync(Guid permissionId, SavePermissionRequest request, CancellationToken cancellationToken)
    {
        var permission = await dbContext.Permissions.FirstOrDefaultAsync(
            currentPermission => currentPermission.Id == permissionId,
            cancellationToken);

        if (permission is null)
        {
            throw new KeyNotFoundException("Permission was not found.");
        }

        var normalizedCode = NormalizeCode(request.Code);

        if (await dbContext.Permissions.AnyAsync(
                currentPermission => currentPermission.Id != permissionId && currentPermission.Code == normalizedCode,
                cancellationToken))
        {
            throw new InvalidOperationException("Permission code already exists.");
        }

        permission.Update(request.Code, request.Name, request.Description, clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return permission.ToDto();
    }

    private static string NormalizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", nameof(value));
        }

        return value.Trim().ToLowerInvariant();
    }
}
