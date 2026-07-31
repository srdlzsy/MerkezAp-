using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Authentication;

public sealed class JwtTokenFactoryTests
{
    private static readonly DateTime Now = new(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_DoesNotEmitPermissionClaimsForAdministrator()
    {
        var user = CreateUserWithRole(
            AuthorizationConstants.AdministratorRoleName,
            Enumerable.Range(1, 250).Select(index => $"module.menu-{index}.list"));

        var token = CreateFactory().Create(user).AccessToken;
        var permissionClaims = ReadPermissionClaims(token);

        Assert.Empty(permissionClaims);
        Assert.True(token.Length < 2000);
    }

    [Fact]
    public void Create_EmitsOnlyRuntimeScopePermissionClaimsForNonAdministrator()
    {
        var user = CreateUserWithRole(
            "PowerUser",
            [
                "stok-islemleri.zayiat-fisleri.list",
                "stok-islemleri.zayiat-fisleri.update",
                "stok-islemleri.zayiat-fisleri.all-warehouses",
                "ortak-islemler.sikayet-oneri.list-all"
            ]);

        var token = CreateFactory().Create(user).AccessToken;
        var permissionClaims = ReadPermissionClaims(token);

        Assert.Equal(
            [
                "stok-islemleri.zayiat-fisleri.all-warehouses",
                "ortak-islemler.sikayet-oneri.list-all"
            ],
            permissionClaims);
    }

    private static JwtTokenFactory CreateFactory() =>
        new(
            Options.Create(new JwtOptions
            {
                Issuer = "FurpaMerkezApi.Tests",
                Audience = "FurpaMerkezApi.Tests",
                SecretKey = "0123456789012345678901234567890123456789",
                ExpiryMinutes = 60
            }),
            new FixedClock(Now));

    private static string[] ReadPermissionClaims(string token) =>
        new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims
            .Where(claim => string.Equals(
                claim.Type,
                AuthorizationConstants.PermissionClaimType,
                StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();

    private static AppUser CreateUserWithRole(string roleName, IEnumerable<string> permissionCodes)
    {
        var user = new AppUser(
            Guid.NewGuid(),
            "test.user",
            "test.user@example.local",
            "Test",
            "User",
            "101",
            "TEST BRANCH",
            "hash",
            true,
            Now);

        var role = new AppRole(Guid.NewGuid(), roleName, null, true, Now);

        foreach (var permissionCode in permissionCodes)
        {
            var permission = new AppPermission(Guid.NewGuid(), permissionCode, permissionCode, null, Now);
            var rolePermission = new AppRolePermission(role.Id, permission.Id, Now);

            SetNavigation(rolePermission, nameof(AppRolePermission.Role), role);
            SetNavigation(rolePermission, nameof(AppRolePermission.Permission), permission);

            role.RolePermissions.Add(rolePermission);
        }

        var userRole = new AppUserRole(user.Id, role.Id, Now);
        SetNavigation(userRole, nameof(AppUserRole.User), user);
        SetNavigation(userRole, nameof(AppUserRole.Role), role);

        user.UserRoles.Add(userRole);

        return user;
    }

    private static void SetNavigation<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);
        property.SetValue(target, value);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
