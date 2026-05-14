using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Detail;

public sealed class GetInterWarehouseShipmentDetailUseCase(WarehouseShippingDetailQueryExecutor queryExecutor)
    : IGetInterWarehouseShipmentDetailUseCase
{
    public Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, direction, false, cancellationToken);
}
