using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.List;

public interface IListOutageReceiptsUseCase
{
    Task<IReadOnlyCollection<StockReceiptListItemDto>> ExecuteAsync(
        StockReceiptListRequest request,
        CancellationToken cancellationToken);
}
