namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.YeniKasaAnalizleri;

public interface IYeniKasaAnalizleriService
{
    Task<IReadOnlyCollection<YeniKasaCiroOzetItemDto>> GetCiroOzetiAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<YeniKasaKasaOzetItemDto>> GetKasaOzetiAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<YeniKasaFisMutabakatItemDto>> GetFisMutabakatiAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<YeniKasaAnomalyItemDto>> GetAnomalilerAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<YeniKasaPaymentMethodItemDto>> GetOdemeTipleriAsync(
        YeniKasaAnalizRequest request,
        CancellationToken cancellationToken);
}
