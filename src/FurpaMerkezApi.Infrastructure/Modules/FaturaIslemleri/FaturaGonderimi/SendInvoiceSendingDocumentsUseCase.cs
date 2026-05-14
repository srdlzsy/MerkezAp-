using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class SendInvoiceSendingDocumentsUseCase(InvoiceSendingService invoiceSendingService)
    : ISendInvoiceSendingDocumentsUseCase
{
    public Task<SendInvoiceDocumentsResponse> ExecuteAsync(
        SendInvoiceDocumentsRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.SendAsync(request, cancellationToken);
}
