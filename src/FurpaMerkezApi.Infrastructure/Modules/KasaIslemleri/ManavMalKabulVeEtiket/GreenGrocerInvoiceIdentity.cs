using System.Globalization;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.ManavMalKabulVeEtiket;

internal static class GreenGrocerInvoiceIdentity
{
    private const int OfficialDocumentLength = 16;
    private const int PrefixLength = 3;
    private const int YearLength = 4;
    private const int OrderNoLength = 9;

    internal static bool TryParseOfficialDocumentNo(
        string? documentNo,
        out GreenGrocerInvoiceDocumentIdentity identity)
    {
        identity = default!;
        var value = documentNo?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value) || value.Length != OfficialDocumentLength)
        {
            return false;
        }

        var prefix = value[..PrefixLength];
        var year = value.Substring(PrefixLength, YearLength);
        var orderNoText = value[^OrderNoLength..];
        if (!prefix.All(IsAsciiLetterOrDigit) ||
            !year.All(char.IsAsciiDigit) ||
            !int.TryParse(orderNoText, NumberStyles.None, CultureInfo.InvariantCulture, out var orderNo) ||
            orderNo <= 0)
        {
            return false;
        }

        identity = new GreenGrocerInvoiceDocumentIdentity(
            prefix + year[^2..],
            orderNo,
            value);
        return true;
    }

    internal static string BuildCandidateSeries(string baseSeries, int suffix) =>
        suffix <= 0 ? baseSeries : baseSeries + suffix.ToString(CultureInfo.InvariantCulture);

    internal static string? NormalizeInvoiceUuid(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (!Guid.TryParse(normalized, out var invoiceUuid))
        {
            throw new ArgumentException("Invoice UUID (ETTN) is not a valid GUID.", nameof(value));
        }

        return invoiceUuid.ToString("D").ToUpperInvariant();
    }

    internal static bool TryNormalizeInvoiceUuid(string? value, out string? normalized)
    {
        normalized = null;
        var candidate = value?.Trim();
        if (string.IsNullOrEmpty(candidate) || !Guid.TryParse(candidate, out var invoiceUuid))
        {
            return false;
        }

        normalized = invoiceUuid.ToString("D").ToUpperInvariant();
        return true;
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'A' and <= 'Z' or >= '0' and <= '9';
}

internal sealed record GreenGrocerInvoiceDocumentIdentity(
    string BaseSeries,
    int DocumentOrderNo,
    string OfficialDocumentNo);
