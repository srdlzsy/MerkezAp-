using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class SetInvoiceViewingPrintedStateUseCase(InvoiceViewingService invoiceViewingService)
    : ISetInvoiceViewingPrintedStateUseCase
{
    public Task<InvoiceViewingPrintedStateResponse> ExecuteAsync(
        InvoiceViewingPrintedStateRequest request,
        CancellationToken cancellationToken) =>
        invoiceViewingService.SetPrintedStateAsync(request, cancellationToken);
}
