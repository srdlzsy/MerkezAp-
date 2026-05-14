namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IListInvoiceSendingDocumentsUseCase
{
    Task<InvoiceSendingListResponse> ExecuteAsync(
        InvoiceSendingListRequest request,
        CancellationToken cancellationToken);
}
