using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Detail;

public interface IGetOutageReceiptDetailUseCase
{
    Task<StockReceiptDetailDto> ExecuteAsync(
        StockReceiptDetailRequest request,
        CancellationToken cancellationToken);
}
