namespace FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;

public sealed record InvoiceRenderedDocumentDto(
    string Source,
    string InvoiceId,
    InvoiceDocumentProfile Profile,
    string AppliedXsltName,
    string XsltSource,
    bool UsedEmbeddedXslt,
    string XmlContent,
    string HtmlContent);
