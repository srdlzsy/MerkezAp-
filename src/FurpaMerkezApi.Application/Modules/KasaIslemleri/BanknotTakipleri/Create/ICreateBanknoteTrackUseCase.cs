using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Create;

public interface ICreateBanknoteTrackUseCase
{
    Task<CreateBanknoteTrackResponse> ExecuteAsync(
        CreateBanknoteTrackRequest request,
        CancellationToken cancellationToken);
}
