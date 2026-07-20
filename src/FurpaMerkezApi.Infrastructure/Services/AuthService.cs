using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class AuthService(
    AuthDbContext dbContext,
    FurpaDbContext furpaDbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenFactory jwtTokenFactory,
    IClock clock,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeLookup(request.Username);
        var normalizedEmail = NormalizeLookup(request.Email);

        if (await dbContext.Users.AnyAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException("Username already exists.");
        }

        if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var isFirstUser = !await dbContext.Users.AnyAsync(cancellationToken);
        var now = clock.UtcNow;

        var user = new AppUser(
            Guid.NewGuid(),
            request.Username,
            request.Email,
            request.FirstName,
            request.LastName,
            request.WarehouseNo,
            request.WarehouseName,
            passwordHasher.Hash(request.Password),
            true,
            now);

        if (isFirstUser)
        {
            user.UserRoles.Add(new AppUserRole(user.Id, AuthSeedData.AdministratorRoleId, now));
        }

        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var createdUser = await LoadUserAsync(user.Id, cancellationToken);
        return CreateAuthResponse(createdUser);
    }

   public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
{
    var normalizedLookup = NormalizeLookup(request.UsernameOrEmail);

   var user = await dbContext.Users .Include(currentUser => currentUser.UserRoles) 
   .ThenInclude(userRole => userRole.Role)
    .ThenInclude(role => role.RolePermissions) 
    .ThenInclude(rolePermission => rolePermission.Permission) 
    .FirstOrDefaultAsync( currentUser => currentUser.NormalizedUsername == normalizedLookup || currentUser.NormalizedEmail == normalizedLookup, cancellationToken);

    if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
    {
        throw new UnauthorizedAccessException("Username, email or password is invalid.");
    }

    var terminalRoleId = Guid.Parse("3c1daafe-5922-466e-9f79-6d2ca34ce84d");

    var isTerminalUser = user.UserRoles.Any(x => x.RoleId == terminalRoleId);

    if (isTerminalUser)
    {
        var ip = request.IpAddress;

        logger.LogInformation("Validating terminal user login network for user {UserId}.", user.Id);

        if (string.IsNullOrWhiteSpace(ip))
        {
            throw new UnauthorizedAccessException("Terminal kullanıcısı için IP adresi zorunludur.");
        }

        var parts = ip.Split('.');

        if (parts.Length != 4)
        {
            throw new UnauthorizedAccessException("Geçersiz IP adresi.");
        }

        var networkPrefix = $"{parts[0]}.{parts[1]}.{parts[2]}.";

        var branch = await furpaDbContext.BranchDetails
            .FirstOrDefaultAsync(x =>
                x.BranchIpAddress.StartsWith(networkPrefix),
                cancellationToken);

        if (branch is null)
        {
            throw new UnauthorizedAccessException("Bu IP adresi herhangi bir şube ağıyla eşleşmiyor.");
        }

      
        if (!int.TryParse(user.WarehouseNo, out var userWarehouseNo) ||
            userWarehouseNo != branch.BranchNo)
        {
            throw new UnauthorizedAccessException("Bu kullanıcı bu şubeden giriş yapamaz.");
        }
    }

    return CreateAuthResponse(user);
}

    public async Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(userId, cancellationToken);
        return user.ToDto();
    }

    private AuthResponse CreateAuthResponse(AppUser user)
    {
        var token = jwtTokenFactory.Create(user);
        return new AuthResponse(token.AccessToken, token.ExpiresAtUtc, user.ToDto());
    }

    private async Task<AppUser> LoadUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(currentUser => currentUser.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
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
