using FurpaMerkezApi.Application.Identity.Contracts;

namespace FurpaMerkezApi.Application.Abstractions.Services;

public interface IPermissionService
{
    Task<IReadOnlyCollection<PermissionDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<PermissionDto> CreateAsync(SavePermissionRequest request, CancellationToken cancellationToken);

    Task<PermissionDto> UpdateAsync(Guid permissionId, SavePermissionRequest request, CancellationToken cancellationToken);
}
