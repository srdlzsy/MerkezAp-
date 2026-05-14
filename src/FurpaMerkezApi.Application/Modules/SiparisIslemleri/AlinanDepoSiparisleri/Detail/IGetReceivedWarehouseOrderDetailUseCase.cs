using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Detail;

public interface IGetReceivedWarehouseOrderDetailUseCase
{
    Task<WarehouseOrderDetailDto> ExecuteAsync(
        WarehouseOrderDetailRequest request,
        CancellationToken cancellationToken);
}
