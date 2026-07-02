namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public interface ISynchronizeInvoiceViewingDocumentsUseCase
{
    Task<InvoiceViewingSynchronizationResponse> ExecuteAsync(
        InvoiceViewingSynchronizationRequest request,
        CancellationToken cancellationToken);
}
