using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Identity.Contracts;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services;

public sealed class UserManagementServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 6, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpdateAsync_ChangesPasswordAndRevokesActiveRefreshTokens()
    {
        await using var dbContext = CreateAuthDbContext();
        var userId = Guid.NewGuid();
        dbContext.Users.Add(new AppUser(
            userId,
            "jdoe",
            "jdoe@furpa.local",
            "John",
            "Doe",
            "110",
            "KESTEL",
            TestPasswordHasher.HashValue("old-secret"),
            true,
            Now.AddDays(-1)));
        dbContext.RefreshTokens.Add(new AppRefreshToken(
            Guid.NewGuid(),
            userId,
            "old-refresh-token-hash",
            Now.AddHours(-1),
            Now.AddDays(1)));
        await dbContext.SaveChangesAsync();

        var service = new UserManagementService(dbContext, new FixedClock(Now), new TestPasswordHasher());

        await service.UpdateAsync(
            userId,
            new UpdateUserRequest(
                "jdoe",
                "jdoe@furpa.local",
                "John",
                "Doe",
                "110",
                "KESTEL",
                true,
                "new-secret"),
            CancellationToken.None);

        var updatedUser = await dbContext.Users.SingleAsync(user => user.Id == userId);
        var refreshToken = await dbContext.RefreshTokens.SingleAsync(token => token.UserId == userId);

        Assert.Equal(TestPasswordHasher.HashValue("new-secret"), updatedUser.PasswordHash);
        Assert.Equal(Now, refreshToken.RevokedAtUtc);
    }

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"user-management-service-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => HashValue(password);

        public bool Verify(string password, string passwordHash) =>
            string.Equals(HashValue(password), passwordHash, StringComparison.Ordinal);

        public static string HashValue(string password) => $"hash:{password}";
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
