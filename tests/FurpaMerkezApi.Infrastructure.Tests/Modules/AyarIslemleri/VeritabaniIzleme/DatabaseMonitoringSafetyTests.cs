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
}
