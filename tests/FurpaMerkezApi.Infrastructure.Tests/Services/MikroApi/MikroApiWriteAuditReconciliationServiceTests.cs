using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Services.MikroApi;

public sealed class MikroApiWriteAuditReconciliationServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReconcileAsync_ReclassifiesOnlyStalePendingAndHistoricalTimeoutSuccess()
    {
        await using var dbContext = CreateAuthDbContext();
        var stalePending = CreateAudit(Now.AddMinutes(-20));
        var recentPending = CreateAudit(Now.AddMinutes(-5));
        var historicalTimeoutSuccess = CreateAudit(Now.AddDays(-2));
        historicalTimeoutSuccess.Complete(
            isError: false,
            isUnknown: false,
            httpStatusCode: 200,
            mikroStatusCode: 200,
            response: "{\"result\":[{\"success\":false,\"errorText\":\"MikroAPI - TimeOut\"}]}",
            error: null,
            attemptCount: 1,
            elapsedMilliseconds: 130_000,
            completedAtUtc: Now.AddDays(-2).AddSeconds(130));
        var recoveredTimeout = CreateAudit(Now.AddDays(-2));
        recoveredTimeout.Complete(
            isError: true,
            isUnknown: true,
            httpStatusCode: 200,
            mikroStatusCode: 200,
            response: "{\"result\":[{\"success\":false,\"errorText\":\"MikroAPI - TimeOut\"}]}",
            error: "MikroAPI - TimeOut",
            attemptCount: 1,
            elapsedMilliseconds: 130_000,
            completedAtUtc: Now.AddDays(-2).AddSeconds(130));
        recoveredTimeout.MarkRecovered("F120/5502", Guid.NewGuid(), null, Now.AddDays(-2).AddMinutes(3));

        dbContext.MikroApiWriteAudits.AddRange(
            stalePending,
            recentPending,
            historicalTimeoutSuccess,
            recoveredTimeout);
        await dbContext.SaveChangesAsync();

        var service = new MikroApiWriteAuditReconciliationService(
            dbContext,
            new FixedClock(Now),
            new StaticOptionsMonitor<MikroApiWriteAuditOptions>(new MikroApiWriteAuditOptions()),
            NullLogger<MikroApiWriteAuditReconciliationService>.Instance);

        var reconciledCount = await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal(2, reconciledCount);
        Assert.Equal(MikroApiWriteAuditStatus.Unknown, stalePending.Status);
        Assert.NotNull(stalePending.CompletedAtUtc);
        Assert.Equal(MikroApiWriteAuditStatus.Pending, recentPending.Status);
        Assert.Equal(MikroApiWriteAuditStatus.Unknown, historicalTimeoutSuccess.Status);
        Assert.Equal(MikroApiWriteAuditStatus.Recovered, recoveredTimeout.Status);
    }

    [Fact]
    public async Task ReconcileAsync_DoesNothingWhenReconciliationIsDisabled()
    {
        await using var dbContext = CreateAuthDbContext();
        var stalePending = CreateAudit(Now.AddHours(-1));
        dbContext.MikroApiWriteAudits.Add(stalePending);
        await dbContext.SaveChangesAsync();

        var service = new MikroApiWriteAuditReconciliationService(
            dbContext,
            new FixedClock(Now),
            new StaticOptionsMonitor<MikroApiWriteAuditOptions>(new MikroApiWriteAuditOptions
            {
                ReconciliationEnabled = false
            }),
            NullLogger<MikroApiWriteAuditReconciliationService>.Instance);

        var reconciledCount = await service.ReconcileAsync(CancellationToken.None);

        Assert.Equal(0, reconciledCount);
        Assert.Equal(MikroApiWriteAuditStatus.Pending, stalePending.Status);
    }

    private static MikroApiWriteAudit CreateAudit(DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "/Api/apiMethods/IrsaliyeKaydetV2",
            new string('A', 64),
            createdAtUtc);

    private static AuthDbContext CreateAuthDbContext()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"mikro-api-audit-reconciliation-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options);
    }

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
