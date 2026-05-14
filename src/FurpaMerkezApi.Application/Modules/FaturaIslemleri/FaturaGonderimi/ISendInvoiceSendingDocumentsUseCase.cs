namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface ISendInvoiceSendingDocumentsUseCase
{
    Task<SendInvoiceDocumentsResponse> ExecuteAsync(
        SendInvoiceDocumentsRequest request,
        CancellationToken cancellationToken);
}
