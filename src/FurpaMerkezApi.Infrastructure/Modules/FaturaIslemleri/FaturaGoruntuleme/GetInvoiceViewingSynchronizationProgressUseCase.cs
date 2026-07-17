using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class GetInvoiceViewingSynchronizationProgressUseCase(InvoiceViewingService invoiceViewingService)
    : IGetInvoiceViewingSynchronizationProgressUseCase
{
    public Task<InvoiceViewingSynchronizationProgressResponse> ExecuteAsync(
        CancellationToken cancellationToken) =>
        Task.FromResult(invoiceViewingService.GetSynchronizationProgress());
}
