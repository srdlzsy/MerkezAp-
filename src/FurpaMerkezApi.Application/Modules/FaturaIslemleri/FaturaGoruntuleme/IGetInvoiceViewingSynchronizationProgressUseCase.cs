namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

public interface IGetInvoiceViewingSynchronizationProgressUseCase
{
    Task<InvoiceViewingSynchronizationProgressResponse> ExecuteAsync(
        CancellationToken cancellationToken);
}
