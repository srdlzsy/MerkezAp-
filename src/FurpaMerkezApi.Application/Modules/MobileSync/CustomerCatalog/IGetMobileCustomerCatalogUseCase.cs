namespace FurpaMerkezApi.Application.Modules.MobileSync.CustomerCatalog;

public interface IGetMobileCustomerCatalogUseCase
{
    Task<MobileCustomerCatalogResponse> ExecuteAsync(
        MobileCustomerCatalogRequest request,
        CancellationToken cancellationToken);
}
