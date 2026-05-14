namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Create;

public interface ICreateInventoryCountUseCase
{
    Task<CreateInventoryCountResponse> ExecuteAsync(
        CreateInventoryCountRequest request,
        CancellationToken cancellationToken);
}
