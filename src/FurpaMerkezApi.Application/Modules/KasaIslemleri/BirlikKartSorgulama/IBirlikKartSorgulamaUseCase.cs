namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BirlikKartSorgulama;

public interface IBirlikKartSorgulamaUseCase
{
    Task<BirlikKartSorgulamaResponse> SorgulaAsync(
        BirlikKartSorgulamaRequest request,
        CancellationToken cancellationToken);

    Task<BirlikKartDetayResponse> DetayAsync(
        BirlikKartDetayRequest request,
        CancellationToken cancellationToken);

    Task<BirlikKartGuncelleResponse> GuncelleAsync(
        BirlikKartSorgulamaGuncelleRequest request,
        CancellationToken cancellationToken);
}
