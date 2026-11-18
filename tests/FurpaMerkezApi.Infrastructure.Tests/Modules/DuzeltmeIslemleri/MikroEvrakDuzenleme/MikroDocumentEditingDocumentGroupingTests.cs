using FurpaMerkezApi.Infrastructure.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public sealed class MikroDocumentEditingDocumentGroupingTests
{
    [Fact]
    public void GroupCustomerMovementDocuments_SeparatesTypeKindAndReturn()
    {
        var rows = new[]
        {
            CustomerMovement(63, 5, 0, 0),
            CustomerMovement(63, 5, 0, 1),
            CustomerMovement(60, 5, 0, 0),
            CustomerMovement(63, 6, 1, 0)
        };

        var groups = MikroDocumentEditingService.GroupCustomerMovementDocuments(rows);

        Assert.Equal(3, groups.Count);
        Assert.Contains(groups, group => group.Count == 2 && group.First().cha_evrak_tip == 63);
        Assert.Contains(groups, group => group.First().cha_evrak_tip == 60);
        Assert.Contains(groups, group => group.First().cha_cinsi == 6 && group.First().cha_normal_Iade == 1);
    }

    [Fact]
    public void GroupCompanyOrderDocuments_SeparatesOrderTypeAndKind()
    {
        var rows = new[]
        {
            CompanyOrder(0, 0, 0),
            CompanyOrder(0, 0, 1),
            CompanyOrder(1, 0, 0),
            CompanyOrder(0, 2, 0)
        };

        var groups = MikroDocumentEditingService.GroupCompanyOrderDocuments(rows);

        Assert.Equal(3, groups.Count);
    }

    [Fact]
    public void GroupCompanyOrderDocuments_SeparatesSameTypeByWarehouseAndCustomer()
    {
        var first = CompanyOrder(0, 0, 0);
        var second = CompanyOrder(0, 0, 1);
        var third = CompanyOrder(0, 0, 2);
        first.sip_depono = 50;
        first.sip_musteri_kod = "CARI-1";
        second.sip_depono = 50;
        second.sip_musteri_kod = "CARI-1";
        third.sip_depono = 53;
        third.sip_musteri_kod = "CARI-2";

        var groups = MikroDocumentEditingService.GroupCompanyOrderDocuments([first, second, third]);

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, group => group.Count == 2 && group.First().sip_depono == 50);
        Assert.Contains(groups, group => group.Count == 1 && group.First().sip_depono == 53);
    }

    [Fact]
    public void GroupInventoryCountDocuments_KeepsDifferentPartiesInSameDocument()
    {
        var first = InventoryCount("PARTI-1", 0);
        var second = InventoryCount("PARTI-2", 1);

        var group = Assert.Single(MikroDocumentEditingService.GroupInventoryCountDocuments([first, second]));

        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void GroupWarehouseOrderDocuments_SeparatesSameSerieAndOrderByRoute()
    {
        var rows = new[]
        {
            WarehouseOrder(50, 120, 0),
            WarehouseOrder(50, 120, 1),
            WarehouseOrder(53, 120, 0),
            WarehouseOrder(50, 121, 0)
        };

        var groups = MikroDocumentEditingService.GroupWarehouseOrderDocuments(rows);

        Assert.Equal(3, groups.Count);
        Assert.Contains(groups, group => group.Count == 2 && group.First().ssip_cikdepo == 50 && group.First().ssip_girdepo == 120);
        Assert.Contains(groups, group => group.First().ssip_cikdepo == 53 && group.First().ssip_girdepo == 120);
        Assert.Contains(groups, group => group.First().ssip_cikdepo == 50 && group.First().ssip_girdepo == 121);
    }

    private static CARI_HESAP_HAREKETLERI CustomerMovement(
        byte documentType,
        byte movementKind,
        byte normalReturn,
        int lineNo) =>
        new()
        {
            cha_Guid = Guid.NewGuid(),
            cha_evrakno_seri = "PS100",
            cha_evrakno_sira = 100,
            cha_evrak_tip = documentType,
            cha_tip = 1,
            cha_cinsi = movementKind,
            cha_normal_Iade = normalReturn,
            cha_satir_no = lineNo,
            cha_kod = "CARI"
        };

    private static SIPARISLER CompanyOrder(byte orderType, byte orderKind, int lineNo) =>
        new()
        {
            sip_Guid = Guid.NewGuid(),
            sip_tip = orderType,
            sip_cins = orderKind,
            sip_evrakno_seri = "A",
            sip_evrakno_sira = 1979,
            sip_satirno = lineNo,
            sip_stok_kod = $"STOK-{lineNo}"
        };

    private static DEPOLAR_ARASI_SIPARISLER WarehouseOrder(
        int outputWarehouseNo,
        int inputWarehouseNo,
        int lineNo) =>
        new()
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_evrakno_seri = "G3",
            ssip_evrakno_sira = 2262,
            ssip_cikdepo = outputWarehouseNo,
            ssip_girdepo = inputWarehouseNo,
            ssip_satirno = lineNo,
            ssip_stok_kod = $"STOK-{lineNo}"
        };

    private static SAYIM_SONUCLARI InventoryCount(string partyCode, int lineNo) =>
        new()
        {
            sym_Guid = Guid.NewGuid(),
            sym_tarihi = new DateTime(2026, 11, 18),
            sym_depono = 50,
            sym_evrakno = 123,
            sym_parti_kodu = partyCode,
            sym_satirno = lineNo,
            sym_Stokkodu = $"STOK-{lineNo}"
        };
}
