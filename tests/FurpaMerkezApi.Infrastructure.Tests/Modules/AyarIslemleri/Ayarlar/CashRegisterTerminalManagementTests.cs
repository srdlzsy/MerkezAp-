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
using FurpaCashRegisterDetailEntity = FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models.CashRegisterDetailEntity;
using MikroCashRegisterDetailEntity = FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models.CashRegisterDetailEntity;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.AyarIslemleri.Ayarlar;

public sealed class CashRegisterTerminalManagementTests
{
    [Fact]
    public async Task CreateCashRegister_CanonicalizesEveryBankBeforeWriting()
    {
        await using var contexts = CreateContexts();
        contexts.Furpa.BranchDetails.Add(new BranchDetailEntity { BranchNo = 160 });
        await contexts.Furpa.SaveChangesAsync();
        var service = CreateService(contexts);

        var result = await service.CreateCashRegisterAsync(
            new CreateCashRegisterRequest(
                160,
                160,
                0,
                [
                    new CreateCashRegisterTerminalRequest("PAV-160", "teb", "T-1", "M-1"),
                    new CreateCashRegisterTerminalRequest("PAV-160", "yapi kredi", "T-2", "M-2")
                ]),
            CancellationToken.None);

        Assert.Equal(["TEB", "Yapi Kredi"], result.Terminals.Select(item => item.Bank).ToArray());
        Assert.Single(await contexts.Furpa.CashRegistryDetails.ToArrayAsync());
        Assert.Equal(2, await contexts.Furpa.CashRegisterDetails.CountAsync());
    }

    [Fact]
    public async Task CreateCashRegister_WithUnknownBank_DoesNotWritePartialCash()
    {
        await using var contexts = CreateContexts();
        contexts.Furpa.BranchDetails.Add(new BranchDetailEntity { BranchNo = 160 });
        await contexts.Furpa.SaveChangesAsync();
        var service = CreateService(contexts);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateCashRegisterAsync(
                new CreateCashRegisterRequest(
                    160,
                    160,
                    0,
                    [new CreateCashRegisterTerminalRequest("PAV-160", "Tanimsiz Banka", "T-1", "M-1")]),
                CancellationToken.None));

        Assert.Empty(await contexts.Furpa.CashRegistryDetails.ToArrayAsync());
        Assert.Empty(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());
    }

    [Fact]
    public async Task CreateTerminal_CanonicalizesBankNameFromPaymentCatalog()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        var service = CreateService(contexts);

        var result = await service.CreateCashRegisterTerminalAsync(
            160,
            160,
            new CreateCashRegisterTerminalRequest("UB0016027768", "teb", "PS927768", "495396"),
            CancellationToken.None);

        Assert.Equal("TEB", result.Bank);
    }

    [Fact]
    public async Task CreateTerminal_RejectsBankOutsidePaymentCatalog()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        var service = CreateService(contexts);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateCashRegisterTerminalAsync(
                160,
                160,
                new CreateCashRegisterTerminalRequest("UB0016027768", "Tanimsiz Banka", "X-1", "M-1"),
                CancellationToken.None));

        Assert.Contains("PaymentTypes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, await contexts.Furpa.CashRegisterDetails.CountAsync());
    }

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
        Assert.Equal(2, await contexts.Furpa.CashRegisterDetails.CountAsync());
    }

    [Fact]
    public async Task DeleteTerminal_KeepsGroupMappingUntilLastBankRowIsDeleted()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        contexts.Furpa.CashRegisterDetails.Add(new FurpaCashRegisterDetailEntity
        {
            Id = 2,
            CashRegisterNo = "UB0016027768",
            Bank = "TEB",
            TerminalId = "PS927768",
            MerchantNo = "495396",
            CashNo = 160
        });
        await contexts.Furpa.SaveChangesAsync();
        var service = CreateService(contexts);

        await service.DeleteCashRegisterTerminalAsync(160, 160, 1, CancellationToken.None);

        Assert.Single(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());

        await service.DeleteCashRegisterTerminalAsync(160, 160, 2, CancellationToken.None);

        Assert.Empty(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());
    }

    [Fact]
    public async Task CreateUpdateAndDeleteTerminal_UsesFurpaSource()
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
        Assert.Empty(await contexts.MikroWrite.CashRegisterDetails.ToArrayAsync());
        Assert.Equal(2, await contexts.Furpa.CashRegisterDetails.CountAsync());

        await service.DeleteCashRegisterTerminalAsync(160, 160, created.Id, CancellationToken.None);

        Assert.Single(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());
    }

    [Fact]
    public async Task DeleteCashRegister_RemovesFurpaTerminalRows()
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
        Assert.Empty(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());
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
        contexts.Furpa.CashRegistryDetails.Add(
            new CashRegistryDetailEntity { DetailId = 1, BranchNo = 129, CashRegisterNo = 103 });
        contexts.Furpa.CashRegisterDetails.Add(
            new FurpaCashRegisterDetailEntity
            {
                Id = 10,
                CashRegisterNo = "PAV-FURPA",
                Bank = "Halkbank",
                TerminalId = "FURPA-129-103",
                CashNo = 103
            });
        await contexts.Furpa.SaveChangesAsync();
        var service = CreateService(contexts);

        var result = await service.ListCashRegisterTerminalsAsync(129, 103, CancellationToken.None);

        var terminal = Assert.Single(result);
        Assert.Equal("PAV-FURPA", terminal.TerminalNo);
        Assert.Equal("Halkbank", terminal.Bank);
        Assert.Equal("FURPA-129-103", terminal.TerminalId);
        Assert.Equal(103, terminal.CashNo);
    }

    [Fact]
    public async Task ListBranchCashRegisters_UsesFurpaFinanceNumberInsteadOfStaleMikroValue()
    {
        await using var contexts = CreateContexts();
        contexts.Furpa.CashRegistryDetails.Add(
            new CashRegistryDetailEntity { DetailId = 1, BranchNo = 145, CashRegisterNo = 163 });
        contexts.Furpa.CashRegisterDetails.Add(
            new FurpaCashRegisterDetailEntity
            {
                Id = 7154,
                CashRegisterNo = "PAV210009468",
                Bank = "TEB",
                TerminalId = "PSB75693",
                CashNo = 163
            });
        contexts.MikroWrite.CashRegisterDetails.Add(
            new MikroCashRegisterDetailEntity
            {
                Id = 11,
                CashRegisterNo = "UB0016025844",
                Bank = "TEB",
                TerminalId = "PS853990",
                CashNo = 163
            });
        await contexts.Furpa.SaveChangesAsync();
        await contexts.MikroWrite.SaveChangesAsync();
        var service = CreateService(contexts);

        var result = await service.ListBranchCashRegistersAsync(145, CancellationToken.None);

        var cashRegister = Assert.Single(result);
        Assert.Equal(163, cashRegister.CashNo);
        Assert.Equal("PAV210009468", cashRegister.CashFinanceNumber);
    }

    [Fact]
    public async Task ListBranchCashRegisters_RejectsMultipleFurpaFinanceNumbers()
    {
        await using var contexts = CreateContexts();
        contexts.Furpa.CashRegistryDetails.Add(
            new CashRegistryDetailEntity { DetailId = 1, BranchNo = 145, CashRegisterNo = 163 });
        contexts.Furpa.CashRegisterDetails.AddRange(
            new FurpaCashRegisterDetailEntity { Id = 1, CashRegisterNo = "PAV-A", Bank = "TEB", CashNo = 163 },
            new FurpaCashRegisterDetailEntity { Id = 2, CashRegisterNo = "PAV-B", Bank = "Is Bankasi", CashNo = 163 });
        await contexts.Furpa.SaveChangesAsync();
        var service = CreateService(contexts);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ListBranchCashRegistersAsync(145, CancellationToken.None));

        Assert.Contains("PAV-A", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PAV-B", exception.Message, StringComparison.Ordinal);
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
        contexts.Furpa.CashRegisterDetails.Add(new FurpaCashRegisterDetailEntity
        {
            Id = 2,
            CashRegisterNo = "UB0016027768",
            Bank = "TEB",
            TerminalId = "PS927768",
            MerchantNo = "495396",
            CashNo = 160
        });
        await contexts.Furpa.SaveChangesAsync();
        contexts.Furpa.ChangeTracker.Clear();
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
            await contexts.Furpa.CashRegisterDetails.CountAsync(item => item.CashNo == 161));
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
        Assert.Single(await contexts.Furpa.CashRegisterDetails.ToArrayAsync());
    }

    [Fact]
    public async Task UpdateTerminal_DoesNotSplitMultiBankPavGroup()
    {
        await using var contexts = CreateContexts();
        await SeedCashRegisterAsync(contexts);
        contexts.Furpa.CashRegisterDetails.Add(new FurpaCashRegisterDetailEntity
        {
            Id = 2,
            CashRegisterNo = "UB0016027768",
            Bank = "TEB",
            TerminalId = "PS927768",
            MerchantNo = "495396",
            CashNo = 160
        });
        await contexts.Furpa.SaveChangesAsync();
        contexts.Furpa.ChangeTracker.Clear();
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
            await contexts.Furpa.CashRegisterDetails.CountAsync(
                item => item.CashRegisterNo == "UB0016027768"));
    }

    [Fact]
    public async Task ListDefinitionIssues_ReportsMissingPavMultipleGroupsAndUnknownBank()
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
        contexts.Furpa.CashRegisterDetails.AddRange(
            new FurpaCashRegisterDetailEntity
            {
                Id = 2,
                CashRegisterNo = "PAV-SECOND",
                Bank = "TEB",
                TerminalId = "T-2",
                MerchantNo = "M-2",
                CashNo = 160
            },
            new FurpaCashRegisterDetailEntity
            {
                Id = 3,
                CashRegisterNo = "UB0016027768",
                Bank = "Eski Banka",
                TerminalId = "T-3",
                MerchantNo = "M-3",
                CashNo = 160
            });
        await contexts.Furpa.SaveChangesAsync();
        var service = CreateService(contexts);

        var result = await service.ListCashRegisterDefinitionIssuesAsync(160, CancellationToken.None);

        Assert.Contains(result, item => item.Code == "MISSING_TERMINAL_GROUP" && item.CashNo == 161);
        Assert.Contains(result, item => item.Code == "MULTIPLE_TERMINAL_GROUPS" && item.CashNo == 160);
        Assert.Contains(result, item => item.Code == "UNKNOWN_BANK" && item.Bank == "Eski Banka");
    }

    private static AyarlarService CreateService(TestContexts contexts) =>
        new(contexts.Furpa, contexts.FurpaB2B, contexts.MikroWrite);

    private static async Task SeedCashRegisterAsync(TestContexts contexts)
    {
        await SeedCashRegistryAsync(contexts.Furpa);
        contexts.Furpa.CashRegisterDetails.Add(new FurpaCashRegisterDetailEntity
        {
            Id = 1,
            CashRegisterNo = "UB0016027768",
            Bank = "Is Bankasi",
            TerminalId = "S0ML5F02",
            MerchantNo = "667125132",
            CashNo = 160
        });
        await contexts.Furpa.SaveChangesAsync();
        contexts.Furpa.ChangeTracker.Clear();
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
        var contexts = new TestContexts(
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

        contexts.MikroWrite.PaymentTypes.AddRange(
            new PaymentTypeEntity { PaymentTypeNo = 1, PaymentName = "Akbank", PaymentGenus = 1, AccountCode = "0001" },
            new PaymentTypeEntity { PaymentTypeNo = 4, PaymentName = "TEB", PaymentGenus = 1, AccountCode = "0007" },
            new PaymentTypeEntity { PaymentTypeNo = 5, PaymentName = "Is Bankasi", PaymentGenus = 1, AccountCode = "0004" },
            new PaymentTypeEntity { PaymentTypeNo = 6, PaymentName = "Yapi Kredi", PaymentGenus = 1, AccountCode = "0011" },
            new PaymentTypeEntity { PaymentTypeNo = 7, PaymentName = "Halkbank", PaymentGenus = 1, AccountCode = "0012" });
        contexts.MikroWrite.SaveChanges();

        return contexts;
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
