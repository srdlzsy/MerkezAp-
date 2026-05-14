using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.List;

public interface IListExpenseReceiptsUseCase
{
    Task<IReadOnlyCollection<StockReceiptListItemDto>> ExecuteAsync(
        StockReceiptListRequest request,
        CancellationToken cancellationToken);
}
