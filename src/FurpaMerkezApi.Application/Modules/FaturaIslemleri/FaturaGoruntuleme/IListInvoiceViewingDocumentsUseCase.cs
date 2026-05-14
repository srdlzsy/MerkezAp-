namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public interface IListInvoiceViewingDocumentsUseCase
{
    Task<InvoiceViewingListResponse> ExecuteAsync(
        InvoiceViewingListRequest request,
        CancellationToken cancellationToken);
}
