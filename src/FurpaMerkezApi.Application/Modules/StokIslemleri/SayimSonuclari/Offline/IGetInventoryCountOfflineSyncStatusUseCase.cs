using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Offline;

public interface IGetInventoryCountOfflineSyncStatusUseCase
{
    Task<OfflineSyncStatusDto<CreateInventoryCountResponse>> ExecuteAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
