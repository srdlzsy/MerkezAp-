namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;

public interface IValidateInvoiceSendingDocumentsUseCase
{
    Task<ValidateInvoiceDocumentsResponse> ExecuteAsync(
        ValidateInvoiceDocumentsRequest request,
        CancellationToken cancellationToken);
}
