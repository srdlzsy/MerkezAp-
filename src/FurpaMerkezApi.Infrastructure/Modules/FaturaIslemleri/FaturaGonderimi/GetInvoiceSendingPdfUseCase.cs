using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class GetInvoiceSendingPdfUseCase(InvoiceSendingService invoiceSendingService)
    : IGetInvoiceSendingPdfUseCase
{
    public Task<InvoiceSendingPdfResult> ExecuteAsync(
        InvoiceSendingDocumentRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.GetOutboxPdfAsync(request, cancellationToken);
}
