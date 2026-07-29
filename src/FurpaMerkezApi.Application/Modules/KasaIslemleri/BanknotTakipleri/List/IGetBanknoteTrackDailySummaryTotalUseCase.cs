namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.List;

public interface IGetBanknoteTrackDailySummaryTotalUseCase
{
    Task<BanknoteTrackDailySummaryTotalDto> ExecuteAsync(
        BanknoteTrackDailySummaryTotalRequest request,
        CancellationToken cancellationToken);
}
