using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class UpdateInvoiceReturnReferenceUseCase(InvoiceSendingService invoiceSendingService)
    : IUpdateInvoiceReturnReferenceUseCase
{
    public Task<UpdateInvoiceReturnReferenceResponse> ExecuteAsync(
        UpdateInvoiceReturnReferenceRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.UpdateReturnReferenceAsync(request, cancellationToken);
}
