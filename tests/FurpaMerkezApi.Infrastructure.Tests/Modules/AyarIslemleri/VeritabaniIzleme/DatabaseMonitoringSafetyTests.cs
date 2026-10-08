using FurpaMerkezApi.Application.Modules.AyarIslemleri.VeritabaniIzleme;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.VeritabaniIzleme;
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
}
