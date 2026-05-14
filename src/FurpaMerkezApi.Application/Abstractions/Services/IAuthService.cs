using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Application.Identity.Contracts;

namespace FurpaMerkezApi.Application.Abstractions.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken);
}
