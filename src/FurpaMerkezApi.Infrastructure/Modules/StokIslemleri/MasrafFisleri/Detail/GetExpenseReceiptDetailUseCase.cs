using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.MasrafFisleri.Detail;

public sealed class GetExpenseReceiptDetailUseCase(StockReceiptDetailQueryExecutor queryExecutor)
    : IGetExpenseReceiptDetailUseCase
{
    public Task<StockReceiptDetailDto> ExecuteAsync(
        StockReceiptDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, StockReceiptKind.ExpenseReceipt, cancellationToken);
}
