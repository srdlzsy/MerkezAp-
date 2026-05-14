using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Persistence;

public static class WarehouseUserSynchronizationExtensions
{
    private const string MagazaciUsernameSuffix = "magazaci";
    private const string TerminalUsernameSuffix = "terminal";
    private const string LocalEmailDomain = "furpamerkez.local";
    private static readonly Guid MagazaciRoleId = Guid.Parse("2d5f7156-a332-497a-ba63-6194e56df746");
    private static readonly Guid TerminalRoleId = Guid.Parse("3c1daafe-5922-466e-9f79-6d2ca34ce84d");

    public static async Task SynchronizeWarehouseUsersAsync(
        this AuthDbContext authDbContext,
        MikroDbContext mikroDbContext,
        IPasswordHasher passwordHasher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authDbContext);
        ArgumentNullException.ThrowIfNull(mikroDbContext);
        ArgumentNullException.ThrowIfNull(passwordHasher);

        var warehouses = await LoadWarehousesAsync(mikroDbContext, cancellationToken);

        if (warehouses.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var roles = await authDbContext.Roles
            .ToListAsync(cancellationToken);
        var magazaciRole = ResolveRequiredRole(roles, MagazaciRoleId, "Magazaci");
        var terminalRole = ResolveRequiredRole(roles, TerminalRoleId, "Terminal");

        if (!magazaciRole.IsActive)
        {
            throw new InvalidOperationException($"Role '{magazaciRole.Name}' is inactive.");
        }

        if (!terminalRole.IsActive)
        {
            throw new InvalidOperationException($"Role '{terminalRole.Name}' is inactive.");
        }

        var expectedUsernames = warehouses
            .SelectMany(warehouse => new[]
            {
                BuildUsername(warehouse.WarehouseNo, MagazaciUsernameSuffix),
                BuildUsername(warehouse.WarehouseNo, TerminalUsernameSuffix)
            })
            .Select(NormalizeLookup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingUsers = await authDbContext.Users
            .Include(user => user.UserRoles)
            .Where(user => expectedUsernames.Contains(user.NormalizedUsername))
            .ToListAsync(cancellationToken);
        var userByNormalizedUsername = existingUsers.ToDictionary(user => user.NormalizedUsername, StringComparer.OrdinalIgnoreCase);

        foreach (var warehouse in warehouses)
        {
            EnsureWarehouseUser(
                authDbContext,
                userByNormalizedUsername,
                warehouse,
                MagazaciUsernameSuffix,
                magazaciRole,
                passwordHasher,
                now);
            EnsureWarehouseUser(
                authDbContext,
                userByNormalizedUsername,
                warehouse,
                TerminalUsernameSuffix,
                terminalRole,
                passwordHasher,
                now);
        }

        if (authDbContext.ChangeTracker.HasChanges())
        {
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<WarehouseSeedItem[]> LoadWarehousesAsync(
        MikroDbContext mikroDbContext,
        CancellationToken cancellationToken)
    {
        var warehouses = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_iptal != true && warehouse.dep_no.HasValue && warehouse.dep_no.Value > 0)
            .OrderBy(warehouse => warehouse.dep_no)
            .Select(warehouse => new
            {
                WarehouseNo = warehouse.dep_no!.Value,
                WarehouseName = warehouse.dep_adi
            })
            .ToArrayAsync(cancellationToken);

        return warehouses
            .Select(warehouse => new WarehouseSeedItem(
                warehouse.WarehouseNo,
                NormalizeWarehouseName(warehouse.WarehouseNo, warehouse.WarehouseName)))
            .ToArray();
    }

    private static void EnsureWarehouseUser(
        AuthDbContext authDbContext,
        IDictionary<string, AppUser> userByNormalizedUsername,
        WarehouseSeedItem warehouse,
        string usernameSuffix,
        AppRole role,
        IPasswordHasher passwordHasher,
        DateTime now)
    {
        var username = BuildUsername(warehouse.WarehouseNo, usernameSuffix);
        var normalizedUsername = NormalizeLookup(username);
        var warehouseNo = warehouse.WarehouseNo.ToString(CultureInfo.InvariantCulture);

        if (!userByNormalizedUsername.TryGetValue(normalizedUsername, out var user))
        {
            user = new AppUser(
                CreateDeterministicGuid($"user:{username}"),
                username,
                BuildEmail(username),
                BuildFirstName(warehouse),
                BuildLastName(usernameSuffix),
                warehouseNo,
                warehouse.WarehouseName,
                passwordHasher.Hash(username),
                true,
                now);
            user.UserRoles.Add(new AppUserRole(user.Id, role.Id, now));

            authDbContext.Users.Add(user);
            userByNormalizedUsername[normalizedUsername] = user;
            return;
        }

        if (!string.Equals(user.WarehouseNo, warehouseNo, StringComparison.Ordinal) ||
            !string.Equals(user.WarehouseName, warehouse.WarehouseName, StringComparison.Ordinal))
        {
            user.UpdateProfile(
                user.Email,
                user.FirstName,
                user.LastName,
                warehouseNo,
                warehouse.WarehouseName,
                user.IsActive,
                now);
        }

        if (!user.UserRoles.Any(userRole => userRole.RoleId == role.Id))
        {
            user.UserRoles.Add(new AppUserRole(user.Id, role.Id, now));
        }
    }

    private static AppRole ResolveRequiredRole(
        IReadOnlyCollection<AppRole> roles,
        Guid roleId,
        string roleName)
    {
        var role = roles.FirstOrDefault(currentRole => currentRole.Id == roleId);

        if (role is null)
        {
            throw new InvalidOperationException(
                $"Required role '{roleName}' with id '{roleId}' was not found in auth database.");
        }

        return role;
    }

    private static string BuildUsername(int warehouseNo, string usernameSuffix) =>
        $"{warehouseNo.ToString(CultureInfo.InvariantCulture)}.{usernameSuffix}";

    private static string BuildEmail(string username) => $"{username}@{LocalEmailDomain}";

    private static string BuildFirstName(WarehouseSeedItem warehouse) =>
        TrimToMaxLength(warehouse.WarehouseName, 100);

    private static string BuildLastName(string usernameSuffix) =>
        usernameSuffix.Equals(TerminalUsernameSuffix, StringComparison.OrdinalIgnoreCase)
            ? "Terminal"
            : "Magazaci";

    private static string NormalizeWarehouseName(int warehouseNo, string? warehouseName)
    {
        var normalized = string.IsNullOrWhiteSpace(warehouseName)
            ? $"Depo {warehouseNo.ToString(CultureInfo.InvariantCulture)}"
            : warehouseName.Trim();

        return TrimToMaxLength(normalized, 150);
    }

    private static string TrimToMaxLength(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string NormalizeLookup(string value) => value.Trim().ToUpperInvariant();

    private static Guid CreateDeterministicGuid(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash);
    }

    private sealed record WarehouseSeedItem(int WarehouseNo, string WarehouseName);
}
