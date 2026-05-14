namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Files;

public interface IGetCashSummaryZReportTotalUseCase
{
    Task<double> ExecuteAsync(
        ZReportValueRequest request,
        CancellationToken cancellationToken);
}
