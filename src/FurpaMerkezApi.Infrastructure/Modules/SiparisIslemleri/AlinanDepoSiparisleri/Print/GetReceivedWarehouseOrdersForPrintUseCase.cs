using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Print;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Print;

public sealed class GetReceivedWarehouseOrdersForPrintUseCase(
    ReceivedWarehouseOrderBulkPrintQueryExecutor queryExecutor)
    : IGetReceivedWarehouseOrdersForPrintUseCase
{
    public Task<IReadOnlyCollection<WarehouseOrderDetailDto>> ExecuteAsync(
        IReadOnlyCollection<WarehouseOrderDetailRequest> requests,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(requests, cancellationToken);
}
