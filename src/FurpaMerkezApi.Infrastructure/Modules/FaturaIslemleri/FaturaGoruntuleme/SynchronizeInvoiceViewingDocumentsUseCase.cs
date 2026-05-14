using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class SynchronizeInvoiceViewingDocumentsUseCase(InvoiceViewingService invoiceViewingService)
    : ISynchronizeInvoiceViewingDocumentsUseCase
{
    public Task ExecuteAsync(
        InvoiceViewingSynchronizationRequest request,
        CancellationToken cancellationToken) =>
        invoiceViewingService.SynchronizeAsync(request, cancellationToken);
}
