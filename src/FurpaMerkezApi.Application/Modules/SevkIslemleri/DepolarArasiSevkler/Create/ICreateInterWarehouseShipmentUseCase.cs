namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;

public interface ICreateInterWarehouseShipmentUseCase
{
    Task<CreateInterWarehouseShipmentResponse> ExecuteAsync(
        CreateInterWarehouseShipmentRequest request,
        CancellationToken cancellationToken);
}
