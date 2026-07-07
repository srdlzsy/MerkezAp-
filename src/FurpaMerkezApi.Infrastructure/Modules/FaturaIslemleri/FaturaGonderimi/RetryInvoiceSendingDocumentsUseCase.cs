using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class RetryInvoiceSendingDocumentsUseCase(InvoiceSendingService invoiceSendingService)
    : IRetryInvoiceSendingDocumentsUseCase
{
    public Task<RetryInvoiceDocumentsResponse> ExecuteAsync(
        RetryInvoiceDocumentsRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.RetryAsync(request, cancellationToken);
}
