using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class ListInvoiceSendingDocumentsUseCase(InvoiceSendingService invoiceSendingService)
    : IListInvoiceSendingDocumentsUseCase
{
    public Task<InvoiceSendingListResponse> ExecuteAsync(
        InvoiceSendingListRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.ListAsync(request, cancellationToken);
}
