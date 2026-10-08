using FurpaMerkezApi.Application.Modules.AyarIslemleri.TerminalCihazlari;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.TerminalCihazlari;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.TerminalCihazlari;

public sealed class TerminalInstallationTests
{
    [Fact]
    public void RecordHeartbeat_TracksVersionAndWarehouseChanges()
    {
        var userId = Guid.NewGuid();
        var firstSeen = new DateTime(2026, 10, 8, 5, 0, 0, DateTimeKind.Utc);
        var installation = new TerminalInstallation(
            Guid.NewGuid(),
            "terminal-test",
            "1.1.90",
            91,
            110,
            userId,
            "Zebra",
            "TC21",
            "13",
            33,
            "arm64-v8a",
            "10.0.0.10",
            firstSeen);

        var changedAt = firstSeen.AddHours(1);
        installation.RecordHeartbeat(
            "1.1.91",
            92,
            120,
            userId,
            "Zebra",
            "TC21",
            "13",
            33,
            "arm64-v8a",
            "10.0.0.11",
            changedAt);

        Assert.Equal(120, installation.WarehouseNo);
        Assert.Equal(110, installation.PreviousWarehouseNo);
        Assert.Equal(1, installation.WarehouseChangeCount);
        Assert.Equal(changedAt, installation.WarehouseChangedAtUtc);
        Assert.Equal(changedAt, installation.VersionChangedAtUtc);
        Assert.Equal(92, installation.BuildNumber);
        Assert.Equal("10.0.0.11", installation.LastIpAddress);
    }

    [Fact]
    public void RecordHeartbeat_DoesNotMoveLastSeenBackwards()
    {
        var now = DateTime.UtcNow;
        var installation = new TerminalInstallation(
            Guid.NewGuid(), "terminal-test", "1.1.90", 91, 110, Guid.NewGuid(),
            null, null, null, null, string.Empty, null, now);

        installation.RecordHeartbeat(
            "1.1.90", 91, 110, installation.UserId,
            null, null, null, null, string.Empty, null, now.AddMinutes(-5));

        Assert.Equal(now, installation.LastSeenAtUtc);
    }

    [Fact]
    public void BuildListQuery_TranslatesOrderingAndUserSearchToSql()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer("Server=(local);Database=TerminalInstallationQueryTest;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        using var dbContext = new AuthDbContext(options);
        var request = new TerminalInstallationListRequest(110, "terminal", null, false, 7, 25);

        var sql = TerminalInstallationService.BuildListQuery(
                dbContext,
                request,
                new DateTime(2026, 10, 8, 6, 0, 0, DateTimeKind.Utc),
                92)
            .ToQueryString();

        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("last_seen_at_utc", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }
}
