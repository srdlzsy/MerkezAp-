using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class CompanyReceivingRecoveryMatcherTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly CreateCompanyReceivingRequest Request = new(
        56,
        Guid.NewGuid(),
        RequestId,
        "320.01",
        DateTime.Today,
        DateTime.Today,
        "F56",
        123,
        "IRS-1",
        null,
        null,
        null,
        true,
        false,
        [new("A", 5, UnitPrice: 10, UnitPointer: 1)]);

    [Fact]
    public void Match_AcceptsOrderOverflowSplitWhenAggregatesMatch()
    {
        var rows = new[] { Row(0, 3), Row(1, 2) };

        Assert.Equal(StockMovementMatch.Complete, CompanyReceivingRecoveryMatcher.Match(Request, rows));
        Assert.Equal(StockMovementMatch.Incomplete, CompanyReceivingRecoveryMatcher.Match(Request, [rows[0]]));
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("quantity")]
    [InlineData("amount")]
    [InlineData("trace")]
    [InlineData("warehouse")]
    public void Match_RejectsDifferentRecoveredContent(string field)
    {
        var rows = new[] { Row(0, 3), Row(1, 2) };
        switch (field)
        {
            case "stock": rows[1].sth_stok_kod = "OTHER"; break;
            case "quantity": rows[1].sth_miktar = 3; break;
            case "amount": rows[1].sth_tutar = 30; break;
            case "trace": rows[1].sth_eticaret_kanal_kodu = "OTHER"; break;
            case "warehouse": rows[1].sth_giris_depo_no = 57; break;
        }

        Assert.Equal(StockMovementMatch.Mismatch, CompanyReceivingRecoveryMatcher.Match(Request, rows));
    }

    private static STOK_HAREKETLERI Row(int rowNo, double quantity) => new()
    {
        sth_Guid = Guid.NewGuid(),
        sth_iptal = false,
        sth_evraktip = 13,
        sth_tip = 0,
        sth_cins = 0,
        sth_normal_iade = 0,
        sth_evrakno_seri = "F56",
        sth_evrakno_sira = 123,
        sth_satirno = rowNo,
        sth_giris_depo_no = 56,
        sth_cikis_depo_no = 0,
        sth_cari_kodu = "320.01",
        sth_stok_kod = "A",
        sth_miktar = quantity,
        sth_birim_pntr = 1,
        sth_tutar = quantity * 10,
        sth_eticaret_kanal_kodu = MobileOfflineSyncService.ToTraceKey(RequestId)
    };
}
