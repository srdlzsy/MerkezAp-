using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.List;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri.List;

public sealed class ListBanknoteTracksUseCase(BanknoteTrackQueryExecutor banknoteTrackQueryExecutor)
    : IListBanknoteTracksUseCase
{
    public Task<IReadOnlyCollection<BanknoteTrackDto>> ExecuteAsync(
        BanknoteTrackListRequest request,
        CancellationToken cancellationToken) =>
        banknoteTrackQueryExecutor.ListAsync(request, cancellationToken);
}
