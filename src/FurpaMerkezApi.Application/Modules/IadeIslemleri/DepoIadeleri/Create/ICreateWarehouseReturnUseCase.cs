using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;

public interface ICreateWarehouseReturnUseCase
{
    Task<CreateWarehouseReturnResponse> ExecuteAsync(
        CreateWarehouseReturnRequest request,
        CancellationToken cancellationToken);

    Task<OfflineSyncStatusDto<CreateWarehouseReturnResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
