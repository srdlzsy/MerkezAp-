using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

internal static class CompanyReceivingRecoveryMatcher
{
    private const byte DocumentType = 13;
    private const byte MovementType = 0;
    private const byte MovementGenre = 0;
    private const byte NormalMovement = 0;
    private const double Tolerance = 0.0001d;

    internal static StockMovementMatch Match(
        CreateCompanyReceivingRequest request,
        IReadOnlyCollection<STOK_HAREKETLERI> rows)
    {
        if (rows.Count == 0) return StockMovementMatch.Missing;
        if (rows.Any(row => row.sth_iptal == true || !row.sth_satirno.HasValue) ||
            rows.Select(row => row.sth_satirno).Distinct().Count() != rows.Count ||
            rows.Select(row => (row.sth_evrakno_seri, row.sth_evrakno_sira)).Distinct().Count() != 1)
            return StockMovementMatch.Mismatch;

        var traceKey = request.ClientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value)
            : string.Empty;
        if (rows.Any(row =>
                row.sth_evraktip != DocumentType ||
                row.sth_tip != MovementType ||
                row.sth_cins != MovementGenre ||
                row.sth_normal_iade != NormalMovement ||
                row.sth_giris_depo_no != request.WarehouseNo ||
                !TextEquals(row.sth_cari_kodu, request.CustomerCode) ||
                (request.ClientRequestId.HasValue && !TextEquals(row.sth_eticaret_kanal_kodu, traceKey))))
            return StockMovementMatch.Mismatch;

        var expected = request.Lines
            .GroupBy(line => Key(
                line.StockCode,
                line.UnitPointer,
                line.PartyCode,
                line.LotNo,
                line.ProjectCode,
                line.CustomerResponsibilityCenter,
                line.ProductResponsibilityCenter))
            .ToDictionary(
                group => group.Key,
                group => new Totals(
                    group.Sum(line => line.DispatchQuantity ?? line.Quantity),
                    group.Sum(line => (line.DispatchQuantity ?? line.Quantity) * line.UnitPrice)));
        var actual = rows
            .GroupBy(row => Key(
                row.sth_stok_kod,
                row.sth_birim_pntr ?? 0,
                row.sth_parti_kodu,
                row.sth_lot_no ?? 0,
                row.sth_proje_kodu,
                row.sth_cari_srm_merkezi,
                row.sth_stok_srm_merkezi))
            .ToDictionary(
                group => group.Key,
                group => new Totals(
                    group.Sum(row => row.sth_miktar ?? 0d),
                    group.Sum(row => row.sth_tutar ?? 0d)));

        foreach (var (key, actualTotals) in actual)
        {
            if (!expected.TryGetValue(key, out var expectedTotals) ||
                actualTotals.Quantity > expectedTotals.Quantity + Tolerance ||
                actualTotals.Amount > expectedTotals.Amount + Tolerance)
                return StockMovementMatch.Mismatch;
        }

        return expected.All(pair =>
                actual.TryGetValue(pair.Key, out var value) &&
                NearlyEquals(value.Quantity, pair.Value.Quantity) &&
                NearlyEquals(value.Amount, pair.Value.Amount))
            ? StockMovementMatch.Complete
            : StockMovementMatch.Incomplete;
    }

    private static AggregateKey Key(
        string? stockCode,
        int unitPointer,
        string? partyCode,
        int lotNo,
        string? projectCode,
        string? customerCenter,
        string? stockCenter) => new(
        Normalize(stockCode),
        unitPointer,
        Normalize(partyCode),
        lotNo,
        Normalize(projectCode),
        Normalize(customerCenter),
        Normalize(stockCenter));

    private static bool NearlyEquals(double left, double right) => Math.Abs(left - right) <= Tolerance;
    private static bool TextEquals(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed record AggregateKey(
        string StockCode,
        int UnitPointer,
        string PartyCode,
        int LotNo,
        string ProjectCode,
        string CustomerCenter,
        string StockCenter);

    private sealed record Totals(double Quantity, double Amount);
}
