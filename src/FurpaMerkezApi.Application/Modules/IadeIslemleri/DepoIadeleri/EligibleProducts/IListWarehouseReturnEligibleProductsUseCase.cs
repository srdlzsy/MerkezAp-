namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.EligibleProducts;

public interface IListWarehouseReturnEligibleProductsUseCase
{
    Task<WarehouseReturnEligibleProductsDto> ExecuteAsync(
        WarehouseReturnEligibleProductsRequest request,
        CancellationToken cancellationToken);
}
