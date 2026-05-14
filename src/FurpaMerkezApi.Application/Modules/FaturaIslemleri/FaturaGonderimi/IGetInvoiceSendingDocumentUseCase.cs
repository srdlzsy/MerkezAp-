namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IGetInvoiceSendingDocumentUseCase
{
    Task<InvoiceSendingDetailDto> ExecuteAsync(
        InvoiceSendingDocumentRequest request,
        CancellationToken cancellationToken);
}
