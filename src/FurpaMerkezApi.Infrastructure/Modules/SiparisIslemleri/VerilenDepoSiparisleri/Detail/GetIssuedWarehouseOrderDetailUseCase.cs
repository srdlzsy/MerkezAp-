using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Detail;

public sealed class GetIssuedWarehouseOrderDetailUseCase(WarehouseOrderDetailQueryExecutor queryExecutor)
    : IGetIssuedWarehouseOrderDetailUseCase
{
    public Task<WarehouseOrderDetailDto> ExecuteAsync(
        WarehouseOrderDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, WarehouseOrderListDirection.Issued, cancellationToken);
}
