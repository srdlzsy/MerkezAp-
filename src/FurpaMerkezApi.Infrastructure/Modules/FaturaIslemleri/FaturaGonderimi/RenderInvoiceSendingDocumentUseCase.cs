using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class RenderInvoiceSendingDocumentUseCase(InvoiceSendingService invoiceSendingService)
    : IRenderInvoiceSendingDocumentUseCase
{
    public Task<InvoiceSendingDetailDto> ExecuteAsync(
        InvoiceSendingRenderRequest request,
        CancellationToken cancellationToken) =>
        invoiceSendingService.RenderAsync(
            request,
            cancellationToken);
}
