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
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class AuthService(
    AuthDbContext dbContext,
    FurpaDbContext furpaDbContext,
    IPasswordHasher passwordHasher,
    IJwtTokenFactory jwtTokenFactory,
    IClock clock,
    IOptions<JwtOptions> jwtOptions,
    IConfiguration configuration,
    ILogger<AuthService> logger) : IAuthService
{
    private static readonly Guid TerminalRoleId = Guid.Parse("3c1daafe-5922-466e-9f79-6d2ca34ce84d");
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

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
        return await CreateAuthResponseAsync(createdUser, cancellationToken);
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

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is invalid.");
        }

        var now = clock.UtcNow;
        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
                .ThenInclude(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
                        .ThenInclude(role => role.RolePermissions)
                            .ThenInclude(rolePermission => rolePermission.Permission)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || !storedToken.IsActive(now) || !storedToken.User.IsActive)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        var refreshToken = CreateRefreshToken(storedToken.UserId, now);
        storedToken.Revoke(now, refreshToken.Entity.TokenHash);
        dbContext.RefreshTokens.Add(refreshToken.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(storedToken.User, refreshToken.Token, refreshToken.Entity.ExpiresAtUtc);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = HashRefreshToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return;
        }

        storedToken.Revoke(clock.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(userId, cancellationToken);
        return user.ToDto();
    }

    public async Task<WarehouseContextResponse> GetWarehouseContextAsync(
        Guid userId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Select(currentUser => new
            {
                currentUser.Id,
                currentUser.Username,
                currentUser.WarehouseNo,
                currentUser.WarehouseName,
                currentUser.IsActive,
                IsTerminalUser = currentUser.UserRoles.Any(userRole => userRole.RoleId == TerminalRoleId)
            })
            .FirstOrDefaultAsync(currentUser => currentUser.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException("User was not found.");
        }

        if (!user.IsActive)
        {
            return new WarehouseContextResponse(
                user.Id,
                user.Username,
                user.WarehouseNo,
                user.WarehouseName,
                null,
                null,
                user.IsTerminalUser,
                true,
                "UserInactive",
                clock.UtcNow);
        }

        if (!user.IsTerminalUser)
        {
            return new WarehouseContextResponse(
                user.Id,
                user.Username,
                user.WarehouseNo,
                user.WarehouseName,
                null,
                null,
                false,
                false,
                "NotTerminalUser",
                clock.UtcNow);
        }

        if (!int.TryParse(user.WarehouseNo, out var userWarehouseNo))
        {
            return new WarehouseContextResponse(
                user.Id,
                user.Username,
                user.WarehouseNo,
                user.WarehouseName,
                null,
                null,
                true,
                true,
                "InvalidTokenWarehouse",
                clock.UtcNow);
        }

        var currentWarehouse = await ResolveWarehouseFromIpAsync(ipAddress, cancellationToken);
        if (currentWarehouse.Status != "Resolved")
        {
            return new WarehouseContextResponse(
                user.Id,
                user.Username,
                user.WarehouseNo,
                user.WarehouseName,
                currentWarehouse.WarehouseNo?.ToString(),
                currentWarehouse.WarehouseName,
                true,
                false,
                currentWarehouse.Status,
                clock.UtcNow);
        }

        var currentWarehouseNo = currentWarehouse.WarehouseNo.GetValueOrDefault();
        var isSameWarehouse = currentWarehouseNo == userWarehouseNo;
        var isAllowedSharedNetworkWarehouse = !isSameWarehouse &&
            GetAllowedNetworkBranchNos(userWarehouseNo).Contains(currentWarehouseNo);
        var requiresRelogin = !isSameWarehouse && !isAllowedSharedNetworkWarehouse;

        return new WarehouseContextResponse(
            user.Id,
            user.Username,
            user.WarehouseNo,
            user.WarehouseName,
            currentWarehouse.WarehouseNo?.ToString(),
            currentWarehouse.WarehouseName,
            true,
            requiresRelogin,
            requiresRelogin
                ? "WarehouseChanged"
                : isAllowedSharedNetworkWarehouse
                    ? "SharedNetwork"
                    : "Ok",
            clock.UtcNow);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(AppUser user, CancellationToken cancellationToken)
    {
        var refreshToken = CreateRefreshToken(user.Id, clock.UtcNow);
        dbContext.RefreshTokens.Add(refreshToken.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(user, refreshToken.Token, refreshToken.Entity.ExpiresAtUtc);
    }

    private AuthResponse CreateAuthResponse(AppUser user, string refreshToken, DateTime refreshTokenExpiresAtUtc)
    {
        var token = jwtTokenFactory.Create(user);
        return new AuthResponse(
            token.AccessToken,
            token.ExpiresAtUtc,
            user.ToDto(),
            refreshToken,
            refreshTokenExpiresAtUtc);
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

    private async Task<ResolvedWarehouseContext> ResolveWarehouseFromIpAsync(
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return new ResolvedWarehouseContext(null, null, "NetworkUnknown");
        }

        var inboundNetworkPrefix = GetIpv4NetworkPrefix(ipAddress);
        if (inboundNetworkPrefix is null)
        {
            return new ResolvedWarehouseContext(null, null, "NetworkUnknown");
        }

        var matches = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .Where(item => item.BranchIpAddress != "")
            .Select(item => new
            {
                item.BranchNo,
                item.BranchIpAddress
            })
            .ToArrayAsync(cancellationToken);

        var resolvedBranches = matches
            .Where(item => string.Equals(
                GetIpv4NetworkPrefix(item.BranchIpAddress),
                inboundNetworkPrefix,
                StringComparison.Ordinal))
            .Select(item => item.BranchNo)
            .Distinct()
            .Order()
            .ToArray();

        return resolvedBranches.Length switch
        {
            0 => new ResolvedWarehouseContext(null, null, "NetworkUnknown"),
            1 => new ResolvedWarehouseContext(
                resolvedBranches[0],
                $"Depo {resolvedBranches[0]}",
                "Resolved"),
            _ => new ResolvedWarehouseContext(null, null, "NetworkAmbiguous")
        };
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

    private (string Token, AppRefreshToken Entity) CreateRefreshToken(Guid userId, DateTime nowUtc)
    {
        var expiryDays = _jwtOptions.RefreshTokenExpiryDays <= 0
            ? 14
            : _jwtOptions.RefreshTokenExpiryDays;
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new AppRefreshToken(
            Guid.NewGuid(),
            userId,
            HashRefreshToken(token),
            nowUtc,
            nowUtc.AddDays(expiryDays));

        return (token, entity);
    }

    private static string HashRefreshToken(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token is required.", nameof(refreshToken));
        }

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken.Trim())));
    }

    private sealed record ResolvedWarehouseContext(
        int? WarehouseNo,
        string? WarehouseName,
        string Status);
}
