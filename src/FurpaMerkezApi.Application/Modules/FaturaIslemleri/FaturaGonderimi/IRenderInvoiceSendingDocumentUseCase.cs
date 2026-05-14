namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IRenderInvoiceSendingDocumentUseCase
{
    Task<InvoiceSendingDetailDto> ExecuteAsync(
        InvoiceSendingRenderRequest request,
        CancellationToken cancellationToken);
}
