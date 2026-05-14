using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.ZayiatFisleri.Detail;

public sealed class GetOutageReceiptDetailUseCase(StockReceiptDetailQueryExecutor queryExecutor)
    : IGetOutageReceiptDetailUseCase
{
    public Task<StockReceiptDetailDto> ExecuteAsync(
        StockReceiptDetailRequest request,
        CancellationToken cancellationToken) =>
        queryExecutor.ExecuteAsync(request, StockReceiptKind.OutageReceipt, cancellationToken);
}
