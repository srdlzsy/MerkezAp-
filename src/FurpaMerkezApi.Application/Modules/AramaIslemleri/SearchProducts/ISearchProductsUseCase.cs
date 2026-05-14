namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;

public interface ISearchProductsUseCase
{
    Task<IReadOnlyCollection<ProductLookupItemDto>> ExecuteAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken);
}
