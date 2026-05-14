using FurpaMerkezApi.Application.Identity.Contracts;

namespace FurpaMerkezApi.Application.Abstractions.Services;

public interface IUserManagementService
{
    Task<IReadOnlyCollection<UserDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<UserDto> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserDto> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<UserDto> AssignRolesAsync(Guid userId, AssignUserRolesRequest request, CancellationToken cancellationToken);
}
