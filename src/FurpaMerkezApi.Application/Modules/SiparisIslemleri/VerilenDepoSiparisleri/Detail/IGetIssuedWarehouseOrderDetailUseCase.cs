using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Detail;

public interface IGetIssuedWarehouseOrderDetailUseCase
{
    Task<WarehouseOrderDetailDto> ExecuteAsync(
        WarehouseOrderDetailRequest request,
        CancellationToken cancellationToken);
}
