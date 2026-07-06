using System.Globalization;
using System.Xml.Linq;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class UblTrInvoiceBusinessRuleValidator(
    ILogger<UblTrInvoiceBusinessRuleValidator> logger)
{
    private const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string AggregateNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string BasicNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly decimal[] AllowedVatRates = [0m, 1m, 8m, 10m, 18m, 20m];
    private static readonly string[] EInvoiceProfiles = ["TICARIFATURA", "TEMELFATURA"];
    private static readonly string[] AllowedInvoiceTypeCodes =
    [
        "SATIS",
        "IADE",
        "ISTISNA",
        "OZELMATRAH"
    ];

    public void Validate(
        string xmlContent,
        string invoiceId,
        InvoiceSendingScenario scenario,
        string targetAlias)
    {
        if (string.IsNullOrWhiteSpace(xmlContent))
        {
            throw new ArgumentException("Invoice XML content is required.", nameof(xmlContent));
        }

        var document = XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
        var errors = new List<string>();
        var invoice = document.Root;
        var aggregate = XNamespace.Get(AggregateNamespace);
        var basic = XNamespace.Get(BasicNamespace);

        if (invoice is null || invoice.Name != XNamespace.Get(InvoiceNamespace) + "Invoice")
        {
            errors.Add("Root element must be UBL Invoice.");
            ThrowIfInvalid(invoiceId, errors);
            return;
        }

        var profileId = RequiredText(invoice, basic + "ProfileID", "ProfileID", errors);
        var invoiceTypeCode = RequiredText(invoice, basic + "InvoiceTypeCode", "InvoiceTypeCode", errors);
        var ublVersion = RequiredText(invoice, basic + "UBLVersionID", "UBLVersionID", errors);
        var customizationId = RequiredText(invoice, basic + "CustomizationID", "CustomizationID", errors);
        var documentCurrencyCode = RequiredText(invoice, basic + "DocumentCurrencyCode", "DocumentCurrencyCode", errors);

        if (!string.Equals(ublVersion, "2.1", StringComparison.Ordinal))
        {
            errors.Add("UBLVersionID must be 2.1.");
        }

        if (!string.Equals(customizationId, "TR1.2", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("CustomizationID must be TR1.2.");
        }

        if (!string.Equals(documentCurrencyCode, "TRY", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("DocumentCurrencyCode must be TRY.");
        }

        ValidateProfile(scenario, profileId, errors);
        ValidateTargetCustomer(scenario, targetAlias, errors);
        ValidateInvoiceType(invoiceTypeCode, errors);
        ValidateParties(invoice, aggregate, basic, scenario, errors);
        ValidateInvoiceLines(invoice, aggregate, basic, errors);
        ValidateTotals(invoice, aggregate, basic, errors);
        ValidateReturnReference(invoice, aggregate, basic, invoiceTypeCode, errors);
        ValidateExemptionRules(invoice, basic, invoiceTypeCode, errors);

        ThrowIfInvalid(invoiceId, errors);
    }

    private static void ValidateProfile(
        InvoiceSendingScenario scenario,
        string profileId,
        List<string> errors)
    {
        if (scenario == InvoiceSendingScenario.EArsiv)
        {
            if (!string.Equals(profileId, "EARSIVFATURA", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("E-Arsiv invoices must use EARSIVFATURA profile.");
            }

            return;
        }

        if (!EInvoiceProfiles.Contains(profileId, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("E-Fatura invoices must use TICARIFATURA or TEMELFATURA profile.");
        }
    }

    private static void ValidateInvoiceType(string invoiceTypeCode, List<string> errors)
    {
        if (!AllowedInvoiceTypeCodes.Contains(invoiceTypeCode, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(
                "InvoiceTypeCode must be one of SATIS, IADE, ISTISNA or OZELMATRAH for this flow.");
        }
    }

    private static void ValidateTargetCustomer(
        InvoiceSendingScenario scenario,
        string targetAlias,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(targetAlias))
        {
            errors.Add("Target customer alias/e-mail is required.");
            return;
        }

        if (scenario == InvoiceSendingScenario.EArsiv && !LooksLikeEmail(targetAlias.Trim()))
        {
            errors.Add("E-Arsiv target customer e-mail must be a valid e-mail address.");
        }
    }

    private static void ValidateParties(
        XElement invoice,
        XNamespace aggregate,
        XNamespace basic,
        InvoiceSendingScenario scenario,
        List<string> errors)
    {
        var supplierParty = invoice
            .Element(aggregate + "AccountingSupplierParty")
            ?.Element(aggregate + "Party");
        var customerParty = invoice
            .Element(aggregate + "AccountingCustomerParty")
            ?.Element(aggregate + "Party");

        ValidateParty(supplierParty, aggregate, basic, "Supplier", requireContactEmail: false, errors);
        ValidateParty(
            customerParty,
            aggregate,
            basic,
            "Customer",
            requireContactEmail: false,
            errors);
    }

    private static void ValidateParty(
        XElement? party,
        XNamespace aggregate,
        XNamespace basic,
        string label,
        bool requireContactEmail,
        List<string> errors)
    {
        if (party is null)
        {
            errors.Add($"{label} party is required.");
            return;
        }

        var taxNumber = party
            .Elements(aggregate + "PartyIdentification")
            .Select(element => element.Element(basic + "ID")?.Value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        var title = party
            .Element(aggregate + "PartyName")
            ?.Element(basic + "Name")
            ?.Value
            ?.Trim() ?? string.Empty;
        var address = party.Element(aggregate + "PostalAddress");
        var street = address?.Element(basic + "StreetName")?.Value?.Trim() ?? string.Empty;
        var city = address?.Element(basic + "CityName")?.Value?.Trim() ?? string.Empty;
        var countryCode = address
            ?.Element(aggregate + "Country")
            ?.Element(basic + "IdentificationCode")
            ?.Value
            ?.Trim() ?? string.Empty;
        var taxSchemeName = party
            .Element(aggregate + "PartyTaxScheme")
            ?.Element(aggregate + "TaxScheme")
            ?.Element(basic + "Name")
            ?.Value
            ?.Trim() ?? string.Empty;
        var email = party
            .Element(aggregate + "Contact")
            ?.Element(basic + "ElectronicMail")
            ?.Value
            ?.Trim() ?? string.Empty;
        var taxIdentity = party
            .Elements(aggregate + "PartyIdentification")
            .Select(element => element.Element(basic + "ID"))
            .FirstOrDefault(id => string.Equals(
                id?.Attribute("schemeID")?.Value,
                "TCKN",
                StringComparison.OrdinalIgnoreCase));

        if (!IsValidTurkishTaxIdentity(taxNumber))
        {
            errors.Add($"{label} VKN/TCKN must contain 10 or 11 digits.");
        }

        if (taxIdentity is not null)
        {
            var person = party.Element(aggregate + "Person");
            var firstName = person?.Element(basic + "FirstName")?.Value?.Trim();
            var familyName = person?.Element(basic + "FamilyName")?.Value?.Trim();

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(familyName))
            {
                errors.Add($"{label} Person with FirstName and FamilyName is required for TCKN.");
            }
        }

        if (string.IsNullOrWhiteSpace(title) || title == "-")
        {
            errors.Add($"{label} title is required.");
        }

        if (string.IsNullOrWhiteSpace(street) || street == "-")
        {
            errors.Add($"{label} street address is required.");
        }

        if (string.IsNullOrWhiteSpace(city) || city == "-")
        {
            errors.Add($"{label} city is required.");
        }

        if (!string.Equals(countryCode, "TR", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} country code must be TR.");
        }

        if (string.IsNullOrWhiteSpace(taxSchemeName))
        {
            errors.Add($"{label} tax office/name is required.");
        }

        if (requireContactEmail && !LooksLikeEmail(email))
        {
            errors.Add("E-Arsiv customer e-mail is required for electronic delivery.");
        }
    }

    private static void ValidateInvoiceLines(
        XElement invoice,
        XNamespace aggregate,
        XNamespace basic,
        List<string> errors)
    {
        var lines = invoice.Elements(aggregate + "InvoiceLine").ToArray();
        var lineCountNumeric = ReadDecimal(invoice.Element(basic + "LineCountNumeric"));

        if (lines.Length == 0)
        {
            errors.Add("At least one InvoiceLine is required.");
            return;
        }

        if (lineCountNumeric.HasValue && lineCountNumeric.Value != lines.Length)
        {
            errors.Add("LineCountNumeric must match InvoiceLine count.");
        }

        foreach (var line in lines)
        {
            var lineId = Text(line.Element(basic + "ID"));
            var quantity = ReadDecimal(line.Element(basic + "InvoicedQuantity"));
            var lineExtensionAmount = ReadDecimal(line.Element(basic + "LineExtensionAmount"));
            var itemName = line
                .Element(aggregate + "Item")
                ?.Element(basic + "Name")
                ?.Value
                ?.Trim() ?? string.Empty;
            var priceAmount = line
                .Element(aggregate + "Price")
                ?.Element(basic + "PriceAmount");

            if (!quantity.HasValue || quantity.Value <= 0m)
            {
                errors.Add($"Line {lineId}: quantity must be greater than zero.");
            }

            if (!lineExtensionAmount.HasValue || lineExtensionAmount.Value < 0m)
            {
                errors.Add($"Line {lineId}: LineExtensionAmount must be zero or greater.");
            }

            if (string.IsNullOrWhiteSpace(itemName) || itemName == "-")
            {
                errors.Add($"Line {lineId}: item name is required.");
            }

            if (!ReadDecimal(priceAmount).HasValue)
            {
                errors.Add($"Line {lineId}: price amount is required.");
            }

            ValidateLineTax(line, aggregate, basic, lineId, errors);
        }
    }

    private static void ValidateLineTax(
        XElement line,
        XNamespace aggregate,
        XNamespace basic,
        string lineId,
        List<string> errors)
    {
        var taxSubtotal = line
            .Element(aggregate + "TaxTotal")
            ?.Element(aggregate + "TaxSubtotal");

        if (taxSubtotal is null)
        {
            errors.Add($"Line {lineId}: TaxSubtotal is required.");
            return;
        }

        var taxableAmount = ReadDecimal(taxSubtotal.Element(basic + "TaxableAmount"));
        var taxAmount = ReadDecimal(taxSubtotal.Element(basic + "TaxAmount"));
        var percent = ReadDecimal(taxSubtotal.Element(basic + "Percent"));
        var taxTypeCode = taxSubtotal
            .Element(aggregate + "TaxCategory")
            ?.Element(aggregate + "TaxScheme")
            ?.Element(basic + "TaxTypeCode")
            ?.Value
            ?.Trim() ?? string.Empty;
        var exemptionCode = taxSubtotal
            .Element(aggregate + "TaxCategory")
            ?.Element(basic + "TaxExemptionReasonCode")
            ?.Value
            ?.Trim() ?? string.Empty;

        if (!string.Equals(taxTypeCode, "0015", StringComparison.Ordinal))
        {
            errors.Add($"Line {lineId}: KDV TaxTypeCode must be 0015.");
        }

        if (!percent.HasValue || !AllowedVatRates.Contains(percent.Value))
        {
            errors.Add($"Line {lineId}: KDV percent must be one of 0, 1, 8, 10, 18 or 20.");
        }

        if (percent is 0m && taxableAmount.GetValueOrDefault() > 0m && string.IsNullOrWhiteSpace(exemptionCode))
        {
            errors.Add($"Line {lineId}: zero-rated taxable line requires TaxExemptionReasonCode.");
        }

        if (taxableAmount.HasValue && taxAmount.HasValue && percent.HasValue && percent.Value > 0m)
        {
            var expectedTax = Math.Round(
                taxableAmount.Value * percent.Value / 100m,
                2,
                MidpointRounding.AwayFromZero);

            if (Math.Abs(expectedTax - taxAmount.Value) > 0.05m)
            {
                errors.Add($"Line {lineId}: KDV amount does not match taxable amount and percent.");
            }
        }
    }

    private static void ValidateTotals(
        XElement invoice,
        XNamespace aggregate,
        XNamespace basic,
        List<string> errors)
    {
        var lines = invoice.Elements(aggregate + "InvoiceLine").ToArray();
        var legalTotal = invoice.Element(aggregate + "LegalMonetaryTotal");
        var taxTotal = invoice.Element(aggregate + "TaxTotal");

        var lineExtensionTotal = ReadCurrencyAmount(legalTotal?.Element(basic + "LineExtensionAmount"));
        var taxExclusiveAmount = ReadCurrencyAmount(legalTotal?.Element(basic + "TaxExclusiveAmount"));
        var taxInclusiveAmount = ReadCurrencyAmount(legalTotal?.Element(basic + "TaxInclusiveAmount"));
        var chargeTotalAmount = ReadCurrencyAmount(legalTotal?.Element(basic + "ChargeTotalAmount")) ?? 0m;
        var payableAmount = ReadCurrencyAmount(legalTotal?.Element(basic + "PayableAmount"));
        var taxAmount = ReadCurrencyAmount(taxTotal?.Element(basic + "TaxAmount"));

        var lineSum = RoundMoney(lines.Sum(line =>
            ReadCurrencyAmount(line.Element(basic + "LineExtensionAmount")) ?? 0m));
        var lineTaxSum = RoundMoney(lines.Sum(line =>
            ReadCurrencyAmount(line.Element(aggregate + "TaxTotal")?.Element(basic + "TaxAmount")) ?? 0m));

        MustMatch(lineExtensionTotal, lineSum, "LegalMonetaryTotal.LineExtensionAmount", errors);
        MustMatch(taxExclusiveAmount, lineSum, "LegalMonetaryTotal.TaxExclusiveAmount", errors);
        MustMatch(taxAmount, lineTaxSum, "TaxTotal.TaxAmount", errors);

        if (taxInclusiveAmount.HasValue)
        {
            MustMatch(taxInclusiveAmount, lineSum + lineTaxSum + chargeTotalAmount, "TaxInclusiveAmount", errors);
        }

        if (payableAmount.HasValue && taxInclusiveAmount.HasValue)
        {
            MustMatch(payableAmount, taxInclusiveAmount.Value, "PayableAmount", errors);
        }
    }

    private static void ValidateReturnReference(
        XElement invoice,
        XNamespace aggregate,
        XNamespace basic,
        string invoiceTypeCode,
        List<string> errors)
    {
        if (!string.Equals(invoiceTypeCode, "IADE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var reference = invoice
            .Element(aggregate + "BillingReference")
            ?.Element(aggregate + "InvoiceDocumentReference");
        var referenceNo = reference?.Element(basic + "ID")?.Value?.Trim() ?? string.Empty;
        var referenceDate = reference?.Element(basic + "IssueDate")?.Value?.Trim() ?? string.Empty;
        var documentTypeCode = reference?.Element(basic + "DocumentTypeCode")?.Value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(referenceNo))
        {
            errors.Add("Return invoice requires BillingReference invoice number.");
        }
        else if (referenceNo.Length != 16)
        {
            errors.Add("Return invoice BillingReference invoice number must be 16 characters.");
        }

        if (!DateTime.TryParseExact(
                referenceDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            errors.Add("Return invoice requires BillingReference IssueDate in yyyy-MM-dd format.");
        }

        if (!string.Equals(documentTypeCode, "IADE", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Return invoice requires BillingReference DocumentTypeCode IADE.");
        }
    }

    private static void ValidateExemptionRules(
        XElement invoice,
        XNamespace basic,
        string invoiceTypeCode,
        List<string> errors)
    {
        var exemptionCodes = invoice
            .Descendants(basic + "TaxExemptionReasonCode")
            .Select(element => element.Value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();

        if (invoiceTypeCode is "ISTISNA" or "OZELMATRAH" && exemptionCodes.Length == 0)
        {
            errors.Add($"{invoiceTypeCode} invoice requires TaxExemptionReasonCode.");
        }

        if (invoiceTypeCode is "SATIS" or "IADE")
        {
            var hasZeroTaxableLine = invoice
                .Descendants()
                .Where(element => element.Name.LocalName == "TaxSubtotal")
                .Any(element =>
                    ReadDecimal(element.Element(basic + "Percent")) == 0m &&
                    ReadDecimal(element.Element(basic + "TaxableAmount")).GetValueOrDefault() > 0m);

            if (!hasZeroTaxableLine && exemptionCodes.Length > 0)
            {
                errors.Add("TaxExemptionReasonCode should only be present for zero-rated taxable lines.");
            }
        }
    }

    private void ThrowIfInvalid(string invoiceId, List<string> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        var message =
            $"UBL-TR is kurali dogrulamasi basarisiz. InvoiceId={invoiceId}. " +
            string.Join(" | ", errors.Take(15));
        logger.LogWarning(
            "UBL-TR invoice business rule validation failed for {InvoiceId}. Errors: {ValidationErrors}",
            invoiceId,
            errors);

        throw new InvalidOperationException(message);
    }

    private static string RequiredText(
        XElement parent,
        XName name,
        string label,
        List<string> errors)
    {
        var value = Text(parent.Element(name));

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{label} is required.");
        }

        return value;
    }

    private static string Text(XElement? element) =>
        element?.Value?.Trim() ?? string.Empty;

    private static decimal? ReadCurrencyAmount(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var currency = element.Attribute("currencyID")?.Value?.Trim();
        if (!string.Equals(currency, "TRY", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ReadDecimal(element);
    }

    private static decimal? ReadDecimal(XElement? element)
    {
        var value = element?.Value?.Trim();

        return decimal.TryParse(
            value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static void MustMatch(
        decimal? actual,
        decimal expected,
        string label,
        List<string> errors)
    {
        if (!actual.HasValue)
        {
            errors.Add($"{label} is required and must use TRY currency.");
            return;
        }

        if (Math.Abs(actual.Value - RoundMoney(expected)) > 0.05m)
        {
            errors.Add($"{label} does not match calculated total.");
        }
    }

    private static bool IsValidTurkishTaxIdentity(string value) =>
        value.Length is 10 or 11 && value.All(char.IsDigit);

    private static bool LooksLikeEmail(string value) =>
        value.Contains('@') &&
        value.IndexOf('@') > 0 &&
        value.LastIndexOf('.') > value.IndexOf('@') + 1;

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
