namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.List;

public interface IListCashTurnoversUseCase
{
    Task<IReadOnlyCollection<CashTurnoverListItemDto>> ExecuteAsync(
        CashTurnoverListRequest request,
        CancellationToken cancellationToken);
}
