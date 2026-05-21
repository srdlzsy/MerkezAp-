using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.List;

public interface IListBanknoteTracksUseCase
{
    Task<IReadOnlyCollection<BanknoteTrackDto>> ExecuteAsync(
        BanknoteTrackListRequest request,
        CancellationToken cancellationToken);
}
