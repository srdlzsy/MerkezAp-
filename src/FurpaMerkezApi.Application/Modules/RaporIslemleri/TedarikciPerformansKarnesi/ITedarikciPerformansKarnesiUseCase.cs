namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.TedarikciPerformansKarnesi;

public interface ITedarikciPerformansKarnesiUseCase
{
    Task<SupplierPerformanceReportDto> GetReportAsync(
        SupplierPerformanceRequest request,
        CancellationToken cancellationToken);

    Task<SupplierPerformanceDetailDto> GetDetailAsync(
        SupplierPerformanceDetailRequest request,
        CancellationToken cancellationToken);
}
