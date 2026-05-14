using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;

public sealed class GetPendingWarehouseReceivingDetailUseCase(WarehouseShippingDetailQueryExecutor queryExecutor)
    : IGetPendingWarehouseReceivingDetailUseCase
{
    public Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecutePendingIncomingAsync(request, cancellationToken);
}
