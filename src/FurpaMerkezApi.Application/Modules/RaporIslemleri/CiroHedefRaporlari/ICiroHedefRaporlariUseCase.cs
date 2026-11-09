namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.CiroHedefRaporlari;

public interface ICiroHedefRaporlariUseCase
{
    Task<CiroHedefKarsilastirmaRaporDto> GetComparisonReportAsync(
        CiroHedefKarsilastirmaRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<HedefTanimDto>> GetGoalsAsync(
        int? year,
        CancellationToken cancellationToken);
}
