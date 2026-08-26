namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;

public interface ISearchSourceWarehousesUseCase
{
    Task<IReadOnlyCollection<SourceWarehouseLookupItemDto>> ExecuteAsync(
        SourceWarehouseSearchRequest request,
        CancellationToken cancellationToken);
}
