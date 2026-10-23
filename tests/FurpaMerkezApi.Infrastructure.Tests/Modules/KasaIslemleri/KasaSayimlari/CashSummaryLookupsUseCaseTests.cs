using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Lookups;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FurpaCashRegisterDetailEntity = FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models.CashRegisterDetailEntity;
using MikroCashRegisterDetailEntity = FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models.CashRegisterDetailEntity;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaSayimlari;

public sealed class CashSummaryLookupsUseCaseTests
{
    [Fact]
    public async Task ListBanknoteTypesAsync_ReturnsDisplayNameFromValue()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.BanknoteTypes.AddRange(
            new BanknoteTypeEntity { BanknoteType = 1, Value = 200 },
            new BanknoteTypeEntity { BanknoteType = 2, Value = 100 });

        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBanknoteTypesAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "100 TL", "200 TL" },
            result.Select(item => item.BanknoteTypeName).ToArray());
    }

    [Fact]
    public async Task ListGiftCheckTypesAsync_ReturnsDisplayNameFromValue()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.GiftCheckTypes.AddRange(
            new GiftCheckTypeEntity { GiftCheckType = 11, Value = 1 },
            new GiftCheckTypeEntity { GiftCheckType = 1, Value = 500 },
            new GiftCheckTypeEntity { GiftCheckType = 2, Value = 1000 });

        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListGiftCheckTypesAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "Birlik Premium Kart", "Hediye Çeki 500 TL", "Hediye Çeki 1000 TL" },
            result.Select(item => item.GiftCheckTypeName).ToArray());
    }

    [Fact]
    public async Task ListFoodCheckPaymentTypesAsync_ReturnsPaymentNameAndAccountCode()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(49, "Furpa Yemek Kart", "108.02.000", paymentGenus: 2),
            CreatePaymentType(50, "Sodexo POS", "108.02.001", paymentGenus: 2),
            CreatePaymentType(52, "Ticket POS", "108.02.002", paymentGenus: 2),
            CreatePaymentType(1, "Akbank", "108.01.001"));

        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListFoodCheckPaymentTypesAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "Furpa Yemek Kart:108.02.000", "Sodexo POS:108.02.001", "Ticket POS:108.02.002" },
            result.Select(item => $"{item.PaymentName}:{item.AccountCode}").ToArray());
    }

    [Fact]
    public async Task ListOnlineSalesPaymentTypesAsync_ReturnsNamedOnlineAndGenusFivePaymentTypes()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(10, "Online Odeme", "0021", paymentGenus: 1),
            CreatePaymentType(600, "Trendyol", "0013", paymentGenus: 5),
            CreatePaymentType(601, "Yemek Sepeti", "0014", paymentGenus: 5),
            CreatePaymentType(602, "Diger Odeme", "0015", paymentGenus: 1));

        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListOnlineSalesPaymentTypesAsync(CancellationToken.None);
        Assert.Equal(
            new[]
            {
                "10:Online Odeme:1:0021",
                "600:Trendyol:5:0013",
                "601:Yemek Sepeti:5:0014"
            },
            result.Select(item =>
                $"{item.PaymentTypeNo}:{item.PaymentName}:{item.PaymentGenus}:{item.AccountCode}").ToArray());
    }

    [Fact]
    public async Task ListExpenseCompassPaymentTypesAsync_ReturnsFallbackWhenDatabaseHasNoExpenseCompass()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListExpenseCompassPaymentTypesAsync(CancellationToken.None);
        var item = Assert.Single(result);

        Assert.Equal("Gider Pusulası", item.PaymentName);
        Assert.Equal(100, item.PaymentTypeNo);
        Assert.Equal(100, item.PaymentTypeId);
        Assert.Equal("100||", item.PaymentTypeKey);
    }

    [Fact]
    public async Task ListBankPaymentTypesAsync_ReturnsAllBankTerminalsForCashRegister()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(1, "Akbank", "108.01.001"),
            CreatePaymentType(2, "Halkbank", "108.01.002"),
            CreatePaymentType(3, "Isbank", "108.01.003"),
            CreatePaymentType(50, "Sodexo", "108.02.001", paymentGenus: 2));

        furpaDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(1, "UB0016026511", "Akbank", "T001"),
            CreateCashRegisterDetail(2, "UB0016026511", "Halkbank", "T002"),
            CreateCashRegisterDetail(3, "UB0016026511", "Isbank", "T003"),
            CreateCashRegisterDetail(4, "OTHER", "Akbank", "T999"));

        await mikroDbContext.SaveChangesAsync();
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest("UB0016026511"),
            CancellationToken.None);

        Assert.Equal(
            new[] { "Akbank:T001:108.01.001", "Halkbank:T002:108.01.002", "Isbank:T003:108.01.003" },
            result.Select(item => $"{item.PaymentName}:{item.TerminalId}:{item.AccountCode}").ToArray());

        Assert.Equal(
            new[] { "1|108.01.001|T001", "2|108.01.002|T002", "3|108.01.003|T003" },
            result.Select(item => item.PaymentTypeKey).ToArray());
    }

    [Fact]
    public async Task ListBankPaymentTypesAsync_IgnoresCashRegisterRowsWithMissingBank()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(1, "Akbank", "108.01.001"),
            CreatePaymentType(2, "Halkbank", "108.01.002"));

        furpaDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(1, "UB0016026394", "Akbank", "T001"),
            new FurpaCashRegisterDetailEntity
            {
                Id = 2,
                CashRegisterNo = "UB0016026394",
                Bank = null,
                TerminalId = null,
                MerchantNo = null,
                CashNo = null
            });

        await mikroDbContext.SaveChangesAsync();
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest("UB0016026394"),
            CancellationToken.None);

        var item = Assert.Single(result);
        Assert.Equal("Akbank", item.PaymentName);
        Assert.Equal("T001", item.TerminalId);
        Assert.Equal("108.01.001", item.AccountCode);
    }

    [Fact]
    public async Task ListBankPaymentTypesAsync_UsesFurpaWhenMikroHasNoTerminal()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(3, "Is Bankasi", "108.01.003"),
            CreatePaymentType(4, "TEB", "108.01.004"));

        furpaDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(1, "PAV210010590", "Is Bankasi", "S0Q1FF05"),
            CreateCashRegisterDetail(2, "PAV210010590", "TEB", "PSB47068"));

        await mikroDbContext.SaveChangesAsync();
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest("PAV210010590"),
            CancellationToken.None);

        Assert.Equal(
            new[] { "Is Bankasi:S0Q1FF05", "TEB:PSB47068" },
            result.Select(item => $"{item.PaymentName}:{item.TerminalId}").ToArray());
    }

    [Fact]
    public async Task ListBankPaymentTypesAsync_UsesOnlyFurpaTerminalRows()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(3, "Is Bankasi", "0004"),
            CreatePaymentType(4, "TEB", "0007"),
            CreatePaymentType(5, "Yapi Kredi", "0011"),
            CreatePaymentType(6, "Ziraat Bankasi", "0019"));

        mikroDbContext.CashRegisterDetails.AddRange(
            CreateMikroCashRegisterDetail(1, "SD0024109036", "TEB", "PS373078", "495383"),
            CreateMikroCashRegisterDetail(2, "SD0024109036", "Is Bankasi", "SO28AL09", "656322211"));

        furpaDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(3, "SD0024109036", "Ziraat Bankasi", "2547147", "3595242"),
            CreateCashRegisterDetail(4, "SD0024109036", "Is Bankasi", "S028AL09", "656322211"),
            CreateCashRegisterDetail(5, "SD0024109036", "Yapi Kredi", "00C9814B", "2602669384"));

        await mikroDbContext.SaveChangesAsync();
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest("SD0024109036"),
            CancellationToken.None);

        Assert.Equal(
            new[] { "Is Bankasi:S028AL09:0004", "Yapi Kredi:00C9814B:0011", "Ziraat Bankasi:2547147:0019" },
            result.Select(item => $"{item.PaymentName}:{item.TerminalId}:{item.AccountCode}").ToArray());
    }

    [Fact]
    public async Task ListBankPaymentTypesAsync_PreservesDistinctTerminalsSharingMerchantNumber()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.Add(CreatePaymentType(1, "Akbank", "0001"));
        mikroDbContext.CashRegisterDetails.AddRange(
            CreateMikroCashRegisterDetail(1, "POS001", "Akbank", "M-T01", "MERCHANT-1"),
            CreateMikroCashRegisterDetail(2, "POS001", "Akbank", "M-T02", "MERCHANT-1"));
        furpaDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(3, "POS001", "Akbank", "F-T01", "MERCHANT-1"),
            CreateCashRegisterDetail(4, "POS001", "Akbank", "F-T02", "MERCHANT-1"));

        await mikroDbContext.SaveChangesAsync();
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest("POS001"),
            CancellationToken.None);

        Assert.Equal(
            new[] { "Akbank:F-T01:0001", "Akbank:F-T02:0001" },
            result.Select(item => $"{item.PaymentName}:{item.TerminalId}:{item.AccountCode}").ToArray());
    }

    [Fact]
    public async Task ListBankPaymentTypesAsync_FiltersByPhysicalCashAndBranch()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(1, "Akbank", "0001"),
            CreatePaymentType(2, "TEB", "0002"),
            CreatePaymentType(3, "Is Bankasi", "0003"));
        furpaDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(1, "PAV-COMMON", "Akbank", "T-129-A", cashNo: 103),
            CreateCashRegisterDetail(2, "PAV-COMMON", "TEB", "T-130-B", cashNo: 104),
            CreateCashRegisterDetail(3, "PAV-OTHER", "Is Bankasi", "T-129-C", cashNo: 103));
        furpaDbContext.CashRegistryDetails.AddRange(
            new() { DetailId = 1, BranchNo = 129, CashRegisterNo = 103 },
            new() { DetailId = 2, BranchNo = 130, CashRegisterNo = 104 });
        await mikroDbContext.SaveChangesAsync();
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest(null, BranchNo: 129, CashNo: 103),
            CancellationToken.None);

        Assert.Equal(
            new[] { "Akbank:T-129-A", "Is Bankasi:T-129-C" },
            result.Select(item => $"{item.PaymentName}:{item.TerminalId}").ToArray());
    }

    [Fact]
    public async Task ListBankPaymentTypesAsync_RequiresBranchWhenFilteringByPhysicalCash()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();
        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest(null, CashNo: 103),
            CancellationToken.None));
    }

    [Fact]
    public async Task GetCashRegisterDetailAsync_UsesFurpaWhenLookingUpByCashNo()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        furpaDbContext.CashRegisterDetails.Add(new FurpaCashRegisterDetailEntity
        {
            Id = 3528,
            CashRegisterNo = "PAV210010590",
            Bank = "Is Bankasi",
            TerminalId = "S0Q1FF05",
            MerchantNo = "000000668389976",
            CashNo = 33
        });
        furpaDbContext.CashRegistryDetails.Add(
            new()
            {
                DetailId = 1,
                BranchNo = 129,
                CashRegisterNo = 33
            });

        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.GetCashRegisterDetailAsync(
            new CashRegisterLookupRequest(33, BranchNo: 129),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3528, result.Id);
        Assert.Equal("PAV210010590", result.CashRegisterNo);
        Assert.Equal("S0Q1FF05", result.TerminalId);
    }

    [Fact]
    public async Task GetCashRegisterDetailAsync_RejectsMultipleFinanceNumbersForPhysicalCash()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        furpaDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(41, "PAV210000001", "Akbank", "T001", cashNo: 103),
            CreateCashRegisterDetail(42, "PAV210000002", "TEB", "T002", cashNo: 103));
        furpaDbContext.CashRegistryDetails.Add(
            new() { DetailId = 1, BranchNo = 129, CashRegisterNo = 103 });
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.GetCashRegisterDetailAsync(
                new CashRegisterLookupRequest(103, null, BranchNo: 129),
                CancellationToken.None));

        Assert.Contains("birden fazla finans/PAV numarasi", exception.Message);
        Assert.Contains("PAV210000001", exception.Message);
        Assert.Contains("PAV210000002", exception.Message);
    }

    [Fact]
    public async Task GetCashRegisterDetailAsync_UsesFurpaWhenLookingUpByCashRegisterNo()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        furpaDbContext.CashRegisterDetails.Add(CreateCashRegisterDetail(
            3528,
            "UB0016026618",
            "Is Bankasi",
            "S0Q1FF05"));

        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.GetCashRegisterDetailAsync(
            new CashRegisterLookupRequest(null, "UB0016026618"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3528, result.Id);
        Assert.Equal("UB0016026618", result.CashRegisterNo);
        Assert.Equal("Is Bankasi", result.Bank);
        Assert.Equal("S0Q1FF05", result.TerminalId);
    }

    [Fact]
    public async Task GetCashRegisterDetailAsync_ReturnsFurpaMatchByRegisterNo()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        furpaDbContext.CashRegisterDetails.Add(CreateCashRegisterDetail(
            3528,
            "PAV210010590",
            "Is Bankasi",
            "S0Q1FF05"));

        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.GetCashRegisterDetailAsync(
            new CashRegisterLookupRequest(null, "PAV210010590"),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PAV210010590", result.CashRegisterNo);
    }

    [Fact]
    public async Task GetCashRegisterDetailAsync_DoesNotUseStaleMikroTerminalMatch()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.CashRegisterDetails.Add(CreateMikroCashRegisterDetail(
            41,
            "UB0016026618",
            "Halkbank",
            "01106322"));
        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.GetCashRegisterDetailAsync(
            new CashRegisterLookupRequest(null, "UB0016026618"),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCashRegisterDetailAsync_ReturnsEmptyStringsWhenOptionalTerminalFieldsAreNull()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        furpaDbContext.CashRegisterDetails.Add(new FurpaCashRegisterDetailEntity
        {
            Id = 3529,
            CashRegisterNo = "PAV210010591",
            Bank = null,
            TerminalId = null,
            MerchantNo = null,
            CashNo = 34
        });

        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.GetCashRegisterDetailAsync(
            new CashRegisterLookupRequest(34, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PAV210010591", result.CashRegisterNo);
        Assert.Equal(string.Empty, result.Bank);
        Assert.Equal(string.Empty, result.TerminalId);
        Assert.Equal(string.Empty, result.MerchantNo);
    }

    private static PaymentTypeEntity CreatePaymentType(
        int paymentTypeNo,
        string paymentName,
        string? accountCode,
        int paymentGenus = 1) =>
        new()
        {
            PaymentTypeNo = paymentTypeNo,
            PaymentName = paymentName,
            PaymentGenus = paymentGenus,
            AccountCode = accountCode
        };

    private static FurpaCashRegisterDetailEntity CreateCashRegisterDetail(
        int id,
        string cashRegisterNo,
        string bank,
        string terminalId,
        string merchantNo = "",
        int? cashNo = null) =>
        new()
        {
            Id = id,
            CashRegisterNo = cashRegisterNo,
            Bank = bank,
            TerminalId = terminalId,
            MerchantNo = merchantNo,
            CashNo = cashNo
        };

    private static MikroCashRegisterDetailEntity CreateMikroCashRegisterDetail(
        int id,
        string cashRegisterNo,
        string bank,
        string terminalId,
        string merchantNo = "",
        int? cashNo = null) =>
        new()
        {
            Id = id,
            CashRegisterNo = cashRegisterNo,
            Bank = bank,
            TerminalId = terminalId,
            MerchantNo = merchantNo,
            CashNo = cashNo
        };

    private static MikroDbContext CreateMikroDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"cash-summary-lookups-mikro-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }

    private static FurpaDbContext CreateFurpaDbContext()
    {
        var options = new DbContextOptionsBuilder<FurpaDbContext>()
            .UseInMemoryDatabase($"cash-summary-lookups-furpa-{Guid.NewGuid():N}")
            .Options;

        return new FurpaDbContext(options);
    }
}
