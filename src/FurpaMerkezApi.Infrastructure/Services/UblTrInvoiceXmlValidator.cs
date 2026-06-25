using System.Xml;
using System.Xml.Schema;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class UblTrInvoiceXmlValidator
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";
    private static readonly string InvoiceSchemaRelativePath = Path.Combine(
        "Assets",
        "UblTr",
        "xsdrt",
        "maindoc",
        "UBL-Invoice-2.1.xsd");
    private static readonly string XmlDsigSchemaRelativePath = Path.Combine(
        "Assets",
        "UblTr",
        "xsdrt",
        "common",
        "UBL-xmldsig-core-schema-2.1.xsd");

    private readonly IHostEnvironment hostEnvironment;
    private readonly ILogger<UblTrInvoiceXmlValidator> logger;
    private readonly Lazy<XmlSchemaSet> schemas;

    public UblTrInvoiceXmlValidator(
        IHostEnvironment hostEnvironment,
        ILogger<UblTrInvoiceXmlValidator> logger)
    {
        this.hostEnvironment = hostEnvironment;
        this.logger = logger;
        schemas = new Lazy<XmlSchemaSet>(CreateSchemaSet);
    }

    public void Validate(string xmlContent, string invoiceId)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new ArgumentException("Invoice XML content is required.", nameof(xmlContent));
        }

        var validationErrors = new List<string>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            ValidationType = ValidationType.Schema,
            Schemas = schemas.Value,
            XmlResolver = null
        };
        settings.ValidationEventHandler += (_, args) =>
        {
            var lineInfo = args.Exception is null
                ? string.Empty
                : $" Line={args.Exception.LineNumber}, Position={args.Exception.LinePosition}.";
            validationErrors.Add($"{args.Severity}: {args.Message}{lineInfo}");
        };

        using var stringReader = new StringReader(xmlContent);
        using var reader = XmlReader.Create(stringReader, settings);

        while (reader.Read())
        {
        }

        if (validationErrors.Count == 0)
        {
            return;
        }

        var message =
            $"UBL-TR XSD dogrulamasi basarisiz. InvoiceId={invoiceId}. " +
            string.Join(" | ", validationErrors.Take(10));
        logger.LogWarning(
            "UBL-TR invoice XML validation failed for {InvoiceId}. Errors: {ValidationErrors}",
            invoiceId,
            validationErrors);

        throw new InvalidOperationException(message);
    }

    private XmlSchemaSet CreateSchemaSet()
    {
        var schemaPath = Path.Combine(hostEnvironment.ContentRootPath, InvoiceSchemaRelativePath);
        var xmlDsigSchemaPath = Path.Combine(hostEnvironment.ContentRootPath, XmlDsigSchemaRelativePath);

        if (!File.Exists(schemaPath))
        {
            throw new FileNotFoundException(
                "UBL-TR Invoice XSD asset was not found.",
                schemaPath);
        }

        if (!File.Exists(xmlDsigSchemaPath))
        {
            throw new FileNotFoundException(
                "UBL-TR XML digital signature XSD asset was not found.",
                xmlDsigSchemaPath);
        }

        var schemaSet = new XmlSchemaSet
        {
            XmlResolver = new XmlUrlResolver()
        };
        AddSchema(schemaSet, XmlDsigNamespace, xmlDsigSchemaPath, DtdProcessing.Parse);
        AddSchema(schemaSet, InvoiceNamespace, schemaPath, DtdProcessing.Prohibit);
        schemaSet.Compile();

        return schemaSet;
    }

    private static void AddSchema(
        XmlSchemaSet schemaSet,
        string targetNamespace,
        string schemaPath,
        DtdProcessing dtdProcessing)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = dtdProcessing,
            XmlResolver = new XmlUrlResolver()
        };

        using var reader = XmlReader.Create(schemaPath, settings);
        schemaSet.Add(targetNamespace, reader);
    }
}
