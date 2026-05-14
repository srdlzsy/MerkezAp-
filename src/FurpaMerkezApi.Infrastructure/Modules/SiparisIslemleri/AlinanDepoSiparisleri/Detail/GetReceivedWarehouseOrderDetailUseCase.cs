using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Detail;

public sealed class GetReceivedWarehouseOrderDetailUseCase(WarehouseOrderDetailQueryExecutor queryExecutor)
    : IGetReceivedWarehouseOrderDetailUseCase
{
    public Task<WarehouseOrderDetailDto> ExecuteAsync(
        WarehouseOrderDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, WarehouseOrderListDirection.Received, cancellationToken);
}
