using System.Globalization;

namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

public static class CompanyReceivingDocumentIdentityParser
{
    public const int DocumentOrderNoLength = 9;
    public const int MaxDocumentSerieLength = 20;

    public static bool TryParseOfficialDocumentNo(
        string? officialDocumentNo,
        out CompanyReceivingDocumentIdentity identity)
    {
        identity = default!;
        var value = officialDocumentNo?.Trim();

        if (string.IsNullOrEmpty(value) ||
            value.Any(char.IsWhiteSpace) ||
            value.Length <= DocumentOrderNoLength ||
            value.Length > MaxDocumentSerieLength + DocumentOrderNoLength)
        {
            return false;
        }

        var serie = value[..^DocumentOrderNoLength];
        if (!IsValidSerie(serie) ||
            !int.TryParse(
                value.AsSpan(value.Length - DocumentOrderNoLength),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var orderNo) ||
            orderNo <= 0)
        {
            return false;
        }

        identity = new CompanyReceivingDocumentIdentity(serie.ToUpperInvariant(), orderNo);
        return true;
    }

    public static bool IsValidSerie(string? documentSerie)
    {
        var value = documentSerie?.Trim();
        return !string.IsNullOrEmpty(value) &&
               value.Length <= MaxDocumentSerieLength &&
               value.All(character =>
                   character is >= 'A' and <= 'Z' or
                   >= 'a' and <= 'z' or
                   >= '0' and <= '9');
    }
}

public sealed record CompanyReceivingDocumentIdentity(string DocumentSerie, int DocumentOrderNo);
