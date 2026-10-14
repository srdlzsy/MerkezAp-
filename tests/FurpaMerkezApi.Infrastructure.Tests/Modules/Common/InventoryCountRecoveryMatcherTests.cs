using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.Common;

public sealed class InventoryCountRecoveryMatcherTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly CreateInventoryCountRequest Request = new(
        56,
        Guid.NewGuid(),
        RequestId,
        "Sayim",
        DateTime.Today,
        [new("A", 2, "111", 1), new("B", 3, null, 2)]);

    [Fact]
    public void Match_DistinguishesMissingIncompleteAndCompleteRows()
    {
        var rows = new[] { Row(0), Row(1) };
        var barcodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["B"] = "222" };

        Assert.Equal(StockMovementMatch.Missing, InventoryCountRecoveryMatcher.Match(Request, "Sayim", barcodes, []));
        Assert.Equal(StockMovementMatch.Incomplete, InventoryCountRecoveryMatcher.Match(Request, "Sayim", barcodes, [rows[0]]));
        Assert.Equal(StockMovementMatch.Complete, InventoryCountRecoveryMatcher.Match(Request, "Sayim", barcodes, rows));
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("quantity")]
    [InlineData("unit")]
    [InlineData("barcode")]
    [InlineData("trace")]
    [InlineData("row")]
    public void Match_RejectsChangedContent(string field)
    {
        var rows = new[] { Row(0), Row(1) };
        var barcodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["B"] = "222" };
        switch (field)
        {
            case "stock": rows[0].sym_Stokkodu = "OTHER"; break;
            case "quantity": rows[0].sym_miktar1 = 9; break;
            case "unit": rows[0].sym_birim_pntr = 3; break;
            case "barcode": rows[0].sym_barkod = "999"; break;
            case "trace": rows[0].sym_serino = "OTHER"; break;
            case "row": rows[1].sym_satirno = 0; break;
        }

        Assert.Equal(StockMovementMatch.Mismatch, InventoryCountRecoveryMatcher.Match(Request, "Sayim", barcodes, rows));
    }

    private static SAYIM_SONUCLARI Row(int rowNo) => new()
    {
        sym_Guid = Guid.NewGuid(),
        sym_iptal = false,
        sym_depono = 56,
        sym_evrakno = 42,
        sym_satirno = rowNo,
        sym_Stokkodu = rowNo == 0 ? "A" : "B",
        sym_miktar1 = rowNo == 0 ? 2 : 3,
        sym_birim_pntr = rowNo == 0 ? (byte)1 : (byte)2,
        sym_barkod = rowNo == 0 ? "111" : "222",
        sym_parti_kodu = "Sayim",
        sym_serino = MobileOfflineSyncService.ToTraceKey(RequestId)
    };
}
