using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Lookups;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Lookups;

public sealed class CashSummaryLookupsUseCase(
    MikroDbContext mikroDbContext,
    FurpaDbContext furpaDbContext)
    : ICashSummaryLookupsUseCase
{
    public async Task<IReadOnlyCollection<CashierItemDto>> GetCashierAndManagerAsync(
        CashierPairRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(request.CashierCode, nameof(request.CashierCode));
        ValidatePositive(request.ManagerCode, nameof(request.ManagerCode));

        var requestedCodes = new[] { request.CashierCode, request.ManagerCode };

        return await furpaDbContext.Cashiers
            .AsNoTracking()
            .Where(item => requestedCodes.Contains(item.CashierCode))
            .OrderBy(item => item.CashierCode)
            .Select(item => new CashierItemDto(
                item.CashierId,
                item.CreateUser,
                item.CreateDate,
                item.UpdateUser,
                item.UpdateDate,
                item.CashierCode,
                item.CashierName,
                item.CashierPassword,
                item.CashierAuthorization,
                item.CashierState))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CashRegistryItemDto>> GetCashRegistriesAsync(
        CashRegistryRequest request,
        CancellationToken cancellationToken)
    {
        ValidatePositive(request.BranchNo, nameof(request.BranchNo));

        return await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == request.BranchNo)
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new CashRegistryItemDto(
                item.DetailId,
                item.BranchNo,
                item.CashRegisterNo,
                item.CashRegisterType))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<CashRegisterDetailDto?> GetCashRegisterDetailAsync(
        CashRegisterLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashNo is <= 0 && string.IsNullOrWhiteSpace(request.CashRegisterNo))
        {
            throw new ArgumentException("Either cash no or cash register no must be provided.");
        }

        var query = mikroDbContext.CashRegisterDetails
            .AsNoTracking()
            .AsQueryable();

        if (request.CashNo is > 0)
        {
            query = query.Where(item => item.CashNo == request.CashNo.Value);
        }
        else
        {
            var cashRegisterNo = request.CashRegisterNo!.Trim();
            query = query.Where(item => item.CashRegisterNo == cashRegisterNo);
        }

        return await query
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterDetailDto(
                item.Id,
                item.CashRegisterNo,
                item.Bank,
                item.TerminalId,
                item.MerchantNo,
                item.CashNo))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CashierSearchItemDto>> SearchCashiersAsync(
        CashierSearchRequest request,
        CancellationToken cancellationToken)
    {
        var filter = request.Filter?.Trim();

        if (string.IsNullOrWhiteSpace(filter))
        {
            throw new ArgumentException("Filter is required.", nameof(request.Filter));
        }

        return await furpaDbContext.Cashiers
            .AsNoTracking()
            .Where(item =>
                EF.Functions.Like(item.CashierName, $"%{filter}%") ||
                item.CashierCode.ToString().Contains(filter))
            .OrderBy(item => item.CashierName)
            .Select(item => new CashierSearchItemDto(
                item.CashierCode,
                item.CashierName,
                item.CashierPassword,
                item.CashierAuthorization,
                item.CashierState))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BanknoteTypeItemDto>> ListBanknoteTypesAsync(
        CancellationToken cancellationToken) =>
        await mikroDbContext.BanknoteTypes
            .AsNoTracking()
            .OrderBy(item => item.Value)
            .Select(item => new BanknoteTypeItemDto(
                item.Value,
                0d,
                0d,
                item.BanknoteType))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<GiftCheckTypeItemDto>> ListGiftCheckTypesAsync(
        CancellationToken cancellationToken) =>
        await mikroDbContext.GiftCheckTypes
            .AsNoTracking()
            .OrderBy(item => item.Value)
            .Select(item => new GiftCheckTypeItemDto(
                item.Value,
                0d,
                0d,
                item.GiftCheckType))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListBankPaymentTypesAsync(
        BankPaymentTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CashRegisterNo))
        {
            throw new ArgumentException("Cash register no is required.", nameof(request.CashRegisterNo));
        }

        var cashRegisterNo = request.CashRegisterNo.Trim();
        var cashRegister = await mikroDbContext.CashRegisterDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CashRegisterNo == cashRegisterNo, cancellationToken);
        var bankName = cashRegister?.Bank;

        var paymentTypes = await mikroDbContext.PaymentTypes
            .AsNoTracking()
            .OrderBy(item => item.PaymentName)
            .ToArrayAsync(cancellationToken);

        return paymentTypes
            .Where(item => CashSummaryCategoryMatcher.IsBankPaymentMatch(item.PaymentName, bankName))
            .Select(item => new PaymentTypeItemDto(
                item.PaymentName,
                item.PaymentTypeNo,
                cashRegister?.TerminalId ?? string.Empty,
                string.Empty,
                0,
                0d))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListFoodCheckPaymentTypesAsync(
        CancellationToken cancellationToken) =>
        await ListPaymentTypesByPredicateAsync(
            CashSummaryCategoryMatcher.IsFoodCheckPaymentType,
            cancellationToken);

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListOnlineSalesPaymentTypesAsync(
        CancellationToken cancellationToken) =>
        await ListPaymentTypesByPredicateAsync(
            CashSummaryCategoryMatcher.IsOnlineSalesPaymentType,
            cancellationToken);

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListExpenseCompassPaymentTypesAsync(
        CancellationToken cancellationToken) =>
        await ListPaymentTypesByPredicateAsync(
            CashSummaryCategoryMatcher.IsExpenseCompassPaymentType,
            cancellationToken);

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListStoreExpensePaymentTypesAsync(
        CancellationToken cancellationToken) =>
        await ListPaymentTypesByPredicateAsync(
            CashSummaryCategoryMatcher.IsStoreExpensePaymentType,
            cancellationToken);

    public async Task<IReadOnlyCollection<CashRegisterDetailDto>> ListOnlineCashRegistersAsync(
        CancellationToken cancellationToken) =>
        await mikroDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.Bank.ToLower().Contains("online"))
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new CashRegisterDetailDto(
                item.Id,
                item.CashRegisterNo,
                item.Bank,
                item.TerminalId,
                item.MerchantNo,
                item.CashNo))
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListPaymentTypesByPredicateAsync(
        Func<string?, bool> predicate,
        CancellationToken cancellationToken)
    {
        var paymentTypes = await mikroDbContext.PaymentTypes
            .AsNoTracking()
            .OrderBy(item => item.PaymentName)
            .ToArrayAsync(cancellationToken);

        return paymentTypes
            .Where(item => predicate(item.PaymentName))
            .Select(item => new PaymentTypeItemDto(
                item.PaymentName,
                item.PaymentTypeNo,
                string.Empty,
                string.Empty,
                0,
                0d))
            .ToArray();
    }

    private static void ValidatePositive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException("Value must be greater than zero.", paramName);
        }
    }
}
