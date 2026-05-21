using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Detail;

public interface IGetBanknoteTrackDetailUseCase
{
    Task<BanknoteTrackDto> ExecuteAsync(
        BanknoteTrackDetailRequest request,
        CancellationToken cancellationToken);
}
