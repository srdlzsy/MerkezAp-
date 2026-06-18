using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class ListInvoiceReturnReferenceCandidatesUseCase(InvoiceSendingService invoiceSendingService)
    : IListInvoiceReturnReferenceCandidatesUseCase
{
    public Task<InvoiceReturnReferenceCandidatesResponse> ExecuteAsync(
        InvoiceReturnReferenceCandidatesRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.ListReturnReferenceCandidatesAsync(request, cancellationToken);
}
