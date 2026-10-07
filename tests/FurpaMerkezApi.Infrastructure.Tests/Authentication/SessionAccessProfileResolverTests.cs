using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Authentication;

public sealed class SessionAccessProfileResolverTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ResolvePermissionCodesAsync_ReturnsEmptyWhenRequestedClientRoleIsInactive()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var generalRole = AddRoleWithPermission(dbContext, "General", "general.permission", isActive: true);
        var inactiveWebRole = AddRoleWithPermission(dbContext, "Web", "web.permission", isActive: false);
        dbContext.UserRoles.Add(new AppUserRole(user.Id, generalRole.Id, Now));
        dbContext.UserClientRoles.Add(new AppUserClientRole(user.Id, "web", inactiveWebRole.Id, Now));
        await dbContext.SaveChangesAsync();

        var permissions = await ResolveAsync(dbContext, user.Id, "web");

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task ResolvePermissionCodesAsync_ReturnsEmptyWhenAnotherClientProfileExists()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var generalRole = AddRoleWithPermission(dbContext, "General", "general.permission", isActive: true);
        var terminalRole = AddRoleWithPermission(dbContext, "Terminal", "terminal.permission", isActive: true);
        dbContext.UserRoles.Add(new AppUserRole(user.Id, generalRole.Id, Now));
        dbContext.UserClientRoles.Add(new AppUserClientRole(user.Id, "terminal", terminalRole.Id, Now));
        await dbContext.SaveChangesAsync();

        var permissions = await ResolveAsync(dbContext, user.Id, "web");

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task ResolvePermissionCodesAsync_DoesNotFallbackWhenUnifiedRoleIsInactive()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var generalRole = AddRoleWithPermission(dbContext, "General", "general.permission", isActive: true);
        var unifiedRole = AddRoleWithPermission(
            dbContext,
            SessionAccessProfileResolver.UnifiedWarehouseRoleName,
            "unified.permission",
            isActive: false);
        dbContext.UserRoles.AddRange(
            new AppUserRole(user.Id, generalRole.Id, Now),
            new AppUserRole(user.Id, unifiedRole.Id, Now));
        await dbContext.SaveChangesAsync();

        var permissions = await ResolveAsync(dbContext, user.Id, "web");

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task ResolvePermissionCodesAsync_UsesRequestedActiveClientRolesOnly()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var generalRole = AddRoleWithPermission(dbContext, "General", "general.permission", isActive: true);
        var webRole = AddRoleWithPermission(dbContext, "Web", "web.permission", isActive: true);
        dbContext.UserRoles.Add(new AppUserRole(user.Id, generalRole.Id, Now));
        dbContext.UserClientRoles.Add(new AppUserClientRole(user.Id, "web", webRole.Id, Now));
        await dbContext.SaveChangesAsync();

        var permissions = await ResolveAsync(dbContext, user.Id, "web");

        Assert.Equal(["web.permission"], permissions);
    }

    [Fact]
    public async Task ResolvePermissionCodesAsync_PreservesGeneralRolesForClassicUsers()
    {
        await using var dbContext = CreateDbContext();
        var user = AddUser(dbContext);
        var generalRole = AddRoleWithPermission(dbContext, "General", "general.permission", isActive: true);
        dbContext.UserRoles.Add(new AppUserRole(user.Id, generalRole.Id, Now));
        await dbContext.SaveChangesAsync();

        var permissions = await ResolveAsync(dbContext, user.Id, "web");

        Assert.Equal(["general.permission"], permissions);
    }

    private static async Task<IReadOnlyCollection<string>> ResolveAsync(
        AuthDbContext dbContext,
        Guid userId,
        string clientType)
    {
        dbContext.ChangeTracker.Clear();
        var resolver = new SessionAccessProfileResolver(dbContext);
        return await resolver.ResolvePermissionCodesAsync(userId, clientType, CancellationToken.None);
    }

    private static AppUser AddUser(AuthDbContext dbContext)
    {
        var user = new AppUser(
            Guid.NewGuid(),
            $"user-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@furpa.local",
            "Test",
            "User",
            "110",
            "Depo 110",
            "password-hash",
            true,
            Now);
        dbContext.Users.Add(user);
        return user;
    }

    private static AppRole AddRoleWithPermission(
        AuthDbContext dbContext,
        string roleName,
        string permissionCode,
        bool isActive)
    {
        var role = new AppRole(Guid.NewGuid(), roleName, null, isActive, Now);
        var permission = new AppPermission(
            Guid.NewGuid(),
            permissionCode,
            permissionCode,
            null,
            Now);
        dbContext.Roles.Add(role);
        dbContext.Permissions.Add(permission);
        dbContext.RolePermissions.Add(new AppRolePermission(role.Id, permission.Id, Now));
        return role;
    }

    private static AuthDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"session-access-profile-{Guid.NewGuid():N}")
            .Options;
        return new AuthDbContext(options);
    }
}
