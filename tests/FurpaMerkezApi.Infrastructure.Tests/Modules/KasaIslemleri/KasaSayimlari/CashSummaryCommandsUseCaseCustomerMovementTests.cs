using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaSayimlari;

public sealed class CashSummaryCommandsUseCaseCustomerMovementTests
{
    [Fact]
    public void CreateMovements_WritesLiveCompatiblePaymentBasedZReportMovements()
    {
        var now = new DateTime(2026, 8, 13, 16, 12, 0);
        var header = CreateHeader();
        var lines = new[]
        {
            new CashSummaryCustomerMovementLine(3, "0004", 82_751.60),
            new CashSummaryCustomerMovementLine(52, "K.0003", 2_000.00),
            new CashSummaryCustomerMovementLine(CashSummaryCustomerMovementFactory.CashPaymentTypeNo, "0002", 5_390.00)
        };

        var movements = CashSummaryCustomerMovementFactory.CreateMovements(
                header,
                lines,
                zTotalValue: 90_226.03,
                documentTotal: 90_141.60,
                customerMovementDocumentOrderNo: 306_887,
                now)
            .ToArray();

        Assert.Equal(5, movements.Length);
        Assert.All(movements, movement =>
        {
            Assert.Equal(CashSummaryCustomerMovementFactory.CustomerMovementDocumentSerie, movement.cha_evrakno_seri);
            Assert.Equal(306_887, movement.cha_evrakno_sira);
            Assert.Equal(CashSummaryCustomerMovementFactory.CustomerMovementDocumentType, movement.cha_evrak_tip);
            Assert.Equal("F172.251.6", movement.cha_aciklama);
            Assert.Equal("Z Raporu", movement.cha_diger_belge_adi);
            Assert.Equal(string.Empty, movement.cha_belge_no);
            Assert.Equal(0d, movement.cha_miktari);
            Assert.Equal(0d, movement.cha_aratoplam);
        });

        AssertMovement(movements[0], rowNo: 0, type: 0, customerGenus: 2, code: "0004", amount: 82_751.60);
        AssertMovement(movements[1], rowNo: 1, type: 0, customerGenus: 0, code: "K.0003", amount: 2_000.00);
        AssertMovement(movements[2], rowNo: 2, type: 0, customerGenus: 4, code: "0002", amount: 5_390.00);
        AssertMovement(movements[3], rowNo: 3, type: 1, customerGenus: 1, code: "5154", amount: -84.43);
        AssertMovement(movements[4], rowNo: 4, type: 1, customerGenus: 4, code: "172", amount: 90_226.03);
    }

    [Fact]
    public void CreateMovements_SkipsZeroOrEmptyAccountLines()
    {
        var movements = CashSummaryCustomerMovementFactory.CreateMovements(
                CreateHeader(),
                new[]
                {
                    new CashSummaryCustomerMovementLine(3, "0004", 0),
                    new CashSummaryCustomerMovementLine(4, string.Empty, 100)
                },
                zTotalValue: 0,
                documentTotal: 0,
                customerMovementDocumentOrderNo: 1,
                new DateTime(2026, 8, 13))
            .ToArray();

        Assert.Empty(movements);
    }

    [Fact]
    public void CreateMovements_KeepsNegativePaymentLineOnItsOwnAccount()
    {
        var movements = CashSummaryCustomerMovementFactory.CreateMovements(
                CreateHeader(),
                new[]
                {
                    new CashSummaryCustomerMovementLine(3, "0004", -10)
                },
                zTotalValue: 0,
                documentTotal: -10,
                customerMovementDocumentOrderNo: 1,
                new DateTime(2026, 8, 13))
            .ToArray();

        Assert.Equal(2, movements.Length);
        AssertMovement(movements[0], rowNo: 0, type: 0, customerGenus: 2, code: "0004", amount: -10);
        AssertMovement(movements[1], rowNo: 1, type: 1, customerGenus: 1, code: "5154", amount: -10);
    }

    [Fact]
    public void ResolveExistingZTotalValue_ReadsWarehouseCreditLine()
    {
        var movements = new[]
        {
            new CARI_HESAP_HAREKETLERI
            {
                cha_evrak_tip = CashSummaryCustomerMovementFactory.CustomerMovementDocumentType,
                cha_tip = 1,
                cha_cari_cins = 1,
                cha_kod = "172",
                cha_meblag = 12.34
            },
            new CARI_HESAP_HAREKETLERI
            {
                cha_evrak_tip = CashSummaryCustomerMovementFactory.CustomerMovementDocumentType,
                cha_tip = 1,
                cha_cari_cins = 4,
                cha_kod = "172",
                cha_meblag = 90_226.03
            }
        };

        var zTotal = CashSummaryCustomerMovementFactory.ResolveExistingZTotalValue(movements, 172);

        Assert.Equal(90_226.03, zTotal);
    }

    private static SummaryEntity CreateHeader() =>
        new()
        {
            WarehouseNo = 172,
            CashNo = 251,
            ZReportNo = 3_078,
            CashierNo = 5_154,
            ManagerNo = 5_121,
            SummaryDate = new DateTime(2026, 8, 12),
            DocumentSerie = "F172.251",
            DocumentOrderNo = 6
        };

    private static void AssertMovement(
        CARI_HESAP_HAREKETLERI movement,
        int rowNo,
        byte type,
        byte customerGenus,
        string code,
        double amount)
    {
        Assert.Equal(rowNo, movement.cha_satir_no);
        Assert.Equal(type, movement.cha_tip);
        Assert.Equal(customerGenus, movement.cha_cari_cins);
        Assert.Equal(code, movement.cha_kod);
        Assert.Equal(amount, movement.cha_meblag);
    }
}
