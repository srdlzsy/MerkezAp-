using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;

namespace FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Create;

public interface ICreateOutageReceiptUseCase
{
    Task<CreateStockReceiptResponse> ExecuteAsync(
        CreateStockReceiptRequest request,
        CancellationToken cancellationToken);
}
