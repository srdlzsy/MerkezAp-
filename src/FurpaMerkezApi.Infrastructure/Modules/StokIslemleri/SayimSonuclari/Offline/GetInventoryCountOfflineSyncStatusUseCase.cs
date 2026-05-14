using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Offline;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.Offline;

public sealed class GetInventoryCountOfflineSyncStatusUseCase(InventoryCountWriteService inventoryCountWriteService)
    : IGetInventoryCountOfflineSyncStatusUseCase
{
    public Task<OfflineSyncStatusDto<CreateInventoryCountResponse>> ExecuteAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken) =>
        inventoryCountWriteService.GetOfflineSyncStatusAsync(
            warehouseNo,
            requestedByUserId,
            clientRequestId,
            cancellationToken);
}
