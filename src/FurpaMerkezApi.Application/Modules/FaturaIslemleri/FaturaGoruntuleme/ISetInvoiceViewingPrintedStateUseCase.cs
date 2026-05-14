namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public interface ISetInvoiceViewingPrintedStateUseCase
{
    Task<InvoiceViewingPrintedStateResponse> ExecuteAsync(
        InvoiceViewingPrintedStateRequest request,
        CancellationToken cancellationToken);
}
