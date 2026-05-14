using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Detail;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari.Detail;

public sealed class GetCashTurnoverDetailUseCase(CashTurnoverQueryExecutor queryExecutor)
    : IGetCashTurnoverDetailUseCase
{
    public Task<CashTurnoverDetailDto> ExecuteAsync(
        CashTurnoverDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.GetDetailAsync(request, cancellationToken);
}
