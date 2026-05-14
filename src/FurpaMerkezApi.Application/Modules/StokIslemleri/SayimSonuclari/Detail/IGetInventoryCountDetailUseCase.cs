namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Detail;

public interface IGetInventoryCountDetailUseCase
{
    Task<InventoryCountDetailDto> ExecuteAsync(
        InventoryCountDetailRequest request,
        CancellationToken cancellationToken);
}
