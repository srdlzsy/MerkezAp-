namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

public interface ICreateIssuedWarehouseOrderUseCase
{
    Task<CreateIssuedWarehouseOrderResponse> ExecuteAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken);
}
