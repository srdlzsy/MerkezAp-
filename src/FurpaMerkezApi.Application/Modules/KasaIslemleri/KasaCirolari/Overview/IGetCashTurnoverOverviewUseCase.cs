namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Overview;

public interface IGetCashTurnoverOverviewUseCase
{
    Task<CashTurnoverOverviewDto> ExecuteAsync(
        CashTurnoverOverviewRequest request,
        CancellationToken cancellationToken);
}
