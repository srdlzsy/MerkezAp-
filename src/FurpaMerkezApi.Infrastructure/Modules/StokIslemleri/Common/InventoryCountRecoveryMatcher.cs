using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

internal static class InventoryCountRecoveryMatcher
{
    internal static StockMovementMatch Match(
        CreateInventoryCountRequest request,
        string name,
        IReadOnlyDictionary<string, string> barcodeLookup,
        IReadOnlyCollection<SAYIM_SONUCLARI> rows)
    {
        var expected = request.Lines.ToArray();
        if (rows.Count == 0) return StockMovementMatch.Missing;
        if (rows.Count > expected.Length ||
            rows.Any(row => !row.sym_satirno.HasValue) ||
            rows.Select(row => row.sym_satirno).Distinct().Count() != rows.Count ||
            rows.Select(row => (row.sym_depono, row.sym_evrakno)).Distinct().Count() != 1)
            return StockMovementMatch.Mismatch;

        var traceKey = request.ClientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value)
            : string.Empty;

        foreach (var row in rows)
        {
            var rowNo = row.sym_satirno!.Value;
            if (rowNo < 0 || rowNo >= expected.Length) return StockMovementMatch.Mismatch;

            var line = expected[rowNo];
            var stockCode = Normalize(line.StockCode);
            var barcode = Normalize(line.Barcode);
            if (barcode.Length == 0)
            {
                barcodeLookup.TryGetValue(stockCode, out barcode);
                barcode ??= string.Empty;
            }

            if (row.sym_iptal == true ||
                row.sym_depono != request.WarehouseNo ||
                !TextEquals(row.sym_Stokkodu, stockCode) ||
                !NearlyEquals(row.sym_miktar1, line.Quantity) ||
                row.sym_birim_pntr != Convert.ToByte(line.UnitPointer) ||
                !TextEquals(row.sym_barkod, barcode) ||
                !TextEquals(row.sym_parti_kodu, name) ||
                (request.ClientRequestId.HasValue && !TextEquals(row.sym_serino, traceKey)))
                return StockMovementMatch.Mismatch;
        }

        return rows.Count == expected.Length
            ? StockMovementMatch.Complete
            : StockMovementMatch.Incomplete;
    }

    private static bool NearlyEquals(double? actual, double expected) =>
        actual.HasValue && double.IsFinite(actual.Value) && Math.Abs(actual.Value - expected) <= 0.0001d;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(Normalize(actual), Normalize(expected), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
}
