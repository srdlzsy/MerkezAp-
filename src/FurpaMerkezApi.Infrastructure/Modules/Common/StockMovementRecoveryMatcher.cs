using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.Common;

internal enum StockMovementMatch { Missing, Incomplete, Complete, Mismatch }

internal static class StockMovementRecoveryMatcher
{
    internal static StockMovementMatch Company(CreateCompanyMovementRequest request, byte genre, byte returnType,
        IReadOnlyCollection<STOK_HAREKETLERI> rows) => Match(
        request.WarehouseNo, request.ClientRequestId, 1, genre, returnType, request.CustomerCode,
        request.Lines.Select(line => new ExpectedLine(line.StockCode, line.Quantity, line.UnitPointer,
            1, line.PartyCode, line.LotNo, line.ProjectCode, line.Quantity * line.UnitPrice,
            line.CustomerResponsibilityCenter, line.ProductResponsibilityCenter, line.OrderLineGuid)).ToArray(), rows);

    internal static StockMovementMatch Receipt(CreateStockReceiptRequest request, byte genre,
        IReadOnlyCollection<STOK_HAREKETLERI> rows) => Match(
        request.WarehouseNo, request.ClientRequestId, 0, genre, 0, string.Empty,
        request.Lines.Select(line => new ExpectedLine(line.StockCode, line.Quantity, line.UnitPointer,
            1, line.PartyCode, line.LotNo, line.ProjectCode)).ToArray(), rows);

    internal static StockMovementMatch Virman(CreateVirmanRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows) => Match(
        request.WarehouseNo, request.ClientRequestId, 6, 3, 0, string.Empty,
        request.Lines.SelectMany(line => (line.MovementType == 2 ? new byte[] { 1, 0 } : [line.MovementType])
            .Select(type => new ExpectedLine(line.StockCode, line.Quantity, line.UnitPointer,
                type, line.PartyCode, line.LotNo, line.ProjectCode))).ToArray(), rows);

    internal static StockMovementMatch Snapshot(
        IReadOnlyCollection<STOK_HAREKETLERI> expectedRows,
        IReadOnlyCollection<STOK_HAREKETLERI> actualRows)
    {
        if (actualRows.Count == 0) return StockMovementMatch.Missing;
        if (actualRows.Count > expectedRows.Count ||
            actualRows.Any(row => !row.sth_satirno.HasValue) ||
            actualRows.Select(row => row.sth_satirno).Distinct().Count() != actualRows.Count ||
            actualRows.Select(row => (row.sth_evrakno_seri, row.sth_evrakno_sira)).Distinct().Count() != 1)
            return StockMovementMatch.Mismatch;

        var expectedByRowNo = expectedRows
            .Where(row => row.sth_satirno.HasValue)
            .ToDictionary(row => row.sth_satirno!.Value);
        if (expectedByRowNo.Count != expectedRows.Count)
            return StockMovementMatch.Mismatch;

        foreach (var actual in actualRows)
        {
            if (!expectedByRowNo.TryGetValue(actual.sth_satirno!.Value, out var expected) ||
                actual.sth_iptal != expected.sth_iptal ||
                actual.sth_evraktip != expected.sth_evraktip ||
                actual.sth_cins != expected.sth_cins ||
                actual.sth_normal_iade != expected.sth_normal_iade ||
                actual.sth_tip != expected.sth_tip ||
                actual.sth_giris_depo_no != expected.sth_giris_depo_no ||
                actual.sth_cikis_depo_no != expected.sth_cikis_depo_no ||
                !TextEquals(actual.sth_cari_kodu, expected.sth_cari_kodu) ||
                !TextEquals(actual.sth_stok_kod, expected.sth_stok_kod) ||
                !NearlyEquals(actual.sth_miktar, expected.sth_miktar ?? 0d) ||
                actual.sth_birim_pntr != expected.sth_birim_pntr ||
                !NearlyEquals(actual.sth_tutar, expected.sth_tutar ?? 0d) ||
                !TextEquals(actual.sth_parti_kodu, expected.sth_parti_kodu) ||
                (actual.sth_lot_no ?? 0) != (expected.sth_lot_no ?? 0) ||
                !TextEquals(actual.sth_proje_kodu, expected.sth_proje_kodu) ||
                !TextEquals(actual.sth_cari_srm_merkezi, expected.sth_cari_srm_merkezi) ||
                !TextEquals(actual.sth_stok_srm_merkezi, expected.sth_stok_srm_merkezi) ||
                (actual.sth_sip_uid ?? Guid.Empty) != (expected.sth_sip_uid ?? Guid.Empty) ||
                !TextEquals(actual.sth_aciklama, expected.sth_aciklama) ||
                !TextEquals(actual.sth_eticaret_kanal_kodu, expected.sth_eticaret_kanal_kodu))
                return StockMovementMatch.Mismatch;
        }

        return actualRows.Count == expectedRows.Count
            ? StockMovementMatch.Complete
            : StockMovementMatch.Incomplete;
    }

    private static StockMovementMatch Match(int warehouseNo, Guid? requestId, byte documentType,
        byte genre, byte returnType, string customerCode, IReadOnlyList<ExpectedLine> expected,
        IReadOnlyCollection<STOK_HAREKETLERI> rows)
    {
        if (rows.Count == 0) return StockMovementMatch.Missing;
        if (rows.Count > expected.Count || rows.Any(row => !row.sth_satirno.HasValue) ||
            rows.Select(row => row.sth_satirno).Distinct().Count() != rows.Count ||
            rows.Select(row => (row.sth_evrakno_seri, row.sth_evrakno_sira)).Distinct().Count() != 1)
            return StockMovementMatch.Mismatch;

        var trace = requestId.HasValue ? MobileOfflineSyncService.ToTraceKey(requestId.Value) : null;
        foreach (var row in rows)
        {
            var index = row.sth_satirno!.Value;
            if (index < 0 || index >= expected.Count) return StockMovementMatch.Mismatch;
            var line = expected[index];
            if (row.sth_iptal == true || row.sth_evraktip != documentType || row.sth_cins != genre ||
                row.sth_normal_iade != returnType || row.sth_tip != line.Type ||
                row.sth_cikis_depo_no != warehouseNo || !TextEquals(row.sth_cari_kodu, customerCode) ||
                !TextEquals(row.sth_stok_kod, line.StockCode) || !NearlyEquals(row.sth_miktar, line.Quantity) ||
                row.sth_birim_pntr != line.UnitPointer || !TextEquals(row.sth_parti_kodu, line.PartyCode) ||
                (row.sth_lot_no ?? 0) != line.LotNo || !TextEquals(row.sth_proje_kodu, line.ProjectCode) ||
                (line.Amount.HasValue && !NearlyEquals(row.sth_tutar, line.Amount.Value)) ||
                !TextEquals(row.sth_cari_srm_merkezi, line.CustomerCenter) ||
                !TextEquals(row.sth_stok_srm_merkezi, line.StockCenter) ||
                (row.sth_sip_uid ?? Guid.Empty) != (line.OrderGuid ?? Guid.Empty) ||
                (trace is not null && !string.Equals(row.sth_eticaret_kanal_kodu, trace, StringComparison.Ordinal)) ||
                (documentType == 6 && row.sth_giris_depo_no != warehouseNo))
                return StockMovementMatch.Mismatch;
        }

        return rows.Count == expected.Count ? StockMovementMatch.Complete : StockMovementMatch.Incomplete;
    }

    internal static bool IsCompleteOrThrow(StockMovementMatch match, bool waitForIncomplete = false)
    {
        if (match == StockMovementMatch.Mismatch) throw ContentMismatch();
        if (match == StockMovementMatch.Incomplete && !waitForIncomplete) throw OutcomeUnconfirmed();
        return match == StockMovementMatch.Complete;
    }

    internal static OperationConflictException ContentMismatch() => new(
        OperationConflictErrorCodes.MikroDocumentContentMismatch,
        "The existing Mikro document does not match the requested document content. Manual review is required; do not retry with a new clientRequestId.", false);

    internal static OperationConflictException OutcomeUnconfirmed() => new(
        OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
        "Mikro API write outcome could not be confirmed. Keep the same payload and clientRequestId; only readback may be retried.", true);

    private static bool NearlyEquals(double? actual, double expected) =>
        actual.HasValue && double.IsFinite(actual.Value) && Math.Abs(actual.Value - expected) <= 0.0001d;

    private static bool TextEquals(string? actual, string? expected) =>
        string.Equals(actual?.Trim() ?? string.Empty, expected?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    private sealed record ExpectedLine(string StockCode, double Quantity, int UnitPointer, byte Type,
        string? PartyCode, int LotNo, string? ProjectCode, double? Amount = null,
        string? CustomerCenter = null, string? StockCenter = null, Guid? OrderGuid = null);
}
