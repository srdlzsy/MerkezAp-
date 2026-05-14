namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public interface IRenderInvoiceViewingDocumentUseCase
{
    Task<InvoiceViewingDetailDto> ExecuteAsync(
        InvoiceViewingRenderRequest request,
        CancellationToken cancellationToken);
}
