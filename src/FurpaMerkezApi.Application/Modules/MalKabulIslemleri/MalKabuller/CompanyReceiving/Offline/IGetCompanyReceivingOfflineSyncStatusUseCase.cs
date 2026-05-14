using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving.Offline;

public interface IGetCompanyReceivingOfflineSyncStatusUseCase
{
    Task<OfflineSyncStatusDto<CreateCompanyReceivingResponse>> ExecuteAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
