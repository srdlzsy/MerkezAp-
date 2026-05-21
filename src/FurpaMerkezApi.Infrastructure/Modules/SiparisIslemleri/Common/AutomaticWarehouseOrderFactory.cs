using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

internal static class AutomaticWarehouseOrderFactory
{
    private const short OrderFileId = 86;
    private const short MovementExtraFileId = 590;
    private const short MikroUserNo = 39;
    private static readonly DateTime OrderEmptyDate = new(1900, 1, 1);
    private static readonly DateTime MovementExtraEmptyDate = new(1899, 12, 30);

    public static DEPOLAR_ARASI_SIPARISLER CreateOrderLine(
        int inWarehouseNo,
        int outWarehouseNo,
        DateTime orderDate,
        DateTime deliveryDate,
        string documentSerie,
        int documentOrderNo,
        int rowNo,
        DateTime now,
        string stockCode,
        double quantity,
        double unitPrice,
        int unitPointer,
        string? description,
        string? projectCode,
        string? responsibilityCenter)
    {
        var amount = quantity * unitPrice;

        return new DEPOLAR_ARASI_SIPARISLER
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_DBCno = 0,
            ssip_SpecRECno = 0,
            ssip_iptal = false,
            ssip_fileid = OrderFileId,
            ssip_hidden = false,
            ssip_kilitli = false,
            ssip_degisti = false,
            ssip_checksum = 0,
            ssip_create_user = MikroUserNo,
            ssip_create_date = now,
            ssip_lastup_user = MikroUserNo,
            ssip_lastup_date = now,
            ssip_special1 = "0",
            ssip_special2 = string.Empty,
            ssip_special3 = string.Empty,
            ssip_firmano = 0,
            ssip_subeno = 0,
            ssip_tarih = orderDate,
            ssip_teslim_tarih = deliveryDate,
            ssip_evrakno_seri = documentSerie,
            ssip_evrakno_sira = documentOrderNo,
            ssip_satirno = rowNo,
            ssip_belgeno = string.Empty,
            ssip_belge_tarih = orderDate,
            ssip_stok_kod = stockCode.Trim(),
            ssip_miktar = quantity,
            ssip_b_fiyat = unitPrice,
            ssip_tutar = amount,
            ssip_teslim_miktar = 0d,
            ssip_aciklama = NormalizeText(description),
            ssip_girdepo = inWarehouseNo,
            ssip_cikdepo = outWarehouseNo,
            ssip_kapat_fl = false,
            ssip_birim_pntr = Convert.ToByte(unitPointer),
            ssip_fiyat_liste_no = 0,
            ssip_stal_uid = Guid.Empty,
            ssip_paket_kod = string.Empty,
            ssip_kapatmanedenkod = string.Empty,
            ssip_projekodu = NormalizeText(projectCode),
            ssip_sormerkezi = NormalizeText(responsibilityCenter),
            ssip_gecerlilik_tarihi = OrderEmptyDate,
            ssip_rezervasyon_miktari = 0d,
            ssip_rezerveden_teslim_edilen = 0d
        };
    }

    public static STOK_HAREKETLERI_EK CreateMovementExtra(
        Guid movementGuid,
        Guid warehouseOrderLineGuid,
        DateTime now) =>
        new()
        {
            sthek_Guid = Guid.NewGuid(),
            sthek_DBCno = 0,
            sthek_SpecRECno = 0,
            sthek_iptal = false,
            sthek_fileid = MovementExtraFileId,
            sthek_hidden = false,
            sthek_kilitli = false,
            sthek_degisti = false,
            sthek_checksum = 0,
            sthek_create_user = MikroUserNo,
            sthek_create_date = now,
            sthek_lastup_user = MikroUserNo,
            sthek_lastup_date = now,
            sthek_special1 = string.Empty,
            sthek_special2 = string.Empty,
            sthek_special3 = string.Empty,
            sthek_related_uid = movementGuid,
            sth_subesip_uid = warehouseOrderLineGuid,
            sth_bkm_uid = Guid.Empty,
            sth_karsikons_uid = Guid.Empty,
            sth_rez_uid = Guid.Empty,
            sth_optamam_uid = Guid.Empty,
            sth_iadeTlp_uid = Guid.Empty,
            sth_HalSatis_uid = Guid.Empty,
            sth_ciroprim_uid = Guid.Empty,
            sth_iade_evrak_seri = string.Empty,
            sth_iade_evrak_sira = 0,
            sth_yat_tes_kodu = string.Empty,
            sth_ihracat_kredi_kodu = string.Empty,
            sth_diib_belge_no = string.Empty,
            sth_diib_satir_no = 0,
            sth_mensey_ulke_tipi = 0,
            sth_mensey_ulke_kodu = string.Empty,
            sth_halrehmiktari = 0d,
            sth_halrehfiyati = 0d,
            sth_halsandikmiktari = 0d,
            sth_halsandikfiyati = 0d,
            sth_halsandikkdvtutari = 0d,
            sth_HalKomisyonuKdv = 0d,
            sth_HalRusum = 0d,
            sth_satistipi = 0,
            sth_vardiya_tarihi = MovementExtraEmptyDate,
            sth_vardiya_no = 0,
            sth_direkt_iscilik_1 = 0d,
            sth_direkt_iscilik_2 = 0d,
            sth_direkt_iscilik_3 = 0d,
            sth_direkt_iscilik_4 = 0d,
            sth_direkt_iscilik_5 = 0d,
            sth_genel_uretim_1 = 0d,
            sth_genel_uretim_2 = 0d,
            sth_genel_uretim_3 = 0d,
            sth_genel_uretim_4 = 0d,
            sth_genel_uretim_5 = 0d,
            sth_fis_tarihi2 = MovementExtraEmptyDate,
            sth_fis_sirano2 = 0,
            sth_fiyfark_esas_evrak_seri = string.Empty,
            sth_fiyfark_esas_evrak_sira = 0,
            sth_fiyfark_esas_satir_no = 0,
            sth_istisna = string.Empty,
            sth_otv_tevkifat_turu = 0,
            sth_otv_tevkifat_tutari = 0d,
            sth_servishar_uid = Guid.Empty,
            sth_bakimsarf_uid = Guid.Empty,
            sth_utsbildirimturu = 0,
            sth_utshekzayiatturu = 0,
            sth_utsimhabertarafgerekcesi = 0,
            sth_utsdigergerekceaciklamasi = string.Empty,
            sth_hizlisatis_promosyonkodu_1 = string.Empty,
            sth_hizlisatis_promosyonkodu_2 = string.Empty,
            sth_hizlisatis_promosyonkodu_3 = string.Empty,
            sth_hks_kunye_no = string.Empty,
            sth_hks_carikodu = string.Empty,
            sth_tevkifat_islem_turu_idx = 0,
            sth_otv_istisnakodu = string.Empty,
            sth_karsi_program_kodu = string.Empty,
            sth_sas_kalem_no = string.Empty,
            sth_yerlilik_orani = 0
        };

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
