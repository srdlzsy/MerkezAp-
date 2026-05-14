using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class GetInvoiceSendingDocumentUseCase(
    IRenderInvoiceSendingDocumentUseCase renderInvoiceSendingDocumentUseCase)
    : IGetInvoiceSendingDocumentUseCase
{
    public Task<InvoiceSendingDetailDto> ExecuteAsync(
        InvoiceSendingDocumentRequest request,
        CancellationToken cancellationToken) =>
        renderInvoiceSendingDocumentUseCase.ExecuteAsync(
            new InvoiceSendingRenderRequest(
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.Scenario,
                InvoiceDocumentProfile.Auto,
                null,
                true),
            cancellationToken);
}
