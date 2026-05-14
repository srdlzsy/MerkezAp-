using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.Detail;

public interface IGetExpenseReceiptDetailUseCase
{
    Task<StockReceiptDetailDto> ExecuteAsync(
        StockReceiptDetailRequest request,
        CancellationToken cancellationToken);
}
