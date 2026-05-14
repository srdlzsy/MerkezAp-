using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.ZayiatFisleri.Create;

public sealed class CreateOutageReceiptUseCase(StockReceiptWriteService stockReceiptWriteService)
    : ICreateOutageReceiptUseCase
{
    public Task<CreateStockReceiptResponse> ExecuteAsync(
        CreateStockReceiptRequest request,
        CancellationToken cancellationToken) =>
        stockReceiptWriteService.ExecuteAsync(request, StockReceiptKind.OutageReceipt, cancellationToken);
}
