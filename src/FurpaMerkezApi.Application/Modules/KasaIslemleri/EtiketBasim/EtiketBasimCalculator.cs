using System.Globalization;

namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBasim;

public static class EtiketBasimCalculator
{
    private const string CanonicalRehinli = "REH\u0130NL\u0130";
    private const string CanonicalRehinsiz = "REH\u0130NS\u0130Z";
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static EtiketBasimCalculationDto Calculate(EtiketBasimCalculationRequest request)
    {
        if (request.GrossWeight <= 0)
        {
            throw new ArgumentException("Gross weight must be greater than zero.", nameof(request.GrossWeight));
        }

        if (request.CaseTare < 0)
        {
            throw new ArgumentException("Case tare cannot be negative.", nameof(request.CaseTare));
        }

        var caseCount = request.CaseCount.GetValueOrDefault(1);
        if (caseCount <= 0)
        {
            throw new ArgumentException("Case count must be greater than zero.", nameof(request.CaseCount));
        }

        var palletTare = request.PalletTare.GetValueOrDefault();
        if (palletTare < 0)
        {
            throw new ArgumentException("Pallet tare cannot be negative.", nameof(request.PalletTare));
        }

        var caseTotalTare = request.CaseTare * caseCount;
        var netReceivedWeight = request.GrossWeight - caseTotalTare - palletTare;
        if (netReceivedWeight <= 0)
        {
            throw new ArgumentException("Net received weight must be greater than zero.");
        }

        var averageCaseWeight = netReceivedWeight / caseCount;
        if (averageCaseWeight > 99m)
        {
            throw new ArgumentException("Average case weight cannot be greater than 99 kg.");
        }

        var labelBarcodeRaw = BuildLabelBarcode(request.StockBarcode, averageCaseWeight);
        var labelBarcode = BuildPrintableLabelBarcode(labelBarcodeRaw);
        return new EtiketBasimCalculationDto(
            Round(caseTotalTare),
            Round(netReceivedWeight),
            Round(averageCaseWeight),
            labelBarcodeRaw,
            labelBarcode,
            ResolveBarcodeSymbology(labelBarcode));
    }

    public static string NormalizeCaseType(string? caseType)
    {
        var normalized = NormalizeText(caseType);
        if (normalized is null)
        {
            throw new ArgumentException("Case type is required.", nameof(caseType));
        }

        var upper = normalized.ToUpper(TurkishCulture);
        var asciiUpper = upper.Replace("\u0130", "I", StringComparison.Ordinal);
        return asciiUpper switch
        {
            "REHINLI" => CanonicalRehinli,
            "REHINSIZ" => CanonicalRehinsiz,
            _ => throw new ArgumentException("Case type must be REHINLI or REHINSIZ.", nameof(caseType))
        };
    }

    public static string ResolveBarcodeSymbology(string? barcode) =>
        NormalizeText(barcode)?.Length switch
        {
            12 or 13 => "EAN13",
            8 => "EAN8",
            _ => "Code128"
        };

    public static string? BuildLabelBarcode(string? stockBarcode, decimal averageCaseWeight)
    {
        var normalizedStockBarcode = NormalizeText(stockBarcode);
        if (normalizedStockBarcode is null)
        {
            return null;
        }

        var roundedAverage = Round(averageCaseWeight);
        var averageText = roundedAverage.ToString("0.00", TurkishCulture);
        var averageDigits = averageText
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);

        return averageText.Length < 5
            ? normalizedStockBarcode + "0" + averageDigits + "0"
            : normalizedStockBarcode + averageDigits + "00";
    }

    public static string? BuildPrintableLabelBarcode(string? labelBarcodeRaw)
    {
        var normalized = NormalizeText(labelBarcodeRaw);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length == 12 && IsAllDigits(normalized))
        {
            return normalized + CalculateEan13CheckDigit(normalized);
        }

        if (normalized.Length == 13 && IsAllDigits(normalized))
        {
            var firstTwelveDigits = normalized[..12];
            return firstTwelveDigits + CalculateEan13CheckDigit(firstTwelveDigits);
        }

        return normalized;
    }

    public static char CalculateEan13CheckDigit(string firstTwelveDigits)
    {
        if (firstTwelveDigits.Length != 12 || !IsAllDigits(firstTwelveDigits))
        {
            throw new ArgumentException("EAN13 check digit requires exactly 12 numeric digits.", nameof(firstTwelveDigits));
        }

        var sum = 0;
        for (var index = 0; index < firstTwelveDigits.Length; index++)
        {
            var digit = firstTwelveDigits[index] - '0';
            sum += index % 2 == 0 ? digit : digit * 3;
        }

        var checkDigit = (10 - (sum % 10)) % 10;
        return (char)('0' + checkDigit);
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool IsAllDigits(string value)
    {
        foreach (var character in value)
        {
            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}