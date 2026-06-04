namespace FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;

public interface IDeleteGreenGrocerOrderUseCase
{
    Task<DeleteGreenGrocerOrderResponse> ExecuteAsync(
        DeleteGreenGrocerOrderRequest request,
        CancellationToken cancellationToken);
}
