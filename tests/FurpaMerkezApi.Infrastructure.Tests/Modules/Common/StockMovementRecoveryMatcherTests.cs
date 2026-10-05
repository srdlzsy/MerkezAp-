using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class StockMovementRecoveryMatcherTests
{
    internal static readonly Guid RequestId = Guid.NewGuid();
    internal static CreateCompanyMovementRequest CompanyRequest => new(56, "320.01", null, null, null, null,
        [new("A", 2, 10), new("B", 3, 10)], ClientRequestId: RequestId);

    internal static STOK_HAREKETLERI Row(int index = 0) => new()
    {
        sth_Guid = Guid.NewGuid(), sth_evrakno_seri = "F56", sth_evrakno_sira = 123,
        sth_tarih = DateTime.Today, sth_belge_tarih = DateTime.Today,
        sth_evraktip = 1, sth_cins = 1, sth_tip = 1, sth_normal_iade = 0,
        sth_cikis_depo_no = 56, sth_giris_depo_no = 0, sth_cari_kodu = "320.01",
        sth_satirno = index, sth_stok_kod = index == 0 ? "A" : "B",
        sth_miktar = index == 0 ? 2 : 3, sth_birim_pntr = 1, sth_tutar = index == 0 ? 20 : 30,
        sth_eticaret_kanal_kodu = MobileOfflineSyncService.ToTraceKey(RequestId)
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Company_RequiresAllRowsIncludingReturn(bool isReturn)
    {
        var rows = new[] { Row(), Row(1) };
        foreach (var row in rows)
        {
            row.sth_cins = isReturn ? (byte)0 : (byte)1;
            row.sth_normal_iade = isReturn ? (byte)1 : (byte)0;
        }
        var genre = isReturn ? (byte)0 : (byte)1;
        var returnType = isReturn ? (byte)1 : (byte)0;
        Assert.Equal(StockMovementMatch.Missing, StockMovementRecoveryMatcher.Company(CompanyRequest, genre, returnType, []));
        Assert.Equal(StockMovementMatch.Incomplete, StockMovementRecoveryMatcher.Company(CompanyRequest, genre, returnType, [rows[1]]));
        Assert.Equal(StockMovementMatch.Complete, StockMovementRecoveryMatcher.Company(CompanyRequest, genre, returnType, rows.Reverse().ToArray()));
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("quantity")]
    [InlineData("unit")]
    [InlineData("amount")]
    [InlineData("trace")]
    [InlineData("warehouse")]
    [InlineData("customer")]
    [InlineData("row")]
    [InlineData("cancelled")]
    [InlineData("party")]
    [InlineData("lot")]
    [InlineData("project")]
    [InlineData("order")]
    public void Company_RejectsDifferentContentWithSameCount(string field)
    {
        var rows = new[] { Row(), Row(1) };
        switch (field)
        {
            case "stock": rows[0].sth_stok_kod = "OTHER"; break;
            case "quantity": rows[0].sth_miktar = null; break;
            case "unit": rows[0].sth_birim_pntr = 2; break;
            case "amount": rows[0].sth_tutar = 1; break;
            case "trace": rows[0].sth_eticaret_kanal_kodu = ""; break;
            case "warehouse": rows[0].sth_cikis_depo_no = 57; break;
            case "customer": rows[0].sth_cari_kodu = "OTHER"; break;
            case "row": rows[1].sth_satirno = 0; break;
            case "cancelled": rows[0].sth_iptal = true; break;
            case "party": rows[0].sth_parti_kodu = "OTHER"; break;
            case "lot": rows[0].sth_lot_no = 2; break;
            case "project": rows[0].sth_proje_kodu = "OTHER"; break;
            case "order": rows[0].sth_sip_uid = Guid.NewGuid(); break;
        }
        Assert.Equal(StockMovementMatch.Mismatch, StockMovementRecoveryMatcher.Company(CompanyRequest, 1, 0, rows));
    }

    [Theory]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    public void Receipt_RequiresExactRows(byte genre)
    {
        var request = new CreateStockReceiptRequest(56, "Creator", "Acceptor", null, null, null, null,
            [new("A", 2), new("B", 3)], RequestId);
        var rows = new[] { Row(), Row(1) };
        foreach (var row in rows) { row.sth_evraktip = 0; row.sth_cins = genre; row.sth_cari_kodu = ""; }
        Assert.Equal(StockMovementMatch.Incomplete, StockMovementRecoveryMatcher.Receipt(request, genre, [rows[0]]));
        Assert.Equal(StockMovementMatch.Complete, StockMovementRecoveryMatcher.Receipt(request, genre, rows));
        rows[1].sth_birim_pntr = 2;
        Assert.Equal(StockMovementMatch.Mismatch, StockMovementRecoveryMatcher.Receipt(request, genre, rows));
    }

    [Fact]
    public void Virman_RequiresBothDirectionsAndCorrectQuantities()
    {
        var request = new CreateVirmanRequest(56, null, null, null, null, [new("A", 2, 2)], RequestId);
        var rows = new[] { Row(), Row() };
        foreach (var row in rows)
        {
            row.sth_evraktip = 6; row.sth_cins = 3; row.sth_cari_kodu = ""; row.sth_giris_depo_no = 56;
        }
        rows[1].sth_satirno = 1; rows[1].sth_tip = 0;
        Assert.Equal(StockMovementMatch.Incomplete, StockMovementRecoveryMatcher.Virman(request, [rows[0]]));
        Assert.Equal(StockMovementMatch.Complete, StockMovementRecoveryMatcher.Virman(request, rows));
        rows[1].sth_tip = 1;
        Assert.Equal(StockMovementMatch.Mismatch, StockMovementRecoveryMatcher.Virman(request, rows));
        rows[1].sth_tip = 0; rows[1].sth_miktar = 3;
        Assert.Equal(StockMovementMatch.Mismatch, StockMovementRecoveryMatcher.Virman(request, rows));
    }

    [Fact]
    public void PartialAndMismatch_HaveDifferentRetryDecisions()
    {
        var partial = Assert.Throws<OperationConflictException>(() => StockMovementRecoveryMatcher.IsCompleteOrThrow(StockMovementMatch.Incomplete));
        Assert.True(partial.Retryable);
        Assert.Equal(OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed, partial.ErrorCode);
        var mismatch = Assert.Throws<OperationConflictException>(() => StockMovementRecoveryMatcher.IsCompleteOrThrow(StockMovementMatch.Mismatch));
        Assert.False(mismatch.Retryable);
        Assert.Equal(OperationConflictErrorCodes.MikroDocumentContentMismatch, mismatch.ErrorCode);
    }
}
