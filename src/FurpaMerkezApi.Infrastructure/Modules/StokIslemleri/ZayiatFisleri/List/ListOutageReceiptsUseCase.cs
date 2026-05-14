using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.ZayiatFisleri.List;

public sealed class ListOutageReceiptsUseCase(StockReceiptListQueryExecutor queryExecutor)
    : IListOutageReceiptsUseCase
{
    public Task<IReadOnlyCollection<StockReceiptListItemDto>> ExecuteAsync(
        StockReceiptListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, StockReceiptKind.OutageReceipt, cancellationToken);
}
