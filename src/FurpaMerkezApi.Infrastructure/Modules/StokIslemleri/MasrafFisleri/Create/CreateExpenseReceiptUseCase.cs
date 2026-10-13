using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
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

    public Task<OfflineSyncStatusDto<CreateStockReceiptResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken) =>
        stockReceiptWriteService.GetOfflineSyncStatusAsync(
            StockReceiptKind.ExpenseReceipt,
            requestedByUserId,
            clientRequestId,
            cancellationToken);
}
