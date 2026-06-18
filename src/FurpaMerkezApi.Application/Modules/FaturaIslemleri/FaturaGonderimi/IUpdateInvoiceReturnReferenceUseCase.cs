namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IUpdateInvoiceReturnReferenceUseCase
{
    Task<UpdateInvoiceReturnReferenceResponse> ExecuteAsync(
        UpdateInvoiceReturnReferenceRequest request,
        CancellationToken cancellationToken);
}
