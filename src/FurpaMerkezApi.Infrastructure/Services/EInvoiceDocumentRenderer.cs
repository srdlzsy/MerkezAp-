using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.Common;
using Microsoft.Extensions.Hosting;
using QRCoder;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class EInvoiceDocumentRenderer(
    IUyumsoftConnectedQueryService queryService,
    IHostEnvironment hostEnvironment)
    : IEInvoiceDocumentRenderer
{
    private const string InboxSource = "inbox";
    private const string OutboxSource = "outbox";

    public async Task<InvoiceRenderedDocumentDto> RenderInboxInvoiceAsync(
        string invoiceLookupId,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        CancellationToken cancellationToken = default,
        bool fallbackToDefaultXslt = true)
        => await RenderInboxInvoiceAsync(
            new[] { invoiceLookupId },
            profile,
            preferEmbeddedXslt,
            cancellationToken,
            fallbackToDefaultXslt);

    public async Task<InvoiceRenderedDocumentDto> RenderInboxInvoiceAsync(
        IReadOnlyCollection<string> invoiceLookupIds,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        CancellationToken cancellationToken = default,
        bool fallbackToDefaultXslt = true)
    {
        var normalizedLookupIds = NormalizeLookupIds(invoiceLookupIds);
        List<string>? failures = null;

        if (normalizedLookupIds.Count == 0)
        {
            throw new ArgumentException("Invoice lookup id is required.", nameof(invoiceLookupIds));
        }

        foreach (var invoiceLookupId in normalizedLookupIds)
        {
            string invoiceXml;

            try
            {
                invoiceXml = await FetchInvoiceXmlAsync("GetInboxInvoice", invoiceLookupId, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                failures ??= [];
                failures.Add($"{invoiceLookupId}: {exception.Message}");
                continue;
            }

            return await RenderXmlAsync(
                InboxSource,
                invoiceLookupId,
                invoiceXml,
                profile,
                preferEmbeddedXslt,
                cancellationToken,
                fallbackToDefaultXslt);
        }

        throw new InvalidOperationException(
            "Uyumsoft GetInboxInvoice could not resolve invoice with any known lookup id. " +
            $"Attempts: {string.Join(" | ", failures ?? normalizedLookupIds)}");
    }

    public async Task<InvoiceRenderedDocumentDto> RenderOutboxInvoiceAsync(
        string invoiceLookupId,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        CancellationToken cancellationToken = default,
        bool fallbackToDefaultXslt = true)
    {
        var invoiceXml = await FetchInvoiceXmlAsync("GetOutboxInvoice", invoiceLookupId, cancellationToken);

        return await RenderXmlAsync(
            OutboxSource,
            invoiceLookupId,
            invoiceXml,
            profile,
            preferEmbeddedXslt,
            cancellationToken,
            fallbackToDefaultXslt);
    }

    public async Task<InvoiceRenderedDocumentDto> RenderXmlAsync(
        string source,
        string invoiceId,
        string xmlContent,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        CancellationToken cancellationToken = default,
        bool fallbackToDefaultXslt = true)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new ArgumentException("Invoice XML content is required.", nameof(xmlContent));
        }

        var invoiceDocument = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
        var resolvedProfile = profile == InvoiceDocumentProfile.Auto
            ? DetectProfile(invoiceDocument)
            : profile;
        var resolvedXslt = await ResolveXsltAsync(
            invoiceDocument,
            resolvedProfile,
            preferEmbeddedXslt,
            fallbackToDefaultXslt,
            cancellationToken);
        var htmlContent = AddStaticQrCode(
            invoiceDocument,
            TransformToHtml(invoiceDocument, resolvedXslt));

        return new InvoiceRenderedDocumentDto(
            source,
            string.IsNullOrWhiteSpace(invoiceId) ? "manual-preview" : invoiceId.Trim(),
            resolvedProfile,
            resolvedXslt.Name,
            resolvedXslt.Source,
            resolvedXslt.UsedEmbeddedXslt,
            invoiceDocument.ToString(SaveOptions.DisableFormatting),
            htmlContent);
    }

    private async Task<string> FetchInvoiceXmlAsync(
        string operationName,
        string invoiceLookupId,
        CancellationToken cancellationToken)
    {
        var response = await queryService.InvokeGetOperationAsync(
            UyumsoftConnectedServiceKind.EInvoice,
            new UyumsoftOperationInvocationRequest(
                operationName,
                [new UyumsoftOperationParameterRequest("invoiceId", invoiceLookupId)]),
            cancellationToken);

        return ExtractInvoiceXml(response);
    }

    private static IReadOnlyCollection<string> NormalizeLookupIds(
        IEnumerable<string?> invoiceLookupIds) =>
        invoiceLookupIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string ExtractInvoiceXml(UyumsoftOperationResponseDto response)
    {
        var candidates = new List<string?>(capacity: response.Nodes.Count + 2)
        {
            response.ResponsePayloadJson,
            response.ScalarValue
        };

        candidates.AddRange(response.Nodes.SelectMany(FlattenNodeValues));

        foreach (var candidate in candidates)
        {
            if (TryExtractInvoiceXml(candidate, out var xmlContent))
            {
                return xmlContent;
            }
        }

        throw new InvalidOperationException(
            $"Uyumsoft {response.OperationName} response does not contain an invoice XML payload.");
    }

    private static IEnumerable<string?> FlattenNodeValues(UyumsoftResponseNodeDto node)
    {
        yield return node.Value;

        foreach (var child in node.Children)
        {
            foreach (var value in FlattenNodeValues(child))
            {
                yield return value;
            }
        }
    }

    private static bool TryExtractInvoiceXml(string? candidate, out string xmlContent)
    {
        xmlContent = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var decoded = WebUtility.HtmlDecode(candidate.Trim());

        if (TryParseInvoiceDocument(decoded, out var invoiceDocument))
        {
            xmlContent = invoiceDocument.Root!.ToString(SaveOptions.DisableFormatting);
            return true;
        }

        var startIndex = decoded.IndexOf("<Invoice", StringComparison.OrdinalIgnoreCase);
        var endIndex = decoded.LastIndexOf("</Invoice>", StringComparison.OrdinalIgnoreCase);

        if (startIndex < 0 || endIndex <= startIndex)
        {
            return false;
        }

        var snippet = decoded[startIndex..(endIndex + "</Invoice>".Length)];

        if (!TryParseInvoiceDocument(snippet, out invoiceDocument))
        {
            return false;
        }

        xmlContent = invoiceDocument.Root!.ToString(SaveOptions.DisableFormatting);
        return true;
    }

    private static bool TryParseInvoiceDocument(string xmlContent, out XDocument invoiceDocument)
    {
        invoiceDocument = default!;

        try
        {
            var parsedDocument = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
            var invoiceElement = FindInvoiceElement(parsedDocument);

            if (invoiceElement is null)
            {
                return false;
            }

            invoiceDocument = new XDocument(invoiceElement);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static XElement? FindInvoiceElement(XDocument document) =>
        document.Root?
            .DescendantsAndSelf()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "Invoice", StringComparison.OrdinalIgnoreCase) &&
                element.HasElements);

    private async Task<ResolvedXslt> ResolveXsltAsync(
        XDocument invoiceDocument,
        InvoiceDocumentProfile profile,
        bool preferEmbeddedXslt,
        bool fallbackToDefaultXslt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (preferEmbeddedXslt && TryResolveEmbeddedXslt(invoiceDocument, out var embeddedXslt))
        {
            return embeddedXslt;
        }

        if (preferEmbeddedXslt && !fallbackToDefaultXslt)
        {
            throw new InvalidOperationException(
                "Embedded XSLT could not be resolved for this invoice document.");
        }

        var xsltFileName = profile == InvoiceDocumentProfile.EArsiv
            ? "earsiv.xslt"
            : "efatura.xslt";
        var xsltPath = Path.Combine(hostEnvironment.ContentRootPath, "Assets", "Xslt", xsltFileName);

        if (!File.Exists(xsltPath))
        {
            throw new FileNotFoundException(
                $"Invoice XSLT asset was not found: {xsltFileName}",
                xsltPath);
        }

        var content = await File.ReadAllTextAsync(xsltPath, cancellationToken);

        return new ResolvedXslt(
            xsltFileName,
            profile == InvoiceDocumentProfile.EArsiv ? "asset-earsiv" : "asset-efatura",
            false,
            content,
            xsltPath);
    }

    private static bool TryResolveEmbeddedXslt(
        XDocument invoiceDocument,
        out ResolvedXslt resolvedXslt)
    {
        resolvedXslt = default!;

        var references = invoiceDocument.Root?
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "AdditionalDocumentReference", StringComparison.OrdinalIgnoreCase))
            .ToArray()
            ?? Array.Empty<XElement>();

        foreach (var reference in references)
        {
            var candidateName = ResolveEmbeddedXsltName(reference);
            var binaryObject = reference
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "EmbeddedDocumentBinaryObject", StringComparison.OrdinalIgnoreCase) &&
                    IsXsltAttachment(element, candidateName));

            if (binaryObject is null)
            {
                continue;
            }

            var xsltContent = DecodeEmbeddedXslt(binaryObject.Value);

            if (string.IsNullOrWhiteSpace(xsltContent) ||
                !xsltContent.Contains("<xsl:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resolvedXslt = new ResolvedXslt(
                string.IsNullOrWhiteSpace(candidateName) ? "embedded-xslt" : candidateName,
                "embedded-attachment",
                true,
                xsltContent,
                null);
            return true;
        }

        return false;
    }

    private static string ResolveEmbeddedXsltName(XElement reference)
    {
        foreach (var element in reference.Descendants())
        {
            var value = element.Value?.Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!value.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase) &&
                !value.EndsWith(".xsl", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return value;
        }

        return string.Empty;
    }

    private static bool IsXsltAttachment(XElement element, string candidateName)
    {
        if (!string.IsNullOrWhiteSpace(candidateName))
        {
            return true;
        }

        var mimeCode = element.Attributes()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Name.LocalName, "mimeCode", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
        var fileName = element.Attributes()
            .FirstOrDefault(attribute =>
                string.Equals(attribute.Name.LocalName, "filename", StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();

        return (!string.IsNullOrWhiteSpace(fileName) &&
                (fileName.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase) ||
                 fileName.EndsWith(".xsl", StringComparison.OrdinalIgnoreCase))) ||
               (!string.IsNullOrWhiteSpace(mimeCode) &&
                mimeCode.Contains("xsl", StringComparison.OrdinalIgnoreCase));
    }

    private static string DecodeEmbeddedXslt(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var trimmed = rawValue.Trim();

        if (trimmed.Contains("<xsl:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        try
        {
            var bytes = Convert.FromBase64String(trimmed);

            foreach (var encoding in GetCandidateEncodings())
            {
                var text = encoding.GetString(bytes);

                if (text.Contains("<xsl:", StringComparison.OrdinalIgnoreCase) ||
                    text.Contains("<xsl", StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static IEnumerable<Encoding> GetCandidateEncodings()
    {
        yield return Encoding.UTF8;
        yield return Encoding.Unicode;
        yield return Encoding.BigEndianUnicode;
        yield return Encoding.GetEncoding(1254);
        yield return Encoding.ASCII;
    }

    private static InvoiceDocumentProfile DetectProfile(XDocument invoiceDocument)
    {
        var profileTokens = invoiceDocument.Root?
            .DescendantsAndSelf()
            .Where(element =>
                element.Name.LocalName is "ProfileID" or "ScenarioId" or "DocumentTypeCode")
            .Select(element => element.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray()
            ?? Array.Empty<string>();

        return profileTokens.Any(value =>
                   value.Contains("EARSIV", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("ARSIV", StringComparison.OrdinalIgnoreCase))
            ? InvoiceDocumentProfile.EArsiv
            : InvoiceDocumentProfile.EFatura;
    }

    private static string TransformToHtml(XDocument invoiceDocument, ResolvedXslt resolvedXslt)
    {
        var xmlReaderSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse
        };
        var transform = new XslCompiledTransform();
        using var xsltReader = resolvedXslt.FilePath is null
            ? XmlReader.Create(new StringReader(resolvedXslt.Content), xmlReaderSettings)
            : XmlReader.Create(resolvedXslt.FilePath, xmlReaderSettings);
        using var invoiceReader = XmlReader.Create(
            new StringReader(invoiceDocument.ToString(SaveOptions.DisableFormatting)),
            xmlReaderSettings);
        using var stringWriter = new StringWriter();

        transform.Load(xsltReader);
        transform.Transform(invoiceReader, null, stringWriter);

        return stringWriter.ToString();
    }

    private static string AddStaticQrCode(XDocument invoiceDocument, string htmlContent)
    {
        if (string.IsNullOrWhiteSpace(htmlContent) ||
            !htmlContent.Contains("id=\"qrcode\"", StringComparison.OrdinalIgnoreCase))
        {
            return htmlContent;
        }

        var payload = BuildInvoiceQrPayload(invoiceDocument);

        if (payload.Count == 0)
        {
            return htmlContent;
        }

        var json = JsonSerializer.Serialize(payload);
        using var qrCodeData = QRCodeGenerator.GenerateQrCode(
            json,
            QRCodeGenerator.ECCLevel.M);
        using var qrCode = new SvgQRCode(qrCodeData);
        var svg = qrCode.GetGraphic(4);

        return Regex.Replace(
            htmlContent,
            @"<div(?<attributes>[^>]*\bid\s*=\s*[""']qrcode[""'][^>]*)>\s*</div>",
            match => $"<div{match.Groups["attributes"].Value}>{svg}</div>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    private static Dictionary<string, string> BuildInvoiceQrPayload(XDocument invoiceDocument)
    {
        var invoice = FindInvoiceElement(invoiceDocument);

        if (invoice is null)
        {
            return [];
        }

        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["vkntckn"] = ResolvePartyTaxNumber(invoice, "AccountingSupplierParty"),
            ["avkntckn"] = ResolvePartyTaxNumber(invoice, "AccountingCustomerParty"),
            ["senaryo"] = ReadDirectValue(invoice, "ProfileID"),
            ["tip"] = ReadDirectValue(invoice, "InvoiceTypeCode"),
            ["tarih"] = ReadDirectValue(invoice, "IssueDate"),
            ["no"] = ReadDirectValue(invoice, "ID"),
            ["ettn"] = ReadDirectValue(invoice, "UUID"),
            ["parabirimi"] = ReadDirectValue(invoice, "DocumentCurrencyCode")
        };

        var monetaryTotal = invoice
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "LegalMonetaryTotal");

        payload["malhizmettoplam"] = ReadDirectValue(monetaryTotal, "LineExtensionAmount");

        var taxSubtotals = invoice
            .Elements()
            .Where(element => element.Name.LocalName == "TaxTotal")
            .Elements()
            .Where(element => element.Name.LocalName == "TaxSubtotal")
            .Where(IsVatSubtotal);

        foreach (var subtotal in taxSubtotals)
        {
            var rate = ReadDirectValue(subtotal, "Percent");

            if (string.IsNullOrWhiteSpace(rate))
            {
                continue;
            }

            payload[$"kdvmatrah({rate})"] = ReadDirectValue(subtotal, "TaxableAmount");
            payload[$"hesaplanankdv({rate})"] = ReadDirectValue(subtotal, "TaxAmount");
        }

        payload["vergidahil"] = ReadDirectValue(monetaryTotal, "TaxInclusiveAmount");
        payload["odenecek"] = ReadDirectValue(monetaryTotal, "PayableAmount");

        return payload;
    }

    private static bool IsVatSubtotal(XElement subtotal) =>
        subtotal
            .Descendants()
            .Any(element =>
                element.Name.LocalName == "TaxTypeCode" &&
                string.Equals(element.Value.Trim(), "0015", StringComparison.Ordinal));

    private static string ResolvePartyTaxNumber(XElement invoice, string partyElementName)
    {
        var party = invoice
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == partyElementName);

        return party?
                   .Descendants()
                   .FirstOrDefault(element =>
                       element.Name.LocalName == "ID" &&
                       element.Attributes().Any(attribute =>
                           attribute.Name.LocalName == "schemeID" &&
                           (string.Equals(attribute.Value, "VKN", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(attribute.Value, "TCKN", StringComparison.OrdinalIgnoreCase))))
                   ?.Value
                   .Trim()
               ?? string.Empty;
    }

    private static string ReadDirectValue(XElement? parent, string elementName) =>
        parent?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == elementName)
            ?.Value
            .Trim()
        ?? string.Empty;

    private sealed record ResolvedXslt(
        string Name,
        string Source,
        bool UsedEmbeddedXslt,
        string Content,
        string? FilePath);
}
