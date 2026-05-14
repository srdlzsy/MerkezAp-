using FurpaMerkezApi.Application.Identity.Contracts;

namespace FurpaMerkezApi.Application.Abstractions.Services;

public interface IRoleService
{
    Task<IReadOnlyCollection<RoleDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<RoleDto> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken);

    Task<RoleDto> UpdateAsync(Guid roleId, SaveRoleRequest request, CancellationToken cancellationToken);

    Task<RoleDto> AssignPermissionsAsync(Guid roleId, AssignRolePermissionsRequest request, CancellationToken cancellationToken);
}
