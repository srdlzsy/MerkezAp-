namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IRetryInvoiceSendingDocumentsUseCase
{
    Task<RetryInvoiceDocumentsResponse> ExecuteAsync(
        RetryInvoiceDocumentsRequest request,
        CancellationToken cancellationToken);
}
