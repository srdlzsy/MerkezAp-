namespace FurpaMerkezApi.Application.Modules.MobileSync.ProductPriceCatalog;

public interface IGetMobileProductPriceCatalogUseCase
{
    Task<MobileProductPriceCatalogResponse> ExecuteAsync(
        MobileProductPriceCatalogRequest request,
        CancellationToken cancellationToken);
}
