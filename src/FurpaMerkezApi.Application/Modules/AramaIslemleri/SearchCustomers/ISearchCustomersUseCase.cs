namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchCustomers;

public interface ISearchCustomersUseCase
{
    Task<IReadOnlyCollection<CustomerLookupItemDto>> ExecuteAsync(
        CustomerSearchRequest request,
        CancellationToken cancellationToken);
}
