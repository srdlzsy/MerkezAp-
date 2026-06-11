using System.Globalization;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

internal static class InventoryCountMikroApiPayloadFactory
{
    internal static InventoryCountMikroApiPayload Create(
        int warehouseNo,
        DateTime documentDate,
        string name,
        IReadOnlyCollection<CreateInventoryCountLineRequest> lines,
        IReadOnlyDictionary<string, string> barcodeLookup,
        string traceKey)
    {
        var satirlar = lines
            .Select(line =>
            {
                var stockCode = NormalizeText(line.StockCode, 25);
                var barcode = ResolveBarcode(line, stockCode, barcodeLookup);

                return new InventoryCountMikroApiLine(
                    documentDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                    warehouseNo,
                    stockCode,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    line.Quantity,
                    0d,
                    0d,
                    0d,
                    0d,
                    line.UnitPointer,
                    barcode,
                    0,
                    0,
                    name,
                    0,
                    traceKey);
            })
            .ToArray();

        return new InventoryCountMikroApiPayload(
            [
                new InventoryCountMikroApiDocument(satirlar)
            ]);
    }

    private static string ResolveBarcode(
        CreateInventoryCountLineRequest line,
        string stockCode,
        IReadOnlyDictionary<string, string> barcodeLookup)
    {
        var barcode = NormalizeText(line.Barcode, 50);
        if (!string.IsNullOrEmpty(barcode))
        {
            return barcode;
        }

        return barcodeLookup.TryGetValue(stockCode, out var resolvedBarcode)
            ? NormalizeText(resolvedBarcode, 50)
            : string.Empty;
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

internal sealed record InventoryCountMikroApiPayload(
    IReadOnlyCollection<InventoryCountMikroApiDocument> evraklar);

internal sealed record InventoryCountMikroApiDocument(
    IReadOnlyCollection<InventoryCountMikroApiLine> satirlar);

internal sealed record InventoryCountMikroApiLine(
    string sym_tarihi,
    int sym_depono,
    string sym_Stokkodu,
    string sym_reyonkodu,
    string sym_koridorkodu,
    string sym_rafkodu,
    double sym_miktar1,
    double sym_miktar2,
    double sym_miktar3,
    double sym_miktar4,
    double sym_miktar5,
    int sym_birim_pntr,
    string sym_barkod,
    int sym_renkno,
    int sym_bedenno,
    string sym_parti_kodu,
    int sym_lot_no,
    string sym_serino);
