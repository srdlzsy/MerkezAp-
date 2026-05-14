using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving.Offline;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving.Offline;

public sealed class GetCompanyReceivingOfflineSyncStatusUseCase(CreateCompanyReceivingUseCase createCompanyReceivingUseCase)
    : IGetCompanyReceivingOfflineSyncStatusUseCase
{
    public Task<OfflineSyncStatusDto<CreateCompanyReceivingResponse>> ExecuteAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken) =>
        createCompanyReceivingUseCase.GetOfflineSyncStatusAsync(
            warehouseNo,
            requestedByUserId,
            clientRequestId,
            cancellationToken);
}
