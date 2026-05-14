using System.Globalization;
using System.Text;

namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;

public static class CompanyOrderDocumentKey
{
    public static string? CreateOrNull(
        int warehouseNo,
        string? documentSerie,
        int documentOrderNo)
    {
        if (warehouseNo <= 0 || string.IsNullOrWhiteSpace(documentSerie) || documentOrderNo < 0)
        {
            return null;
        }

        return Create(warehouseNo, documentSerie, documentOrderNo);
    }

    public static string Create(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        if (string.IsNullOrWhiteSpace(documentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(documentSerie));
        }

        if (documentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(documentOrderNo));
        }

        var rawValue = string.Join(
            '|',
            warehouseNo.ToString(CultureInfo.InvariantCulture),
            documentSerie.Trim(),
            documentOrderNo.ToString(CultureInfo.InvariantCulture));

        return ToBase64Url(Encoding.UTF8.GetBytes(rawValue));
    }

    public static CompanyOrderDetailRequest Parse(string documentKey)
    {
        if (string.IsNullOrWhiteSpace(documentKey))
        {
            throw new ArgumentException("Document key is required.", nameof(documentKey));
        }

        try
        {
            var rawValue = Encoding.UTF8.GetString(FromBase64Url(documentKey));
            var parts = rawValue.Split('|');

            if (parts.Length != 3 ||
                !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var warehouseNo) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var documentOrderNo))
            {
                throw new ArgumentException("Document key is invalid.", nameof(documentKey));
            }

            return new CompanyOrderDetailRequest(
                warehouseNo,
                parts[1],
                documentOrderNo);
        }
        catch (FormatException)
        {
            throw new ArgumentException("Document key is invalid.", nameof(documentKey));
        }
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var normalizedValue = value.Replace('-', '+').Replace('_', '/');
        var padding = normalizedValue.Length % 4;

        if (padding > 0)
        {
            normalizedValue = normalizedValue.PadRight(normalizedValue.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(normalizedValue);
    }
}
