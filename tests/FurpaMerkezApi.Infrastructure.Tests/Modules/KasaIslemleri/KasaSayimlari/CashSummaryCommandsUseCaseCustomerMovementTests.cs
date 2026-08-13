using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.KasaIslemleri.KasaSayimlari;

public sealed class CashSummaryCommandsUseCaseCustomerMovementTests
{
    [Fact]
    public void CreateMovements_IncludesZReportTotalAndDifferenceMovements()
    {
        var movements = CreateMovements(
            CreateRequest(zTotalValue: 94_895.15, total: 94_981.35),
            documentTotal: 94_981.35);

        Assert.Equal(3, movements.Count);

        var mainMovement = movements.Single(item => item.cha_satir_no == CashSummaryCustomerMovementFactory.MainLineNo);
        Assert.Equal(94_981.35, mainMovement.cha_meblag);
        Assert.Equal((byte)0, mainMovement.cha_tip);
        Assert.Equal("KASA-1", mainMovement.cha_kod);
        Assert.Equal((short)51, mainMovement.cha_fileid);
        Assert.Equal((byte)60, mainMovement.cha_evrak_tip);
        Assert.Equal((byte)5, mainMovement.cha_cinsi);
        Assert.Equal((byte)3, mainMovement.cha_fatura_belge_turu);
        Assert.Equal("Z Raporu", mainMovement.cha_diger_belge_adi);
        Assert.Equal("1", mainMovement.cha_srmrkkodu);

        var differenceMovement = movements.Single(item => item.cha_satir_no == CashSummaryCustomerMovementFactory.ZDifferenceLineNo);
        Assert.Equal(86.20, differenceMovement.cha_meblag);
        Assert.Equal((byte)1, differenceMovement.cha_tip);
        Assert.Equal(CashSummaryCustomerMovementFactory.ZDifferenceAccountCode, differenceMovement.cha_kod);

        var zTotalMovement = movements.Single(item => item.cha_satir_no == CashSummaryCustomerMovementFactory.ZReportTotalLineNo);
        Assert.Equal(94_895.15, zTotalMovement.cha_meblag);
        Assert.Equal((byte)1, zTotalMovement.cha_tip);
        Assert.Equal("1", zTotalMovement.cha_kod);
    }

    [Fact]
    public void CreateMovements_SkipsZReportMovements_WhenZReportTotalIsZero()
    {
        var movements = CreateMovements(
            CreateRequest(zTotalValue: 0, total: 94_981.35),
            documentTotal: 94_981.35);

        var movement = Assert.Single(movements);
        Assert.Equal(CashSummaryCustomerMovementFactory.MainLineNo, movement.cha_satir_no);
        Assert.Equal(94_981.35, movement.cha_meblag);
    }

    private static IReadOnlyCollection<CARI_HESAP_HAREKETLERI> CreateMovements(
        CreateCashSummaryRequest request,
        double documentTotal)
    {
        return CashSummaryCustomerMovementFactory.CreateMovements(
                request,
                request.SummaryDate.Date,
                "F1.57",
                1,
                documentTotal,
                new DateTime(2026, 8, 13, 12, 0, 0)
            )
            .ToArray();
    }

    private static CreateCashSummaryRequest CreateRequest(double zTotalValue, double total) =>
        new(
            1,
            57,
            31_113_905,
            5_140,
            3_343,
            zTotalValue,
            total,
            new DateTime(2026, 8, 8),
            Array.Empty<CreateCashSummaryGiftCheckLineRequest>(),
            Array.Empty<CreateCashSummaryBanknoteLineRequest>(),
            Array.Empty<CreateCashSummaryPaymentLineRequest>(),
            Array.Empty<CreateCashSummaryStoreExpenseLineRequest>());
}
