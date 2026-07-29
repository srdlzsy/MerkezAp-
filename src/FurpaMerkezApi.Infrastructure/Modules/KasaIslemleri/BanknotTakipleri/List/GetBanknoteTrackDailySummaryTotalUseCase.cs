using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.List;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri.List;

public sealed class GetBanknoteTrackDailySummaryTotalUseCase(BanknoteTrackQueryExecutor banknoteTrackQueryExecutor)
    : IGetBanknoteTrackDailySummaryTotalUseCase
{
    public Task<BanknoteTrackDailySummaryTotalDto> ExecuteAsync(
        BanknoteTrackDailySummaryTotalRequest request,
        CancellationToken cancellationToken) =>
        banknoteTrackQueryExecutor.GetDailySummaryTotalAsync(request, cancellationToken);
}
