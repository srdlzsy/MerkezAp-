using FurpaMerkezApi.Domain.Entities;
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
}
