using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using FurpaMerkezApi.Infrastructure.Persistence.FurpaB2B;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using MikroCashRegisterDetailEntity = FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models.CashRegisterDetailEntity;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.Ayarlar;

public sealed class CashRegisterTerminalManagementTests
{
    [Fact]
    public async Task CreateTerminal_AllowsAnotherBankRowForSameTerminalNumber()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        var service = CreateService(contexts);

        var result = await service.CreateCashRegisterTerminalAsync(
            160,
            160,
            new CreateCashRegisterTerminalRequest("UB0016027768", "TEB", "PS927768", "495396"),
            CancellationToken.None);

        Assert.Equal("UB0016027768", result.TerminalNo);
        Assert.Equal(2, await contexts.MikroWrite.CashRegisterDetails.CountAsync());
        Assert.Single(await contexts.MikroWrite.CashRegisterBranches.ToArrayAsync());
    }

    [Fact]
    public async Task DeleteTerminal_KeepsGroupMappingUntilLastBankRowIsDeleted()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        contexts.MikroWrite.CashRegisterDetails.Add(new MikroCashRegisterDetailEntity
        {
            Id = 2,
            CashRegisterNo = "UB0016027768",
            Bank = "TEB",
            TerminalId = "PS927768",
            MerchantNo = "495396",
            CashNo = 160
        });
        await contexts.MikroWrite.SaveChangesAsync();
        var service = CreateService(contexts);

        await service.DeleteCashRegisterTerminalAsync(160, 160, 1, CancellationToken.None);

        Assert.Single(await contexts.MikroWrite.CashRegisterDetails.ToArrayAsync());
        Assert.Single(await contexts.MikroWrite.CashRegisterBranches.ToArrayAsync());

        await service.DeleteCashRegisterTerminalAsync(160, 160, 2, CancellationToken.None);

        Assert.Empty(await contexts.MikroWrite.CashRegisterDetails.ToArrayAsync());
        Assert.Empty(await contexts.MikroWrite.CashRegisterBranches.ToArrayAsync());
    }

    [Fact]
    public async Task CreateUpdateAndDeleteTerminal_UsesMikroAndLeavesLegacyFurpaRowsUntouched()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegistryAsync(contexts.Furpa);
        contexts.Furpa.CashRegisterDetails.Add(new FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models.CashRegisterDetailEntity
        {
            Id = 10,
            CashRegisterNo = "UB0016027768",
            Bank = "Is Bankasi",
            TerminalId = "S0ML5F02",
            MerchantNo = "667125132",
            CashNo = 160
        });
        await contexts.Furpa.SaveChangesAsync();
        contexts.Furpa.ChangeTracker.Clear();
        var service = CreateService(contexts);

        var created = await service.CreateCashRegisterTerminalAsync(
            160,
            160,
            new CreateCashRegisterTerminalRequest("UB0016027768", "TEB", "PS927768", "495396"),
            CancellationToken.None);
        var updated = await service.UpdateCashRegisterTerminalAsync(
            160,
            160,
            created.Id,
            new CreateCashRegisterTerminalRequest("UB0016027768", "Yapi Kredi", "0037307B", "2684568492"),
            CancellationToken.None);

        Assert.Equal("Yapi Kredi", updated.Bank);
        Assert.Equal(1, await contexts.MikroWrite.CashRegisterDetails.CountAsync());
        Assert.Equal(1, await contexts.Furpa.CashRegisterDetails.CountAsync());

        await service.DeleteCashRegisterTerminalAsync(160, 160, created.Id, CancellationToken.None);

        Assert.Empty(await contexts.MikroWrite.CashRegisterDetails.ToArrayAsync());
        Assert.Single(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());
    }

    [Fact]
    public async Task DeleteCashRegister_LeavesLegacyFurpaTerminalRowsUntouched()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegistryAsync(contexts.Furpa);
        contexts.Furpa.CashRegisterDetails.Add(new FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models.CashRegisterDetailEntity
        {
            Id = 10,
            CashRegisterNo = "UB0016027768",
            Bank = "Is Bankasi",
            TerminalId = "S0ML5F02",
            MerchantNo = "667125132",
            CashNo = 160
        });
        await contexts.Furpa.SaveChangesAsync();
        contexts.Furpa.ChangeTracker.Clear();
        var service = CreateService(contexts);

        await service.DeleteCashRegisterAsync(160, 160, CancellationToken.None);

        Assert.Empty(await contexts.Furpa.CashRegistryDetails.ToArrayAsync());
        Assert.Single(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());
    }

    [Fact]
    public async Task ListCashRegisterTerminals_FiltersByBranchAndPhysicalCashNo()
    {
        await using var contexts = CreateContexts();
        contexts.MikroWrite.CashRegisterDetails.AddRange(
            new MikroCashRegisterDetailEntity
            {
                Id = 1,
                CashRegisterNo = "PAV-SHARED",
                Bank = "Akbank",
                TerminalId = "129-103",
                CashNo = 103
            },
            new MikroCashRegisterDetailEntity
            {
                Id = 2,
                CashRegisterNo = "PAV-SHARED",
                Bank = "TEB",
                TerminalId = "129-104",
                CashNo = 104
            },
            new MikroCashRegisterDetailEntity
            {
                Id = 3,
                CashRegisterNo = "PAV-OTHER",
                Bank = "Is Bankasi",
                TerminalId = "130-103",
                CashNo = 103
            });
        contexts.MikroWrite.CashRegisterBranches.AddRange(
            new CashRegisterBranchEntity { Id = 1, BranchNo = 129, CashRegisterNo = "PAV-SHARED" },
            new CashRegisterBranchEntity { Id = 2, BranchNo = 130, CashRegisterNo = "PAV-SHARED" },
            new CashRegisterBranchEntity { Id = 3, BranchNo = 130, CashRegisterNo = "PAV-OTHER" });
        await contexts.MikroWrite.SaveChangesAsync();
        var service = CreateService(contexts);

        var result = await service.ListCashRegisterTerminalsAsync(129, 103, CancellationToken.None);

        var terminal = Assert.Single(result);
        Assert.Equal("PAV-SHARED", terminal.TerminalNo);
        Assert.Equal("Akbank", terminal.Bank);
        Assert.Equal(103, terminal.CashNo);
    }

    [Fact]
    public async Task CreateTerminal_WhenPavBelongsToAnotherCash_ReportsItsLocation()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        contexts.Furpa.CashRegistryDetails.Add(new CashRegistryDetailEntity
        {
            DetailId = 2,
            BranchNo = 160,
            CashRegisterNo = 161,
            CashRegisterType = 0
        });
        await contexts.Furpa.SaveChangesAsync();
        var service = CreateService(contexts);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateCashRegisterTerminalAsync(
                160,
                161,
                new CreateCashRegisterTerminalRequest("UB0016027768", "TEB", "PS927768", "495396"),
                CancellationToken.None));

        Assert.Contains("160 sube / 160 kasa", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Dogru Kasaya Tasi", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MoveTerminalGroup_MovesEveryBankRowToTargetCash()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        contexts.Furpa.CashRegistryDetails.Add(new CashRegistryDetailEntity
        {
            DetailId = 2,
            BranchNo = 160,
            CashRegisterNo = 161,
            CashRegisterType = 0
        });
        contexts.MikroWrite.CashRegisterDetails.Add(new MikroCashRegisterDetailEntity
        {
            Id = 2,
            CashRegisterNo = "UB0016027768",
            Bank = "TEB",
            TerminalId = "PS927768",
            MerchantNo = "495396",
            CashNo = 160
        });
        await contexts.Furpa.SaveChangesAsync();
        await contexts.MikroWrite.SaveChangesAsync();
        contexts.Furpa.ChangeTracker.Clear();
        contexts.MikroWrite.ChangeTracker.Clear();
        var service = CreateService(contexts);

        var result = await service.MoveCashRegisterTerminalGroupAsync(
            160,
            160,
            "UB0016027768",
            new MoveCashRegisterTerminalGroupRequest(160, 161),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.Equal(161, item.CashNo));
        Assert.Equal(
            2,
            await contexts.MikroWrite.CashRegisterDetails.CountAsync(item => item.CashNo == 161));
        Assert.Single(await contexts.MikroWrite.CashRegisterBranches.ToArrayAsync());
    }

    [Fact]
    public async Task UpdateCashRegisterType_ChangesPhysicalCashWithoutTouchingTerminals()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        var service = CreateService(contexts);

        var result = await service.UpdateCashRegisterTypeAsync(
            160,
            160,
            new UpdateCashRegisterTypeRequest(1),
            CancellationToken.None);

        Assert.Equal(1, result.CashType);
        Assert.Equal("Ek Kasa", result.CashTypeName);
        Assert.Single(await contexts.MikroWrite.CashRegisterDetails.ToArrayAsync());
    }

    [Fact]
    public async Task UpdateTerminal_DoesNotSplitMultiBankPavGroup()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        contexts.MikroWrite.CashRegisterDetails.Add(new MikroCashRegisterDetailEntity
        {
            Id = 2,
            CashRegisterNo = "UB0016027768",
            Bank = "TEB",
            TerminalId = "PS927768",
            MerchantNo = "495396",
            CashNo = 160
        });
        await contexts.MikroWrite.SaveChangesAsync();
        contexts.MikroWrite.ChangeTracker.Clear();
        var service = CreateService(contexts);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateCashRegisterTerminalAsync(
                160,
                160,
                1,
                new CreateCashRegisterTerminalRequest(
                    "PAV210009999",
                    "Is Bankasi",
                    "S0ML5F02",
                    "667125132"),
                CancellationToken.None));

        Assert.Contains("tek satirdan degistirilemez", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            2,
            await contexts.MikroWrite.CashRegisterDetails.CountAsync(
                item => item.CashRegisterNo == "UB0016027768"));
    }

    private static AyarlarService CreateService(TestContexts contexts) =>
        new(contexts.Furpa, contexts.FurpaB2B, contexts.MikroWrite);

    private static async Task SeedCashRegisterAsync(TestContexts contexts)
    {
        await SeedCashRegistryAsync(contexts.Furpa);
        contexts.MikroWrite.CashRegisterDetails.Add(new MikroCashRegisterDetailEntity
        {
            Id = 1,
            CashRegisterNo = "UB0016027768",
            Bank = "Is Bankasi",
            TerminalId = "S0ML5F02",
            MerchantNo = "667125132",
            CashNo = 160
        });
        contexts.MikroWrite.CashRegisterBranches.Add(new CashRegisterBranchEntity
        {
            Id = 1,
            CashRegisterNo = "UB0016027768",
            BranchNo = 160
        });

        await contexts.MikroWrite.SaveChangesAsync();
        contexts.MikroWrite.ChangeTracker.Clear();
    }

    private static async Task SeedCashRegistryAsync(FurpaDbContext furpaDbContext)
    {
        furpaDbContext.CashRegistryDetails.Add(new CashRegistryDetailEntity
        {
            DetailId = 1,
            BranchNo = 160,
            CashRegisterNo = 160,
            CashRegisterType = 0
        });
        await furpaDbContext.SaveChangesAsync();
        furpaDbContext.ChangeTracker.Clear();
    }

    private static TestContexts CreateContexts()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new TestContexts(
            new FurpaDbContext(new DbContextOptionsBuilder<FurpaDbContext>()
                .UseInMemoryDatabase($"terminal-furpa-{suffix}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options),
            new FurpaB2BDbContext(new DbContextOptionsBuilder<FurpaB2BDbContext>()
                .UseInMemoryDatabase($"terminal-b2b-{suffix}")
                .Options),
            new MikroWriteDbContext(new DbContextOptionsBuilder<MikroWriteDbContext>()
                .UseInMemoryDatabase($"terminal-mikro-{suffix}")
                .Options));
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
