namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BirlikKartSorgulama;

public interface IBirlikKartSorgulamaUseCase
{
    Task<BirlikKartSorgulamaResponse> SorgulaAsync(
        BirlikKartSorgulamaRequest request,
        CancellationToken cancellationToken);
}
