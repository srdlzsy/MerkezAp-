using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.MasrafFisleri.List;

public sealed class ListExpenseReceiptsUseCase(StockReceiptListQueryExecutor queryExecutor)
    : IListExpenseReceiptsUseCase
{
    public Task<IReadOnlyCollection<StockReceiptListItemDto>> ExecuteAsync(
        StockReceiptListRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, StockReceiptKind.ExpenseReceipt, cancellationToken);
}
