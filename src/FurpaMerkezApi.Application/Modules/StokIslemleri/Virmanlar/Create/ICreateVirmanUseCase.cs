using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Create;

public interface ICreateVirmanUseCase
{
    Task<CreateVirmanResponse> ExecuteAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken);

    Task<OfflineSyncStatusDto<CreateVirmanResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
