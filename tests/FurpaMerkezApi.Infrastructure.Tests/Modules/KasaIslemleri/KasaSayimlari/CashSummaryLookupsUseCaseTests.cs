using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Lookups;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaSayimlari;

public sealed class CashSummaryLookupsUseCaseTests
{
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
    public async Task ListBankPaymentTypesAsync_ReturnsAllBankTerminalsForCashRegister()
    {
        await using var mikroDbContext = CreateMikroDbContext();
        await using var furpaDbContext = CreateFurpaDbContext();

        mikroDbContext.PaymentTypes.AddRange(
            CreatePaymentType(1, "Akbank", "108.01.001"),
            CreatePaymentType(2, "Halkbank", "108.01.002"),
            CreatePaymentType(3, "Isbank", "108.01.003"),
            CreatePaymentType(50, "Sodexo", "108.02.001", paymentGenus: 2));

        mikroDbContext.CashRegisterDetails.AddRange(
            CreateCashRegisterDetail(1, "UB0016026511", "Akbank", "T001"),
            CreateCashRegisterDetail(2, "UB0016026511", "Halkbank", "T002"),
            CreateCashRegisterDetail(3, "UB0016026511", "Isbank", "T003"),
            CreateCashRegisterDetail(4, "OTHER", "Akbank", "T999"));

        await mikroDbContext.SaveChangesAsync();

        var useCase = new CashSummaryLookupsUseCase(mikroDbContext, furpaDbContext);

        var result = await useCase.ListBankPaymentTypesAsync(
            new BankPaymentTypeRequest("UB0016026511"),
            CancellationToken.None);

        Assert.Equal(
            new[] { "Akbank:T001:108.01.001", "Halkbank:T002:108.01.002", "Isbank:T003:108.01.003" },
            result.Select(item => $"{item.PaymentName}:{item.TerminalId}:{item.AccountCode}").ToArray());
    }

    private static PaymentTypeEntity CreatePaymentType(
        int paymentTypeNo,
        string paymentName,
        string accountCode,
        int paymentGenus = 1) =>
        new()
        {
            PaymentTypeNo = paymentTypeNo,
            PaymentName = paymentName,
            PaymentGenus = paymentGenus,
            AccountCode = accountCode
        };

    private static CashRegisterDetailEntity CreateCashRegisterDetail(
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
