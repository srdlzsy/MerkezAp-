namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.Common;

public static class BarcodeLookupNormalizer
{
    public static BarcodeLookupInfo Normalize(string barcode)
    {
        var originalBarcode = barcode.Trim();
        var isCheckDigitValid = IsEan13Candidate(originalBarcode)
            ? IsValidEan13(originalBarcode)
            : (bool?)null;

        if (IsVariableWeightBarcode(originalBarcode))
        {
            var quantityValue = int.Parse(originalBarcode.AsSpan(7, 5));

            return new BarcodeLookupInfo(
                originalBarcode,
                originalBarcode[..7],
                true,
                quantityValue / 1000d,
                "KG",
                isCheckDigitValid);
        }

        return new BarcodeLookupInfo(
            originalBarcode,
            originalBarcode,
            false,
            null,
            null,
            isCheckDigitValid);
    }

    private static bool IsVariableWeightBarcode(string value) =>
        IsEan13Candidate(value) &&
        (value.StartsWith("27", StringComparison.Ordinal) ||
         value.StartsWith("29", StringComparison.Ordinal));

    private static bool IsEan13Candidate(string value) =>
        value.Length == 13 && value.All(char.IsDigit);

    private static bool IsValidEan13(string value)
    {
        var sum = 0;

        for (var index = 0; index < 12; index++)
        {
            var digit = value[index] - '0';
            sum += index % 2 == 0 ? digit : digit * 3;
        }

        var expectedCheckDigit = (10 - sum % 10) % 10;
        return expectedCheckDigit == value[12] - '0';
    }
}

public sealed record BarcodeLookupInfo(
    string OriginalBarcode,
    string LookupBarcode,
    bool IsVariableWeightBarcode,
    double? EmbeddedQuantity,
    string? EmbeddedQuantityUnit,
    bool? IsCheckDigitValid);
