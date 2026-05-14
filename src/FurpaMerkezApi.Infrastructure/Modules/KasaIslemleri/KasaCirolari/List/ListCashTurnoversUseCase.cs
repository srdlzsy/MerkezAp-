using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.List;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari.List;

public sealed class ListCashTurnoversUseCase(CashTurnoverQueryExecutor queryExecutor)
    : IListCashTurnoversUseCase
{
    public Task<IReadOnlyCollection<CashTurnoverListItemDto>> ExecuteAsync(
        CashTurnoverListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ListAsync(request, cancellationToken);
}
