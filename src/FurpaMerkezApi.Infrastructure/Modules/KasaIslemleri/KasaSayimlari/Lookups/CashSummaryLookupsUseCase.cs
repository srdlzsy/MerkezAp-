using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
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

        var cashRegisters = await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == request.BranchNo)
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new
            {
                item.DetailId,
                item.BranchNo,
                item.CashRegisterNo,
                item.CashRegisterType
            })
            .ToArrayAsync(cancellationToken);

        return cashRegisters
            .Select(item =>
            {
                var option = SettingsTypeCatalog.ResolveCashTypeOption(item.CashRegisterType);
                return new CashRegistryItemDto(
                    item.DetailId,
                    item.BranchNo,
                    item.CashRegisterNo,
                    item.CashRegisterType,
                    option.Name,
                    option.Description);
            })
            .ToArray();
    }

    public async Task<CashRegisterDetailDto?> GetCashRegisterDetailAsync(
        CashRegisterLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashNo is <= 0 && string.IsNullOrWhiteSpace(request.CashRegisterNo))
        {
            throw new ArgumentException("Either cash no or cash register no must be provided.");
        }

        CashRegisterTerminalSource? detail;
        if (!string.IsNullOrWhiteSpace(request.CashRegisterNo))
        {
            var cashRegisterNo = request.CashRegisterNo.Trim();
            detail = await GetMikroCashRegisterDetailByRegisterNoAsync(cashRegisterNo, cancellationToken)
                ?? await GetFurpaCashRegisterDetailByRegisterNoAsync(cashRegisterNo, cancellationToken);
        }
        else
        {
            var cashNo = request.CashNo!.Value;
            detail = await GetFurpaCashRegisterDetailByCashNoAsync(cashNo, cancellationToken)
                ?? await GetMikroCashRegisterDetailByCashNoAsync(cashNo, cancellationToken);
        }

        return detail is null ? null : ToCashRegisterDetailDto(detail);
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
        var cashRegisterTerminals = await ListMikroCashRegisterDetailsByRegisterNoAsync(
            cashRegisterNo,
            cancellationToken);

        var terminalBanks = cashRegisterTerminals
            .Select(item => item.Bank)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (terminalBanks.Length == 0)
        {
            cashRegisterTerminals = await ListFurpaCashRegisterDetailsByRegisterNoAsync(
                cashRegisterNo,
                cancellationToken);

            terminalBanks = cashRegisterTerminals
                .Select(item => item.Bank)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (terminalBanks.Length == 0)
            {
                return [];
            }
        }

        var paymentTypeRows = await mikroDbContext.PaymentTypes
            .AsNoTracking()
            .Where(item => item.PaymentGenus == 1 && terminalBanks.Contains(item.PaymentName ?? string.Empty))
            .Select(item => new
            {
                PaymentName = item.PaymentName ?? string.Empty,
                item.PaymentTypeNo,
                AccountCode = item.AccountCode ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        var paymentTypes = (
            from paymentType in paymentTypeRows
            join cashRegister in cashRegisterTerminals
                on paymentType.PaymentName equals cashRegister.Bank
            orderby paymentType.PaymentName, cashRegister.TerminalId
            select new
            {
                paymentType.PaymentName,
                paymentType.PaymentTypeNo,
                paymentType.AccountCode,
                cashRegister.TerminalId
            }).ToArray();

        return paymentTypes
            .Select(item => new PaymentTypeItemDto(
                item.PaymentName,
                item.PaymentTypeNo,
                item.TerminalId,
                item.AccountCode,
                0,
                0d))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListFoodCheckPaymentTypesAsync(
        CancellationToken cancellationToken) =>
        await ListPaymentTypesByGenusAsync(2, cancellationToken);

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListOnlineSalesPaymentTypesAsync(
        CancellationToken cancellationToken) =>
        await ListPaymentTypesByPredicateAsync(
            CashSummaryCategoryMatcher.IsOnlineSalesPaymentType,
            cancellationToken);

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListExpenseCompassPaymentTypesAsync(
        CancellationToken cancellationToken)
    {
        var paymentTypes = await ListPaymentTypesByPredicateAsync(
            CashSummaryCategoryMatcher.IsExpenseCompassPaymentType,
            cancellationToken);

        return paymentTypes.Count > 0
            ? paymentTypes
            :
            [
                new PaymentTypeItemDto(
                    "Gider Pusulası",
                    100,
                    string.Empty,
                    string.Empty,
                    0,
                    0d)
            ];
    }

    public async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListStoreExpensePaymentTypesAsync(
        CancellationToken cancellationToken) =>
        await ListPaymentTypesByPredicateAsync(
            CashSummaryCategoryMatcher.IsStoreExpensePaymentType,
            cancellationToken);

    public async Task<IReadOnlyCollection<CashRegisterDetailDto>> ListOnlineCashRegistersAsync(
        CancellationToken cancellationToken)
    {
        var mikroRows = await mikroDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.Bank != null && item.Bank.ToLower().Contains("online"))
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .ToArrayAsync(cancellationToken);

        var furpaRows = await furpaDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.Bank != null && item.Bank.ToLower().Contains("online"))
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .ToArrayAsync(cancellationToken);

        return PreferFirstSource(mikroRows, furpaRows)
            .Select(ToCashRegisterDetailDto)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListPaymentTypesByPredicateAsync(
        Func<string?, bool> predicate,
        CancellationToken cancellationToken)
    {
        var paymentTypes = await mikroDbContext.PaymentTypes
            .AsNoTracking()
            .OrderBy(item => item.PaymentName)
            .Select(item => new
            {
                PaymentName = item.PaymentName ?? string.Empty,
                item.PaymentTypeNo,
                AccountCode = item.AccountCode ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        return paymentTypes
            .Where(item => predicate(item.PaymentName))
            .Select(item => new PaymentTypeItemDto(
                item.PaymentName,
                item.PaymentTypeNo,
                string.Empty,
                item.AccountCode ?? string.Empty,
                0,
                0d))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<PaymentTypeItemDto>> ListPaymentTypesByGenusAsync(
        int paymentGenus,
        CancellationToken cancellationToken)
    {
        var paymentTypes = await mikroDbContext.PaymentTypes
            .AsNoTracking()
            .Where(item => item.PaymentGenus == paymentGenus)
            .OrderBy(item => item.PaymentName)
            .Select(item => new
            {
                PaymentName = item.PaymentName ?? string.Empty,
                item.PaymentTypeNo,
                AccountCode = item.AccountCode ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        return paymentTypes
            .Select(item => new PaymentTypeItemDto(
                item.PaymentName,
                item.PaymentTypeNo,
                string.Empty,
                item.AccountCode ?? string.Empty,
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

    private async Task<CashRegisterTerminalSource?> GetMikroCashRegisterDetailByRegisterNoAsync(
        string cashRegisterNo,
        CancellationToken cancellationToken) =>
        await mikroDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashRegisterNo == cashRegisterNo)
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<CashRegisterTerminalSource?> GetFurpaCashRegisterDetailByRegisterNoAsync(
        string cashRegisterNo,
        CancellationToken cancellationToken) =>
        await furpaDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashRegisterNo == cashRegisterNo)
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<CashRegisterTerminalSource?> GetMikroCashRegisterDetailByCashNoAsync(
        int cashNo,
        CancellationToken cancellationToken) =>
        await mikroDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashNo == cashNo)
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<CashRegisterTerminalSource?> GetFurpaCashRegisterDetailByCashNoAsync(
        int cashNo,
        CancellationToken cancellationToken) =>
        await furpaDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashNo == cashNo)
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<CashRegisterTerminalSource[]> ListMikroCashRegisterDetailsByRegisterNoAsync(
        string cashRegisterNo,
        CancellationToken cancellationToken) =>
        await mikroDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashRegisterNo == cashRegisterNo)
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .ToArrayAsync(cancellationToken);

    private async Task<CashRegisterTerminalSource[]> ListFurpaCashRegisterDetailsByRegisterNoAsync(
        string cashRegisterNo,
        CancellationToken cancellationToken) =>
        await furpaDbContext.CashRegisterDetails
            .AsNoTracking()
            .Where(item => item.CashRegisterNo == cashRegisterNo)
            .OrderBy(item => item.Id)
            .Select(item => new CashRegisterTerminalSource(
                item.Id,
                item.CashRegisterNo ?? string.Empty,
                item.Bank ?? string.Empty,
                item.TerminalId ?? string.Empty,
                item.MerchantNo ?? string.Empty,
                item.CashNo))
            .ToArrayAsync(cancellationToken);

    private static IReadOnlyCollection<CashRegisterTerminalSource> PreferFirstSource(
        IReadOnlyCollection<CashRegisterTerminalSource> firstSource,
        IReadOnlyCollection<CashRegisterTerminalSource> fallbackSource)
    {
        var result = new List<CashRegisterTerminalSource>(firstSource.Count + fallbackSource.Count);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in firstSource.Concat(fallbackSource))
        {
            var key = string.Join(
                "|",
                item.CashRegisterNo,
                item.Bank,
                item.TerminalId,
                item.MerchantNo);

            if (seenKeys.Add(key))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static CashRegisterDetailDto ToCashRegisterDetailDto(CashRegisterTerminalSource item) =>
        new(
            item.Id,
            item.CashRegisterNo,
            item.Bank,
            item.TerminalId,
            item.MerchantNo,
            item.CashNo);

    private sealed record CashRegisterTerminalSource(
        int Id,
        string CashRegisterNo,
        string Bank,
        string TerminalId,
        string MerchantNo,
        int? CashNo);
}
