using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri.Create;

public sealed class CreateBanknoteTrackUseCase(BanknoteTrackWriteService banknoteTrackWriteService)
    : ICreateBanknoteTrackUseCase
{
    public Task<CreateBanknoteTrackResponse> ExecuteAsync(
        CreateBanknoteTrackRequest request,
        CancellationToken cancellationToken) =>
        banknoteTrackWriteService.CreateAsync(request, cancellationToken);
}
