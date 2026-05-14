using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Detail;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Detail;

public sealed class GetWarehouseReturnDetailUseCase(WarehouseShippingDetailQueryExecutor queryExecutor)
    : IGetWarehouseReturnDetailUseCase
{
    public Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(
            request,
            direction,
            true,
            cancellationToken);
}
