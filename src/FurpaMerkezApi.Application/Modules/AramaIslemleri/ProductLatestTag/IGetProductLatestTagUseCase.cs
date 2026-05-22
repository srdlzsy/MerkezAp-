namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductLatestTag;

public interface IGetProductLatestTagUseCase
{
    Task<ProductLatestTagDto?> ExecuteAsync(
        ProductLatestTagRequest request,
        CancellationToken cancellationToken);
}
