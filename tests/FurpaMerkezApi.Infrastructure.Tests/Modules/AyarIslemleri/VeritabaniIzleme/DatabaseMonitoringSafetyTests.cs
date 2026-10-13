using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.VeritabaniIzleme;

public sealed class DatabaseMonitoringSafetyTests
{
    [Fact]
    public void MatchesSessionIdentity_RejectsReusedSessionId()
    {
        var expectedLogin = new DateTime(2026, 10, 7, 11, 30, 54, DateTimeKind.Unspecified);
        var request = new TerminateDatabaseSessionRequest(expectedLogin, 9572, "Mikro API", "Root blocker checked.");

        Assert.False(DatabaseMonitoringService.MatchesSessionIdentity(
            expectedLogin.AddMinutes(1),
            9572,
            "Mikro API",
            request));
        Assert.False(DatabaseMonitoringService.MatchesSessionIdentity(
            expectedLogin,
            10001,
            "Mikro API",
            request));
        Assert.False(DatabaseMonitoringService.MatchesSessionIdentity(
            expectedLogin,
            9572,
            "Another program",
            request));
    }

    [Fact]
    public void SanitizeSql_MasksStringLiteralsAndLimitsLength()
    {
        const string sql = "SELECT * FROM users WHERE password = N'secret-value' AND code = 'F56'";

        var sanitized = DatabaseMonitoringService.SanitizeSql(sql, 48);

        Assert.DoesNotContain("secret-value", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("F56", sanitized, StringComparison.Ordinal);
        Assert.True(sanitized.Length <= 48);
    }

    [Fact]
    public void DeduplicateIncidentCandidates_KeepsOneCandidatePerFingerprint()
    {
        var candidates = new[]
        {
            Candidate("same-fingerprint", "warning", 30_000),
            Candidate("same-fingerprint", "critical", 20_000),
            Candidate("other-fingerprint", "warning", 40_000)
        };

        var result = DatabaseMonitoringService.DeduplicateIncidentCandidates(candidates);

        Assert.Equal(2, result.Count);
        Assert.Equal(
            "critical",
            Assert.Single(result, item => item.Fingerprint == "same-fingerprint").Severity);
    }

    [Fact]
    public async Task GetSafeWrites_DefaultsToUncompletedButExplicitCompletedStatusWins()
    {
        var now = new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc);
        await using var authDb = new AuthDbContext(
            new DbContextOptionsBuilder<AuthDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        await using var mikroDb = new MikroDbContext(
            new DbContextOptionsBuilder<MikroDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var processing = Request("shipment.create", now.AddMinutes(-5));
        var failed = Request("return.create", now.AddMinutes(-4));
        failed.MarkFailed(
            "Manual review required.",
            now.AddMinutes(-2),
            "MIKRO_DOCUMENT_CONTENT_MISMATCH",
            retryable: false);
        var completed = Request("count.create", now.AddMinutes(-3));
        completed.MarkCompleted("{}", now.AddMinutes(-1));
        authDb.MobileOfflineSyncRequests.AddRange(processing, failed, completed);
        await authDb.SaveChangesAsync();
        var service = new DatabaseMonitoringService(
            mikroDb,
            authDb,
            new FixedClock(now),
            new StaticOptionsMonitor<DatabaseMonitoringOptions>(new DatabaseMonitoringOptions()));

        var active = await service.GetSafeWritesAsync(
            new SafeWriteMonitoringRequest(null, null, null, null, IncludeCompleted: false, Take: 100),
            CancellationToken.None);
        var completedOnly = await service.GetSafeWritesAsync(
            new SafeWriteMonitoringRequest("Completed", null, null, null, IncludeCompleted: false, Take: 100),
            CancellationToken.None);

        Assert.Equal(2, active.Count);
        Assert.Equal("Processing", active.First().Status);
        Assert.DoesNotContain(active, item => item.Status == "Completed");
        Assert.False(Assert.Single(active, item => item.Status == "Failed").Retryable);
        Assert.Equal("Completed", Assert.Single(completedOnly).Status);
    }

    private static DatabaseMonitoringService.IncidentCandidate Candidate(
        string fingerprint,
        string severity,
        long elapsedMilliseconds) =>
        new(
            fingerprint,
            "Blocking",
            severity,
            42,
            41,
            "Mikro",
            "login",
            "host",
            "program",
            "LCK_M_X",
            elapsedMilliseconds,
            "SELECT 1",
            "Review blocking session.");

    private static MobileOfflineSyncRequest Request(string operationCode, DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            operationCode,
            Guid.NewGuid(),
            56,
            Guid.NewGuid().ToString("D"),
            new string('a', 64),
            "{}",
            createdAtUtc);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
