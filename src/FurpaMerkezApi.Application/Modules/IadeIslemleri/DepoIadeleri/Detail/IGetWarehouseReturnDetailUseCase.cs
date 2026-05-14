using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Detail;

public interface IGetWarehouseReturnDetailUseCase
{
    Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken);
}
