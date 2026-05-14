namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;

public interface ICreateWarehouseReturnUseCase
{
    Task<CreateWarehouseReturnResponse> ExecuteAsync(
        CreateWarehouseReturnRequest request,
        CancellationToken cancellationToken);
}
