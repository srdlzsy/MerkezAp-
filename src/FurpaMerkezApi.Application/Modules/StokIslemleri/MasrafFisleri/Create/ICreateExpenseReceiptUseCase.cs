using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.Create;

public interface ICreateExpenseReceiptUseCase
{
    Task<CreateStockReceiptResponse> ExecuteAsync(
        CreateStockReceiptRequest request,
        CancellationToken cancellationToken);
}
