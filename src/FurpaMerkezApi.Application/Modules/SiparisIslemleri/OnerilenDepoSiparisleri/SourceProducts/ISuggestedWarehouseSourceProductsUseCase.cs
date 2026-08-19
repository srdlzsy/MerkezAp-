namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.SourceProducts;

public interface ISuggestedWarehouseSourceProductsUseCase
{
    Task<IReadOnlyCollection<SuggestedWarehouseSourceProductDto>> ExecuteAsync(
        int sourceWarehouseNo,
        CancellationToken cancellationToken);
}
