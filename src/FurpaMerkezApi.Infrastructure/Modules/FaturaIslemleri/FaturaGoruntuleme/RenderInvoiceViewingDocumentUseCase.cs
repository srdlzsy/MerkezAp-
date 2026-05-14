using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class RenderInvoiceViewingDocumentUseCase(InvoiceViewingService invoiceViewingService)
    : IRenderInvoiceViewingDocumentUseCase
{
    public Task<InvoiceViewingDetailDto> ExecuteAsync(
        InvoiceViewingRenderRequest request,
        CancellationToken cancellationToken) =>
        invoiceViewingService.RenderAsync(request, cancellationToken);
}
