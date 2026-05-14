namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Detail;

public interface IGetCashTurnoverDetailUseCase
{
    Task<CashTurnoverDetailDto> ExecuteAsync(
        CashTurnoverDetailRequest request,
        CancellationToken cancellationToken);
}
