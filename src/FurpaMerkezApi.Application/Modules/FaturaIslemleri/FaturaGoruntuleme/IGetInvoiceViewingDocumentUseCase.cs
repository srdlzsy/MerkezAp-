namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public interface IGetInvoiceViewingDocumentUseCase
{
    Task<InvoiceViewingDetailDto> ExecuteAsync(
        InvoiceViewingDetailRequest request,
        CancellationToken cancellationToken);
}
