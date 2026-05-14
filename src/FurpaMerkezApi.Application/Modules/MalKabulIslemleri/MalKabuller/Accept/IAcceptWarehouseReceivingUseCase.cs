namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;

public interface IAcceptWarehouseReceivingUseCase
{
    Task<AcceptWarehouseReceivingResponse> ExecuteAsync(
        AcceptWarehouseReceivingRequest request,
        CancellationToken cancellationToken);
}
