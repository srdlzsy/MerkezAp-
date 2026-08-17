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
            new GiftCheckTypeEntity { GiftCheckType = 1, Value = 500 },
            new GiftCheckTypeEntity { GiftCheckType = 2, Value = 1000 });

        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListGiftCheckTypesAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "Hediye Çeki 500 TL", "Hediye Çeki 1000 TL" },
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
    public async Task ListOnlineSalesPaymentTypesAsync_ReturnsEmptyAccountCodeWhenDatabaseValueIsNull()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.Add(
            CreatePaymentType(70, "Online Satis", accountCode: null, paymentGenus: 5));

        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListOnlineSalesPaymentTypesAsync(CancellationToken.None);
        var item = Assert.Single(result);

        Assert.Equal("Online Satis", item.PaymentName);
        Assert.Equal(string.Empty, item.AccountCode);
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
    public async Task GetCashRegisterDetailAsync_ReadsTerminalDetailFromFurpaDatabase()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.CashRegisterDetails.Add(new MikroCashRegisterDetailEntity
        {
            Id = 41,
            CashRegisterNo = "UB0016026618",
            Bank = "Halkbank",
            TerminalId = "01106322",
            MerchantNo = "000000002066032",
            CashNo = 33
        });
        furpaDbContext.CashRegisterDetails.Add(new FurpaCashRegisterDetailEntity
        {
            Id = 3528,
            CashRegisterNo = "PAV210010590",
            Bank = "Is Bankasi",
            TerminalId = "S0Q1FF05",
            MerchantNo = "000000668389976",
            CashNo = 33
        });

        await mikroDbContext.SaveChangesAsync();
        await furpaDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.GetCashRegisterDetailAsync(
            new CashRegisterLookupRequest(33, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3528, result.Id);
        Assert.Equal("PAV210010590", result.CashRegisterNo);
        Assert.Equal("S0Q1FF05", result.TerminalId);
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
        string terminalId) =>
        new()
        {
            Id = id,
            CashRegisterNo = cashRegisterNo,
            Bank = bank,
            TerminalId = terminalId,
            MerchantNo = string.Empty,
            CashNo = null
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
