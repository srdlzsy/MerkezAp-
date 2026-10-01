using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FurpaMerkezApi.Infrastructure.Authentication;

public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

public interface IJwtTokenFactory
{
    TokenResult Create(
        AppUser user,
        SessionAccessProfile accessProfile,
        string clientType,
        string? deviceId,
        Guid sessionId);
}

public sealed class JwtTokenFactory(IOptions<JwtOptions> options, IClock clock) : IJwtTokenFactory
{
    private readonly JwtOptions _options = options.Value;

    public TokenResult Create(
        AppUser user,
        SessionAccessProfile accessProfile,
        string clientType,
        string? deviceId,
        Guid sessionId)
    {
        ValidateOptions();

        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.ExpiryMinutes);

        var roles = accessProfile.RoleNames;
        var permissions = accessProfile.Permissions
            .Select(permission => permission.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new("first_name", user.FirstName),
            new("last_name", user.LastName),
            new("warehouse_no", user.WarehouseNo),
            new("warehouse_name", user.WarehouseName),
            new("client_type", AuthenticationClientTypes.Normalize(clientType)),
            new("session_id", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            claims.Add(new Claim("device_id", deviceId.Trim()));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (!roles.Any(AuthorizationConstants.IsAdministratorRole))
        {
            claims.AddRange(permissions
                .Where(AuthorizationConstants.ShouldEmitPermissionClaim)
                .Select(permission => new Claim(AuthorizationConstants.PermissionClaimType, permission)));
        }

        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: signingCredentials);

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.Issuer) ||
            string.IsNullOrWhiteSpace(_options.Audience) ||
            string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("JWT configuration is missing required values.");
        }

        if (_options.SecretKey.Length < 32)
        {
            throw new InvalidOperationException("JWT secret key must be at least 32 characters.");
        }
    }
}
