using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.MasrafFisleri.Create;

public sealed class CreateExpenseReceiptUseCase(StockReceiptWriteService stockReceiptWriteService)
    : ICreateExpenseReceiptUseCase
{
    public Task<CreateStockReceiptResponse> ExecuteAsync(
        CreateStockReceiptRequest request,
        CancellationToken cancellationToken) =>
        stockReceiptWriteService.ExecuteAsync(request, StockReceiptKind.ExpenseReceipt, cancellationToken);
}
