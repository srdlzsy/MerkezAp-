using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;

public interface ICreateInterWarehouseShipmentUseCase
{
    Task<CreateInterWarehouseShipmentResponse> ExecuteAsync(
        CreateInterWarehouseShipmentRequest request,
        CancellationToken cancellationToken);

    Task<OfflineSyncStatusDto<CreateInterWarehouseShipmentResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
