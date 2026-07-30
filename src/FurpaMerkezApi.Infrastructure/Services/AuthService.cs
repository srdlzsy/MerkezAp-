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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class AuthService(
    AuthDbContext dbContext,
    FurpaDbContext furpaDbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenFactory jwtTokenFactory,
    IClock clock,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly Guid TerminalRoleId = Guid.Parse("3c1daafe-5922-466e-9f79-6d2ca34ce84d");

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

        var user = await dbContext.Users
            .Include(currentUser => currentUser.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.RolePermissions)
                        .ThenInclude(rolePermission => rolePermission.Permission)
            .FirstOrDefaultAsync(
                currentUser =>
                    currentUser.NormalizedUsername == normalizedLookup ||
                    currentUser.NormalizedEmail == normalizedLookup,
                cancellationToken);

        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Username, email or password is invalid.");
        }

        var isTerminalUser = user.UserRoles.Any(userRole => userRole.RoleId == TerminalRoleId);

        if (isTerminalUser)
        {
            await ValidateTerminalUserNetworkAsync(user, request.IpAddress, cancellationToken);
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

    private async Task ValidateTerminalUserNetworkAsync(
        AppUser user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Validating terminal user login network for user {UserId}.", user.Id);

        if (!int.TryParse(user.WarehouseNo, out var userWarehouseNo))
        {
            throw new UnauthorizedAccessException("Kullanici depo numarasi gecersiz.");
        }

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new UnauthorizedAccessException("Terminal kullanicisi icin IP adresi zorunludur.");
        }

        var inboundNetworkPrefix = GetIpv4NetworkPrefix(ipAddress);
        if (inboundNetworkPrefix is null)
        {
            throw new UnauthorizedAccessException("Gecersiz IP adresi.");
        }

        var allowedNetworkBranchNos = GetAllowedNetworkBranchNos(userWarehouseNo);
        var branchIpAddresses = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .Where(item => allowedNetworkBranchNos.Contains(item.BranchNo))
            .Select(item => item.BranchIpAddress)
            .ToArrayAsync(cancellationToken);

        if (branchIpAddresses.Length == 0)
        {
            throw new UnauthorizedAccessException("Kullanici deposu icin sube IP ayarlari bulunamadi.");
        }

        var matchesAllowedNetwork = branchIpAddresses
            .Select(GetIpv4NetworkPrefix)
            .Any(branchNetworkPrefix =>
                string.Equals(inboundNetworkPrefix, branchNetworkPrefix, StringComparison.Ordinal));

        if (!matchesAllowedNetwork)
        {
            throw new UnauthorizedAccessException("Bu kullanici bu subeden giris yapamaz.");
        }
    }

    private int[] GetAllowedNetworkBranchNos(int userWarehouseNo)
    {
        var allowedBranchNos = new HashSet<int> { userWarehouseNo };

        foreach (var group in configuration.GetSection("Auth:TerminalLogin:SharedNetworkWarehouseGroups").GetChildren())
        {
            var groupWarehouseNos = group.GetSection("WarehouseNos").Get<int[]>() ?? [];

            if (!groupWarehouseNos.Contains(userWarehouseNo))
            {
                continue;
            }

            foreach (var groupWarehouseNo in groupWarehouseNos.Where(warehouseNo => warehouseNo > 0))
            {
                allowedBranchNos.Add(groupWarehouseNo);
            }
        }

        return allowedBranchNos.ToArray();
    }

    private static string? GetIpv4NetworkPrefix(string ipAddress)
    {
        var parts = ipAddress.Trim().Split('.', StringSplitOptions.TrimEntries);

        if (parts.Length != 4)
        {
            return null;
        }

        for (var i = 0; i < parts.Length; i++)
        {
            if (!byte.TryParse(parts[i], out var parsedPart))
            {
                return null;
            }

            parts[i] = parsedPart.ToString();
        }

        return $"{parts[0]}.{parts[1]}.{parts[2]}.";
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
