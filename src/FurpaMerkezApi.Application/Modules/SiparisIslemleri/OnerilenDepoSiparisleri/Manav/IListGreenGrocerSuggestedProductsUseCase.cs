namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.Manav;

public interface IListGreenGrocerSuggestedProductsUseCase
{
    Task<IReadOnlyCollection<GreenGrocerSuggestedProductDto>> ExecuteAsync(
        CancellationToken cancellationToken);
}
