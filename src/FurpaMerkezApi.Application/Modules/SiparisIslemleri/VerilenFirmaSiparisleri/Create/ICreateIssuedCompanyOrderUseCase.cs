namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

using FurpaMerkezApi.Application.Modules.Common.OfflineSync;

public interface ICreateIssuedCompanyOrderUseCase
{
    Task<CreateIssuedCompanyOrderResponse> ExecuteAsync(
        CreateIssuedCompanyOrderRequest request,
        CancellationToken cancellationToken);

    Task<OfflineSyncStatusDto<CreateIssuedCompanyOrderResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken);
}
