namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductAvailability;

public interface IGetProductAvailabilityUseCase
{
    Task<IReadOnlyCollection<ProductAvailabilityItemDto>> ExecuteAsync(
        ProductAvailabilityRequest request,
        CancellationToken cancellationToken);
}
