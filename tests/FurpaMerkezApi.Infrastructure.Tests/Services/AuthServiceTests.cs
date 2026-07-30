using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Authentication.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using FurpaMerkezApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services;

public sealed class AuthServiceTests
{
    private static readonly Guid TerminalRoleId = Guid.Parse("3c1daafe-5922-466e-9f79-6d2ca34ce84d");
    private static readonly DateTime Now = new(2026, 7, 30, 8, 0, 0, DateTimeKind.Utc);
    private const string Password = "secret";

    [Fact]
    public async Task LoginAsync_AllowsTerminalWarehouseFromSharedNetworkGroupWithoutOwnBranchIpSettings()
    {
        await using var authDbContext = CreateAuthDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();
        await AddTerminalUserAsync(authDbContext, "terminal56", "56");
        await AddBranchAsync(furpaDbContext, 50, "192.168.254.12");

        var service = CreateService(
            authDbContext,
            furpaDbContext,
            [50, 56]);

        var response = await service.LoginAsync(
            new LoginRequest("terminal56", Password, "192.168.254.237"),
            CancellationToken.None);

        Assert.Equal("token-56", response.AccessToken);
        Assert.Equal("56", response.User.WarehouseNo);
    }

    [Fact]
    public async Task LoginAsync_AllowsTerminalWarehouse100OrGreaterFromOwnBranchNetwork()
    {
        await using var authDbContext = CreateAuthDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();
        await AddTerminalUserAsync(authDbContext, "terminal120", "120");
        await AddBranchAsync(furpaDbContext, 120, "10.0.120.15");

        var service = CreateService(authDbContext, furpaDbContext);

        var response = await service.LoginAsync(
            new LoginRequest("terminal120", Password, "10.0.120.88"),
            CancellationToken.None);

        Assert.Equal("token-120", response.AccessToken);
        Assert.Equal("120", response.User.WarehouseNo);
    }

    [Fact]
    public async Task LoginAsync_RejectsTerminalWarehouse100OrGreaterFromDifferentNetwork()
    {
        await using var authDbContext = CreateAuthDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();
        await AddTerminalUserAsync(authDbContext, "terminal120", "120");
        await AddBranchAsync(furpaDbContext, 120, "10.0.120.15");

        var service = CreateService(authDbContext, furpaDbContext);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(
                new LoginRequest("terminal120", Password, "10.0.121.88"),
                CancellationToken.None));
    }

    private static AuthService CreateService(
        AuthDbContext authDbContext,
        FurpaDbContext furpaDbContext,
        params int[][] sharedNetworkGroups) =>
        new(
            authDbContext,
            furpaDbContext,
            new TestPasswordHasher(),
            new TestJwtTokenFactory(),
            new FixedClock(Now),
            CreateConfiguration(sharedNetworkGroups),
            NullLogger<AuthService>.Instance);

    private static IConfiguration CreateConfiguration(int[][] sharedNetworkGroups)
    {
        var values = new Dictionary<string, string?>();

        for (var groupIndex = 0; groupIndex < sharedNetworkGroups.Length; groupIndex++)
        {
            var warehouseNos = sharedNetworkGroups[groupIndex];
            for (var warehouseIndex = 0; warehouseIndex < warehouseNos.Length; warehouseIndex++)
            {
                values[$"Auth:TerminalLogin:SharedNetworkWarehouseGroups:{groupIndex}:WarehouseNos:{warehouseIndex}"] =
                    warehouseNos[warehouseIndex].ToString();
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-service-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

    private static FurpaDbContext CreateFurpaDbContext()
    {
        var options = new DbContextOptionsBuilder<FurpaDbContext>()
            .UseInMemoryDatabase($"auth-service-furpa-{Guid.NewGuid():N}")
            .Options;

        return new FurpaDbContext(options);
    }

    private static async Task AddTerminalUserAsync(
        AuthDbContext dbContext,
        string username,
        string warehouseNo)
    {
        var user = new AppUser(
            Guid.NewGuid(),
            username,
            $"{username}@furpa.local",
            "Terminal",
            "User",
            warehouseNo,
            $"Depo {warehouseNo}",
            TestPasswordHasher.HashValue(Password),
            true,
            Now);

        dbContext.Roles.Add(new AppRole(TerminalRoleId, "Terminal", null, true, Now));
        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new AppUserRole(user.Id, TerminalRoleId, Now));

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static async Task AddBranchAsync(
        FurpaDbContext dbContext,
        int branchNo,
        string branchIpAddress)
    {
        dbContext.BranchDetails.Add(new BranchDetailEntity
        {
            BranchNo = branchNo,
            BranchIpAddress = branchIpAddress,
            PosGenelFolderPath = "KASA\\POSGENEL",
            PoskonFolderPath = "KASA\\POSKON",
            BranchScalesFolderPath = "TERAZI",
            ScalesType = 1
        });

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => HashValue(password);

        public bool Verify(string password, string passwordHash) =>
            string.Equals(HashValue(password), passwordHash, StringComparison.Ordinal);

        public static string HashValue(string password) => $"hash:{password}";
    }

    private sealed class TestJwtTokenFactory : IJwtTokenFactory
    {
        public TokenResult Create(AppUser user) =>
            new($"token-{user.WarehouseNo}", Now.AddHours(1));
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
