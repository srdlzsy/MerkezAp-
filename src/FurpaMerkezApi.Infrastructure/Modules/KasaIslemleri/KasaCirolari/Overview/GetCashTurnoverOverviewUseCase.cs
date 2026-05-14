using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Overview;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari.Overview;

public sealed class GetCashTurnoverOverviewUseCase(CashTurnoverQueryExecutor queryExecutor)
    : IGetCashTurnoverOverviewUseCase
{
    public Task<CashTurnoverOverviewDto> ExecuteAsync(
        CashTurnoverOverviewRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.GetOverviewAsync(request, cancellationToken);
}
