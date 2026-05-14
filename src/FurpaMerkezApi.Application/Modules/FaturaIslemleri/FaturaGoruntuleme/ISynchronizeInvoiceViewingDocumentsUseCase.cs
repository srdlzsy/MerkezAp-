namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public interface ISynchronizeInvoiceViewingDocumentsUseCase
{
    Task ExecuteAsync(
        InvoiceViewingSynchronizationRequest request,
        CancellationToken cancellationToken);
}
