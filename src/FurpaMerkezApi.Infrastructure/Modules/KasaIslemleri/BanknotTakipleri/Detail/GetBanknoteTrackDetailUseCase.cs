using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Detail;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri.Detail;

public sealed class GetBanknoteTrackDetailUseCase(BanknoteTrackQueryExecutor banknoteTrackQueryExecutor)
    : IGetBanknoteTrackDetailUseCase
{
    public async Task<BanknoteTrackDto> ExecuteAsync(
        BanknoteTrackDetailRequest request,
        CancellationToken cancellationToken)
    {
        var response = await banknoteTrackQueryExecutor.GetAsync(request, cancellationToken);

        return response ?? throw new KeyNotFoundException("Banknote track was not found.");
    }
}
