using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

public sealed class CompanyReceivingIrsaliyeMikroApiPayloadFactoryTests
{
    [Fact]
    public void CalculateTaxAmount_ReturnsRoundedTaxForPositiveAmount()
    {
        var taxAmount = CreateCompanyReceivingUseCase.CalculateTaxAmount(11935d, 1m);

        Assert.Equal(119.35d, taxAmount);
    }

    [Fact]
    public void CalculateTaxAmount_ReturnsZeroWhenAmountIsZero()
    {
        var taxAmount = CreateCompanyReceivingUseCase.CalculateTaxAmount(0d, 20m);

        Assert.Equal(0d, taxAmount);
    }

    [Fact]
    public void Create_MapsOrderGuidToSipUid()
    {
        var orderGuid = Guid.Parse("0f4db720-3374-4f80-ae21-6f7d2edec8b1");
        var payload = CompanyReceivingIrsaliyeMikroApiPayloadFactory.Create(
            [CreateMovement(orderGuid)],
            "test");

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(orderGuid.ToString(), line.sth_sip_uid);
    }

    [Fact]
    public void Create_LeavesSipUidEmptyWhenMovementIsNotLinked()
    {
        var payload = CompanyReceivingIrsaliyeMikroApiPayloadFactory.Create(
            [CreateMovement(Guid.Empty)],
            "test");

        var line = payload.evraklar.Single().satirlar.Single();

        Assert.Equal(string.Empty, line.sth_sip_uid);
    }

    private static STOK_HAREKETLERI CreateMovement(Guid orderGuid) =>
        new()
        {
            sth_tarih = new DateTime(2026, 7, 29),
            sth_tip = 0,
            sth_cins = 0,
            sth_normal_iade = 0,
            sth_evraktip = 13,
            sth_evrakno_seri = "FMK1",
            sth_evrakno_sira = 1,
            sth_satirno = 0,
            sth_belge_no = "FMK1000000001",
            sth_belge_tarih = new DateTime(2026, 7, 29),
            sth_stok_kod = "01",
            sth_cari_cinsi = 0,
            sth_cari_kodu = "32000001",
            sth_miktar = 10d,
            sth_birim_pntr = 1,
            sth_tutar = 100d,
            sth_isk_mas1 = 0,
            sth_isk_mas2 = 1,
            sth_giris_depo_no = 1,
            sth_cikis_depo_no = 1,
            sth_sip_uid = orderGuid
        };
}
