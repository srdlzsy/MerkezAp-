using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Detail;

public interface IGetInterWarehouseShipmentDetailUseCase
{
    Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        WarehouseShippingDirection direction,
        CancellationToken cancellationToken);
}
