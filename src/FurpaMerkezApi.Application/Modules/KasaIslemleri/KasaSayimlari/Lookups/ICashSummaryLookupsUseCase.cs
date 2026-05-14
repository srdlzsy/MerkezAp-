namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Lookups;

public interface ICashSummaryLookupsUseCase
{
    Task<IReadOnlyCollection<CashierItemDto>> GetCashierAndManagerAsync(
        CashierPairRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashRegistryItemDto>> GetCashRegistriesAsync(
        CashRegistryRequest request,
        CancellationToken cancellationToken);

    Task<CashRegisterDetailDto?> GetCashRegisterDetailAsync(
        CashRegisterLookupRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashierSearchItemDto>> SearchCashiersAsync(
        CashierSearchRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BanknoteTypeItemDto>> ListBanknoteTypesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GiftCheckTypeItemDto>> ListGiftCheckTypesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PaymentTypeItemDto>> ListBankPaymentTypesAsync(
        BankPaymentTypeRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PaymentTypeItemDto>> ListFoodCheckPaymentTypesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PaymentTypeItemDto>> ListOnlineSalesPaymentTypesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PaymentTypeItemDto>> ListExpenseCompassPaymentTypesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PaymentTypeItemDto>> ListStoreExpensePaymentTypesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashRegisterDetailDto>> ListOnlineCashRegistersAsync(
        CancellationToken cancellationToken);
}
