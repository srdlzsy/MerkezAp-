using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;

namespace FurpaMerkezApi.Application.Abstractions.Services;

public interface IEInvoiceDocumentRenderer
{
    Task<InvoiceRenderedDocumentDto> RenderInboxInvoiceAsync(
        string invoiceLookupId,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        CancellationToken cancellationToken = default,
        bool fallbackToDefaultXslt = true);

    Task<InvoiceRenderedDocumentDto> RenderOutboxInvoiceAsync(
        string invoiceLookupId,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        CancellationToken cancellationToken = default,
        bool fallbackToDefaultXslt = true);

    Task<InvoiceRenderedDocumentDto> RenderXmlAsync(
        string source,
        string invoiceId,
        string xmlContent,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        CancellationToken cancellationToken = default,
        bool fallbackToDefaultXslt = true);
}
