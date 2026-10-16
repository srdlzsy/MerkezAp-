namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

public interface ICreateIssuedWarehouseOrderUseCase
{
    Task<CreateIssuedWarehouseOrderResponse> ExecuteAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken);

    Task<OfflineSyncStatusDto<CreateIssuedWarehouseOrderResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
