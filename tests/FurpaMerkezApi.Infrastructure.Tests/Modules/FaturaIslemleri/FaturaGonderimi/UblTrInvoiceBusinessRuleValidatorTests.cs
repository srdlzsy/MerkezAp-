using System.Xml.Linq;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;
using FurpaMerkezApi.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.FaturaIslemleri.FaturaGonderimi;

public sealed class UblTrInvoiceBusinessRuleValidatorTests
{
    private readonly UblTrInvoiceBusinessRuleValidator validator =
        new(NullLogger<UblTrInvoiceBusinessRuleValidator>.Instance);

    [Fact]
    public void Validate_AcceptsDiscountedLineWithConsistentTotals()
    {
        var xml = BuildInvoiceXml(
            lineExtensionAmount: 90m,
            taxableAmount: 90m,
            taxAmount: 9m,
            taxExclusiveAmount: 90m,
            payableAmount: 99m);

        validator.Validate(xml, "FAT2026000000001", InvoiceSendingScenario.EFatura, "urn:mail:test@example.com");
    }

    [Fact]
    public void Validate_RejectsGrossLineAmountWhenDiscountExists()
    {
        var xml = BuildInvoiceXml(
            lineExtensionAmount: 100m,
            taxableAmount: 100m,
            taxAmount: 9m,
            taxExclusiveAmount: 110m,
            payableAmount: 95m);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            validator.Validate(xml, "FAT2026000000001", InvoiceSendingScenario.EFatura, "urn:mail:test@example.com"));

        Assert.Contains("Line 1: LineExtensionAmount", exception.Message, StringComparison.Ordinal);
        Assert.Contains("KDV amount does not match", exception.Message, StringComparison.Ordinal);
    }

    private static string BuildInvoiceXml(
        decimal lineExtensionAmount,
        decimal taxableAmount,
        decimal taxAmount,
        decimal taxExclusiveAmount,
        decimal payableAmount)
    {
        XNamespace invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        XNamespace aggregate = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace basic = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

        var document = new XDocument(
            new XElement(
                invoice + "Invoice",
                new XAttribute(XNamespace.Xmlns + "cac", aggregate.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "cbc", basic.NamespaceName),
                new XElement(basic + "UBLVersionID", "2.1"),
                new XElement(basic + "CustomizationID", "TR1.2"),
                new XElement(basic + "ProfileID", "TICARIFATURA"),
                new XElement(basic + "ID", "FAT2026000000001"),
                new XElement(basic + "UUID", Guid.NewGuid()),
                new XElement(basic + "IssueDate", "2026-09-01"),
                new XElement(basic + "InvoiceTypeCode", "SATIS"),
                new XElement(basic + "DocumentCurrencyCode", "TRY"),
                new XElement(basic + "LineCountNumeric", "1"),
                BuildParty(aggregate, basic, "AccountingSupplierParty", "1234567890", true),
                BuildParty(aggregate, basic, "AccountingCustomerParty", "0987654321", false),
                new XElement(
                    aggregate + "TaxTotal",
                    Amount(basic, "TaxAmount", taxAmount),
                    new XElement(
                        aggregate + "TaxSubtotal",
                        Amount(basic, "TaxableAmount", taxableAmount),
                        Amount(basic, "TaxAmount", taxAmount),
                        new XElement(basic + "Percent", "10"),
                        new XElement(
                            aggregate + "TaxCategory",
                            new XElement(
                                aggregate + "TaxScheme",
                                new XElement(basic + "Name", "KDV"),
                                new XElement(basic + "TaxTypeCode", "0015"))))),
                new XElement(
                    aggregate + "LegalMonetaryTotal",
                    Amount(basic, "LineExtensionAmount", lineExtensionAmount),
                    Amount(basic, "TaxExclusiveAmount", taxExclusiveAmount),
                    Amount(basic, "TaxInclusiveAmount", payableAmount),
                    Amount(basic, "AllowanceTotalAmount", 10m),
                    Amount(basic, "ChargeTotalAmount", 0m),
                    Amount(basic, "PayableAmount", payableAmount)),
                new XElement(
                    aggregate + "InvoiceLine",
                    new XElement(basic + "ID", "1"),
                    new XElement(basic + "InvoicedQuantity", new XAttribute("unitCode", "C62"), "2"),
                    Amount(basic, "LineExtensionAmount", lineExtensionAmount),
                    new XElement(
                        aggregate + "AllowanceCharge",
                        new XElement(basic + "ChargeIndicator", "false"),
                        new XElement(basic + "MultiplierFactorNumeric", "0.10"),
                        Amount(basic, "Amount", 10m),
                        Amount(basic, "BaseAmount", 100m)),
                    new XElement(
                        aggregate + "TaxTotal",
                        Amount(basic, "TaxAmount", taxAmount),
                        new XElement(
                            aggregate + "TaxSubtotal",
                            Amount(basic, "TaxableAmount", taxableAmount),
                            Amount(basic, "TaxAmount", taxAmount),
                            new XElement(basic + "Percent", "10"),
                            new XElement(
                                aggregate + "TaxCategory",
                                new XElement(
                                    aggregate + "TaxScheme",
                                    new XElement(basic + "Name", "KDV"),
                                    new XElement(basic + "TaxTypeCode", "0015"))))),
                    new XElement(
                        aggregate + "Item",
                        new XElement(basic + "Name", "Test urunu")),
                    new XElement(
                        aggregate + "Price",
                        Amount(basic, "PriceAmount", 50m)))));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement BuildParty(
        XNamespace aggregate,
        XNamespace basic,
        string elementName,
        string taxNumber,
        bool includeAddress)
    {
        return new XElement(
            aggregate + elementName,
            new XElement(
                aggregate + "Party",
                new XElement(
                    aggregate + "PartyIdentification",
                    new XElement(basic + "ID", new XAttribute("schemeID", "VKN"), taxNumber)),
                new XElement(
                    aggregate + "PartyName",
                    new XElement(basic + "Name", "Test firma")),
                new XElement(
                    aggregate + "PostalAddress",
                    includeAddress ? new XElement(basic + "StreetName", "Test cadde") : null,
                    includeAddress ? new XElement(basic + "CityName", "Bursa") : null,
                    new XElement(
                        aggregate + "Country",
                        new XElement(basic + "IdentificationCode", "TR"))),
                new XElement(
                    aggregate + "PartyTaxScheme",
                    new XElement(
                        aggregate + "TaxScheme",
                        new XElement(basic + "Name", "Test vergi dairesi")))));
    }

    private static XElement Amount(XNamespace basic, string name, decimal value) =>
        new(
            basic + name,
            new XAttribute("currencyID", "TRY"),
            value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
}
