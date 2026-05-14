using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;

public interface IGetPendingWarehouseReceivingDetailUseCase
{
    Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        CancellationToken cancellationToken);
}
