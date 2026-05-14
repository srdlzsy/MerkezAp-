using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class GetInvoiceViewingDocumentUseCase(InvoiceViewingService invoiceViewingService)
    : IGetInvoiceViewingDocumentUseCase
{
    public Task<InvoiceViewingDetailDto> ExecuteAsync(
        InvoiceViewingDetailRequest request,
        CancellationToken cancellationToken) =>
        invoiceViewingService.GetAsync(request, cancellationToken);
}
