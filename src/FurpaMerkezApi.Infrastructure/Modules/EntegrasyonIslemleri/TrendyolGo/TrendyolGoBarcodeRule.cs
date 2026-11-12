namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal static class TrendyolGoBarcodeRule
{
    public const string ProduceBarcodePrefix = "FURPA";
    public const int MaxScaleBarcodeLength = 8;

    public static bool IsScaleProduceBarcode(string? rawBarcode)
    {
        if (string.IsNullOrWhiteSpace(rawBarcode))
        {
            return false;
        }

        var trimmed = rawBarcode.Trim();
        return trimmed.Length <= MaxScaleBarcodeLength &&
               (trimmed.StartsWith("27", StringComparison.Ordinal) ||
                trimmed.StartsWith("29", StringComparison.Ordinal));
    }

    public static string? FormatBarcode(string? rawBarcode)
    {
        if (string.IsNullOrWhiteSpace(rawBarcode))
        {
            return null;
        }

        var trimmed = rawBarcode.Trim();
        if (trimmed.StartsWith(ProduceBarcodePrefix, StringComparison.OrdinalIgnoreCase) ||
            !IsScaleProduceBarcode(trimmed))
        {
            return trimmed;
        }

        return $"{ProduceBarcodePrefix}{trimmed}";
    }

    public static string[] ExpandLookupBarcodes(IReadOnlyCollection<string> barcodes)
    {
        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var barcode in barcodes)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                continue;
            }

            var trimmed = barcode.Trim();
            expanded.Add(trimmed);
            if (trimmed.StartsWith(ProduceBarcodePrefix, StringComparison.OrdinalIgnoreCase) &&
                trimmed.Length > ProduceBarcodePrefix.Length)
            {
                expanded.Add(trimmed[ProduceBarcodePrefix.Length..]);
            }
        }

        return expanded.ToArray();
    }

    public static string SqlBarcodeExpression(string barcodeExpression) =>
        $"""
        CASE
            WHEN UPPER(LTRIM(RTRIM({barcodeExpression}))) NOT LIKE '{ProduceBarcodePrefix}%'
                 AND LEN(LTRIM(RTRIM({barcodeExpression}))) <= {MaxScaleBarcodeLength}
                 AND LEFT(LTRIM(RTRIM({barcodeExpression})), 2) IN ('27', '29')
                THEN '{ProduceBarcodePrefix}' + LTRIM(RTRIM({barcodeExpression}))
            ELSE LTRIM(RTRIM({barcodeExpression}))
        END
        """;

    public static string SqlRankedBarcodesCte(string? extraWhereClause = null)
    {
        var extraCondition = string.IsNullOrWhiteSpace(extraWhereClause)
            ? string.Empty
            : $"{Environment.NewLine}                  AND {extraWhereClause}";

        return $"""
            RankedBarcodes AS
            (
                SELECT barcode.bar_stokkodu AS StockCode,
                       LTRIM(RTRIM(barcode.bar_kodu)) AS Barcode,
                       COALESCE(barcode.bar_birimpntr, 1) AS UnitPointer,
                       ROW_NUMBER() OVER
                       (
                           PARTITION BY barcode.bar_stokkodu
                           ORDER BY CASE
                                        WHEN EXISTS (
                                            SELECT 1
                                            FROM dbo.STOK_SATIS_FIYAT_LISTELERI AS p WITH (NOLOCK)
                                            WHERE p.sfiyat_stokkod = barcode.bar_stokkodu
                                              AND p.sfiyat_deposirano = @warehouseNo
                                              AND p.sfiyat_listesirano = @priceListNo
                                              AND p.sfiyat_odemeplan = @paymentPlanNo
                                              AND p.sfiyat_birim_pntr = COALESCE(barcode.bar_birimpntr, 1)
                                              AND COALESCE(p.sfiyat_iptal, 0) = 0
                                              AND COALESCE(p.sfiyat_fiyati, 0) > 0
                                        ) THEN 0 ELSE 1
                                    END,
                                    CASE WHEN COALESCE(barcode.bar_master, 0) = 1 THEN 0 ELSE 1 END,
                                    CASE WHEN COALESCE(barcode.bar_birimpntr, 1) = 1 THEN 0 ELSE 1 END,
                                    COALESCE(barcode.bar_lastup_date, barcode.bar_create_date) DESC,
                                    LTRIM(RTRIM(barcode.bar_kodu)) ASC
                       ) AS RowNo
                FROM dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                WHERE COALESCE(barcode.bar_iptal, 0) = 0
                  AND NULLIF(LTRIM(RTRIM(barcode.bar_kodu)), '') IS NOT NULL{extraCondition}
            )
            """;
    }
}
