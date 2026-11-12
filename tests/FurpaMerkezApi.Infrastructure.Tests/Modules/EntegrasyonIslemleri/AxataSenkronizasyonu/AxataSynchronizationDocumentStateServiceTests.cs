using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationDocumentStateServiceTests
{
    [Fact]
    public async Task RecordOutcomeAsync_DefersRepeatedBusinessFailureWithBackoff()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc));
        var service = CreateService(dbContext, clock);

        await service.RecordOutcomeAsync(
            "c01-outbound-delivery-import",
            [],
            [new AxataSynchronizationDocumentFailure("123", "Miktar sifir.")],
            CancellationToken.None);

        Assert.Contains(
            "123",
            await service.GetDeferredDocumentKeysAsync(
                "c01-outbound-delivery-import",
                CancellationToken.None));

        clock.UtcNow = clock.UtcNow.AddMinutes(16);
        Assert.Empty(await service.GetDeferredDocumentKeysAsync(
            "c01-outbound-delivery-import",
            CancellationToken.None));

        await service.RecordOutcomeAsync(
            "c01-outbound-delivery-import",
            [],
            [new AxataSynchronizationDocumentFailure("123", "Miktar halen sifir.")],
            CancellationToken.None);

        var state = await dbContext.AxataSynchronizationDocumentStates.SingleAsync();
        Assert.Equal(2, state.FailureCount);
        Assert.Equal(clock.UtcNow.AddHours(1), state.NextAttemptAtUtc);
    }

    [Fact]
    public async Task RecordOutcomeAsync_RemovesStateWhenDocumentSucceeds()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc));
        var service = CreateService(dbContext, clock);

        await service.RecordOutcomeAsync(
            "g01-inbound-atf-import",
            [],
            [new AxataSynchronizationDocumentFailure("F50.22989", "Belge dogrulanamadi.")],
            CancellationToken.None);
        await service.RecordOutcomeAsync(
            "g01-inbound-atf-import",
            ["f50.22989"],
            [],
            CancellationToken.None);

        Assert.Empty(await dbContext.AxataSynchronizationDocumentStates.ToArrayAsync());
    }

    [Fact]
    public async Task Disabled_DoesNotReadOrWriteDocumentState()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc));
        var service = CreateService(dbContext, clock, enabled: false);

        await service.RecordOutcomeAsync(
            "g02-inbound-delivery-import",
            [],
            [new AxataSynchronizationDocumentFailure("987", "Miktar sifir.")],
            CancellationToken.None);

        Assert.Empty(await dbContext.AxataSynchronizationDocumentStates.ToArrayAsync());
        Assert.Empty(await service.GetDeferredDocumentKeysAsync(
            "g02-inbound-delivery-import",
            CancellationToken.None));
    }

    [Fact]
    public async Task RepeatedBusinessFailure_StopsAutomaticRetryAtManualReviewThreshold()
    {
        await using var dbContext = CreateDbContext();
        var clock = new MutableClock(new DateTime(2026, 11, 12, 6, 0, 0, DateTimeKind.Utc));
        var service = CreateService(dbContext, clock, manualReviewAfterFailureCount: 3);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await service.RecordOutcomeAsync(
                "c01-outbound-delivery-import",
                [],
                [new AxataSynchronizationDocumentFailure("263207", "Teslim miktari kalan miktardan buyuk.")],
                CancellationToken.None);
            clock.UtcNow = clock.UtcNow.AddHours(7);
        }

        var state = await dbContext.AxataSynchronizationDocumentStates.SingleAsync();
        Assert.Equal(3, state.FailureCount);
        Assert.Contains("263207", await service.GetDeferredDocumentKeysAsync(
            "c01-outbound-delivery-import",
            CancellationToken.None));

        await service.RecordOutcomeAsync(
            "c01-outbound-delivery-import",
            [],
            [new AxataSynchronizationDocumentFailure("263207", "Ayni veri sorunu devam ediyor.")],
            CancellationToken.None);

        Assert.Equal(3, state.FailureCount);

        clock.UtcNow = clock.UtcNow.AddDays(31);
        await service.RecordOutcomeAsync(
            "c01-outbound-delivery-import",
            [],
            [],
            CancellationToken.None);

        Assert.Single(await dbContext.AxataSynchronizationDocumentStates.ToArrayAsync());
        Assert.Contains("263207", await service.GetDeferredDocumentKeysAsync(
            "c01-outbound-delivery-import",
            CancellationToken.None));
    }

    private static AuthDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AxataSynchronizationDocumentStateService CreateService(
        AuthDbContext dbContext,
        IClock clock,
        bool enabled = true,
        int manualReviewAfterFailureCount = 12) =>
        new(
            dbContext,
            clock,
            new StaticOptionsMonitor<AxataSynchronizationOptions>(new AxataSynchronizationOptions
            {
                DocumentRetryState = new AxataDocumentRetryStateOptions
                {
                    Enabled = enabled,
                    ManualReviewAfterFailureCount = manualReviewAfterFailureCount
                }
            }),
            NullLogger<AxataSynchronizationDocumentStateService>.Instance);

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
