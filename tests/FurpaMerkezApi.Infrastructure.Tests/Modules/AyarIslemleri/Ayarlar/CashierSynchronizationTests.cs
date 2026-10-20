using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.Ayarlar;

public sealed class CashierSynchronizationTests
{
    [Fact]
    public async Task CreateCashierAsync_CreatesMatchingCashierDefinition()
    {
        await using var contexts = CreateContexts();
        var service = CreateService(contexts);

        var result = await service.CreateCashierAsync(
            new CreateCashierRequest("Ali Veli", "R"),
            1,
            CancellationToken.None);

        var cashier = await contexts.Furpa.Cashiers.SingleAsync();
        var definition = await contexts.Furpa.CashierDefinitions.SingleAsync();

        Assert.Equal(cashier.CashierId, definition.CashierId);
        Assert.Equal(result.GeneratedPassword, cashier.CashierPassword);
        Assert.Equal(result.GeneratedPassword, definition.Password);
        Assert.Equal("ALİ", definition.FirstName);
        Assert.Equal("VELİ", definition.LastName);
        Assert.Equal("R", definition.Authorization);
        Assert.True(definition.IsActive);
        Assert.Matches("^[0-9]{6}$", result.GeneratedPassword);
    }

    [Fact]
    public async Task UpdateCashierAsync_SynchronizesFieldsAndPreservesDefinitionOnlyValues()
    {
        await using var contexts = CreateContexts();
        await SeedCashierAsync(contexts.Furpa);
        var service = CreateService(contexts);

        await service.UpdateCashierAsync(
            4855,
            new UpdateCashierRequest("Seray Nur Ayhan", "R", false),
            1,
            CancellationToken.None);

        var definition = await contexts.Furpa.CashierDefinitions.SingleAsync();

        Assert.Equal("SERAY NUR", definition.FirstName);
        Assert.Equal("AYHAN", definition.LastName);
        Assert.Equal("R", definition.Authorization);
        Assert.False(definition.IsActive);
        Assert.Equal("Korunan adres", definition.Address);
        Assert.Equal("555", definition.Phone);
        Assert.Equal("korunan-anahtar", definition.PasswordKey);
    }

    [Fact]
    public async Task ResetCashierPasswordAsync_StoresSamePasswordInBothCashierTables()
    {
        await using var contexts = CreateContexts();
        await SeedCashierAsync(contexts.Furpa);
        var service = CreateService(contexts);

        var result = await service.ResetCashierPasswordAsync(4855, 1, CancellationToken.None);

        var cashier = await contexts.Furpa.Cashiers.SingleAsync();
        var definition = await contexts.Furpa.CashierDefinitions.SingleAsync();

        Assert.Equal(result.GeneratedPassword, cashier.CashierPassword);
        Assert.Equal(result.GeneratedPassword, definition.Password);
        Assert.Matches("^[0-9]{6}$", result.GeneratedPassword);
    }

    private static AyarlarService CreateService(TestContexts contexts) =>
        new(contexts.Furpa, contexts.FurpaB2B, contexts.MikroWrite);

    private static TestContexts CreateContexts()
    {
        var databaseSuffix = Guid.NewGuid().ToString("N");
        var furpaOptions = new DbContextOptionsBuilder<FurpaDbContext>()
            .UseInMemoryDatabase($"cashier-furpa-{databaseSuffix}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var furpaB2BOptions = new DbContextOptionsBuilder<FurpaB2BDbContext>()
            .UseInMemoryDatabase($"cashier-b2b-{databaseSuffix}")
            .Options;
        var mikroWriteOptions = new DbContextOptionsBuilder<MikroWriteDbContext>()
            .UseInMemoryDatabase($"cashier-mikro-{databaseSuffix}")
            .Options;

        return new TestContexts(
            new FurpaDbContext(furpaOptions),
            new FurpaB2BDbContext(furpaB2BOptions),
            new MikroWriteDbContext(mikroWriteOptions));
    }

    private static async Task SeedCashierAsync(FurpaDbContext dbContext)
    {
        var createdAt = new DateTime(2025, 9, 16, 12, 37, 22);
        var updatedAt = new DateTime(2026, 10, 16, 17, 41, 45);
        dbContext.Cashiers.Add(new CashierEntity
        {
            CashierId = 11486,
            CashierCode = 4855,
            CashierName = "SERAY AYHAN",
            CashierPassword = "654321",
            CashierAuthorization = "Z",
            CashierState = true,
            CreateUser = 1,
            CreateDate = createdAt,
            UpdateUser = 1,
            UpdateDate = updatedAt
        });
        dbContext.CashierDefinitions.Add(new CashierDefinitionEntity
        {
            CashierId = 11486,
            CashierCode = "4855",
            FirstName = "SERAY",
            LastName = "AYHAN",
            Password = "123456",
            Authorization = "Z",
            IsActive = true,
            CreatedByUser = "1",
            CreatedAt = createdAt,
            UpdatedByUser = "1",
            UpdatedAt = createdAt,
            Address = "Korunan adres",
            Phone = "555",
            PasswordKey = "korunan-anahtar"
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private sealed class TestContexts(
        FurpaDbContext furpa,
        FurpaB2BDbContext furpaB2B,
        MikroWriteDbContext mikroWrite) : IAsyncDisposable
    {
        public FurpaDbContext Furpa { get; } = furpa;

        public FurpaB2BDbContext FurpaB2B { get; } = furpaB2B;

        public MikroWriteDbContext MikroWrite { get; } = mikroWrite;

        public async ValueTask DisposeAsync()
        {
            await Furpa.DisposeAsync();
            await FurpaB2B.DisposeAsync();
            await MikroWrite.DisposeAsync();
        }
    }
}
