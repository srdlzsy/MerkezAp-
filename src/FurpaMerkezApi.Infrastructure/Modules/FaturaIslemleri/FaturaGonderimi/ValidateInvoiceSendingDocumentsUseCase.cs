using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class ValidateInvoiceSendingDocumentsUseCase(InvoiceSendingService invoiceSendingService)
    : IValidateInvoiceSendingDocumentsUseCase
{
    public Task<ValidateInvoiceDocumentsResponse> ExecuteAsync(
        ValidateInvoiceDocumentsRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.ValidateAsync(request, cancellationToken);
}
