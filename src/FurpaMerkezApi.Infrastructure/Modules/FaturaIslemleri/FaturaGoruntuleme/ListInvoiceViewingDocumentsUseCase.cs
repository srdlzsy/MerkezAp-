using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class ListInvoiceViewingDocumentsUseCase(InvoiceViewingService invoiceViewingService)
    : IListInvoiceViewingDocumentsUseCase
{
    public Task<InvoiceViewingListResponse> ExecuteAsync(
        InvoiceViewingListRequest request,
        CancellationToken cancellationToken) =>
        invoiceViewingService.ListAsync(request, cancellationToken);
}
