namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IListInvoiceReturnReferenceCandidatesUseCase
{
    Task<InvoiceReturnReferenceCandidatesResponse> ExecuteAsync(
        InvoiceReturnReferenceCandidatesRequest request,
        CancellationToken cancellationToken);
}
