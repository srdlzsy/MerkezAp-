namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;

public interface ISearchWarehousesUseCase
{
    Task<IReadOnlyCollection<WarehouseLookupItemDto>> ExecuteAsync(
        WarehouseSearchRequest request,
        CancellationToken cancellationToken);
}
