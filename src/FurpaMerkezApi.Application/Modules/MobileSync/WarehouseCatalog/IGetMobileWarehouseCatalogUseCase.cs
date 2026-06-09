namespace FurpaMerkezApi.Application.Modules.MobileSync.WarehouseCatalog;

public interface IGetMobileWarehouseCatalogUseCase
{
    Task<MobileWarehouseCatalogResponse> ExecuteAsync(
        MobileWarehouseCatalogRequest request,
        CancellationToken cancellationToken);
}
