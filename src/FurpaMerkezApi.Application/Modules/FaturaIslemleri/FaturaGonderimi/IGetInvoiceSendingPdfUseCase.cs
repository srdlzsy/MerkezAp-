namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IGetInvoiceSendingPdfUseCase
{
    Task<InvoiceSendingPdfResult> ExecuteAsync(
        InvoiceSendingDocumentRequest request,
        CancellationToken cancellationToken);
}
