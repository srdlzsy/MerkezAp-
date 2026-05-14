namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;

public interface IResolveBarcodeUseCase
{
    Task<BarcodeResolutionDto> ExecuteAsync(
        BarcodeResolutionRequest request,
        CancellationToken cancellationToken);
}
