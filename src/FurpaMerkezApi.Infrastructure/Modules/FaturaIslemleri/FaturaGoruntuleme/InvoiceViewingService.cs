using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

public sealed class InvoiceViewingService(
    UyumsoftInboxInvoiceSyncService syncService,
    InvoiceViewingQueryExecutor queryExecutor,
    IEInvoiceDocumentRenderer invoiceDocumentRenderer)
{
    public async Task<InvoiceViewingListResponse> ListAsync(
        InvoiceViewingListRequest request,
        CancellationToken cancellationToken)
        => await queryExecutor.ListAsync(request, cancellationToken);

    public Task<InvoiceViewingSynchronizationResponse> SynchronizeAsync(
        InvoiceViewingSynchronizationRequest request,
        CancellationToken cancellationToken) =>
        syncService.SynchronizeRangeAsync(
            request.StartDate,
            request.EndDate,
            request.IncludeStatuses,
            cancellationToken);

    public Task<InvoiceViewingDetailDto> GetAsync(
        InvoiceViewingDetailRequest request,
        CancellationToken cancellationToken) =>
        RenderAsync(
            new InvoiceViewingRenderRequest(
                request.DocumentId,
                InvoiceDocumentProfile.Auto,
                null,
                true),
            cancellationToken);

    public async Task<InvoiceViewingDetailDto> RenderAsync(
        InvoiceViewingRenderRequest request,
        CancellationToken cancellationToken)
    {
        await syncService.EnsureInvoiceExistsAsync(request.DocumentId, cancellationToken);

        var renderContext = await queryExecutor.GetRenderContextByLookupIdAsync(request.DocumentId, cancellationToken);
        var summary = renderContext.Summary;
        var preferEmbeddedXslt = request.PreferEmbeddedXslt ?? !summary.IsStandard;
        var renderedDocument = await invoiceDocumentRenderer.RenderInboxInvoiceAsync(
            renderContext.LookupIds,
            request.Profile,
            preferEmbeddedXslt,
            cancellationToken: cancellationToken,
            fallbackToDefaultXslt: request.FallbackToDefaultXslt);

        return new InvoiceViewingDetailDto(
            summary,
            renderedDocument with
            {
                InvoiceId = summary.InvoiceId
            });
    }

    public async Task<InvoiceViewingPrintedStateResponse> SetPrintedStateAsync(
        InvoiceViewingPrintedStateRequest request,
        CancellationToken cancellationToken)
    {
        await syncService.EnsureInvoiceExistsAsync(request.DocumentId, cancellationToken);

        var summary = await queryExecutor.UpdatePrintedStateAsync(
            request.DocumentId,
            request.IsPrinted,
            cancellationToken);

        return new InvoiceViewingPrintedStateResponse(summary, request.Source);
    }
}
