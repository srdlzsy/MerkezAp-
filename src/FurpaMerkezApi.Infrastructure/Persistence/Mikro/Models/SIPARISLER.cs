using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class SIPARISLER
{
    public Guid sip_Guid { get; set; }

    public short sip_DBCno { get; set; }

    public int? sip_SpecRECno { get; set; }

    public bool? sip_iptal { get; set; }

    public short? sip_fileid { get; set; }

    public bool? sip_hidden { get; set; }

    public bool? sip_kilitli { get; set; }

    public bool? sip_degisti { get; set; }

    public int? sip_checksum { get; set; }

    public short? sip_create_user { get; set; }

    public DateTime sip_create_date { get; set; }

    public short? sip_lastup_user { get; set; }

    public DateTime? sip_lastup_date { get; set; }

    public string? sip_special1 { get; set; }

    public string? sip_special2 { get; set; }

    public string? sip_special3 { get; set; }

    public int? sip_firmano { get; set; }

    public int? sip_subeno { get; set; }

    public DateTime? sip_tarih { get; set; }

    public DateTime? sip_teslim_tarih { get; set; }

    public byte? sip_tip { get; set; }

    public byte? sip_cins { get; set; }

    public string? sip_evrakno_seri { get; set; }

    public int? sip_evrakno_sira { get; set; }

    public int? sip_satirno { get; set; }

    public string? sip_belgeno { get; set; }

    public DateTime? sip_belge_tarih { get; set; }

    public string? sip_satici_kod { get; set; }

    public string? sip_musteri_kod { get; set; }

    public string? sip_stok_kod { get; set; }

    public double? sip_b_fiyat { get; set; }

    public double? sip_miktar { get; set; }

    public byte? sip_birim_pntr { get; set; }

    public double? sip_teslim_miktar { get; set; }

    public double? sip_tutar { get; set; }

    public double? sip_iskonto_1 { get; set; }

    public double? sip_iskonto_2 { get; set; }

    public double? sip_iskonto_3 { get; set; }

    public double? sip_iskonto_4 { get; set; }

    public double? sip_iskonto_5 { get; set; }

    public double? sip_iskonto_6 { get; set; }

    public double? sip_masraf_1 { get; set; }

    public double? sip_masraf_2 { get; set; }

    public double? sip_masraf_3 { get; set; }

    public double? sip_masraf_4 { get; set; }

    public byte? sip_vergi_pntr { get; set; }

    public double? sip_vergi { get; set; }

    public byte? sip_masvergi_pntr { get; set; }

    public double? sip_masvergi { get; set; }

    public int? sip_opno { get; set; }

    public string? sip_aciklama { get; set; }

    public string? sip_aciklama2 { get; set; }

    public int? sip_depono { get; set; }

    public short? sip_OnaylayanKulNo { get; set; }

    public bool? sip_vergisiz_fl { get; set; }

    public bool? sip_kapat_fl { get; set; }

    public bool? sip_promosyon_fl { get; set; }

    public string? sip_cari_sormerk { get; set; }

    public string? sip_stok_sormerk { get; set; }

    public byte? sip_cari_grupno { get; set; }

    public byte? sip_doviz_cinsi { get; set; }

    public double? sip_doviz_kuru { get; set; }

    public double? sip_alt_doviz_kuru { get; set; }

    public int? sip_adresno { get; set; }

    public string? sip_teslimturu { get; set; }

    public bool? sip_cagrilabilir_fl { get; set; }

    public Guid? sip_prosip_uid { get; set; }

    public byte? sip_iskonto1 { get; set; }

    public byte? sip_iskonto2 { get; set; }

    public byte? sip_iskonto3 { get; set; }

    public byte? sip_iskonto4 { get; set; }

    public byte? sip_iskonto5 { get; set; }

    public byte? sip_iskonto6 { get; set; }

    public byte? sip_masraf1 { get; set; }

    public byte? sip_masraf2 { get; set; }

    public byte? sip_masraf3 { get; set; }

    public byte? sip_masraf4 { get; set; }

    public bool? sip_isk1 { get; set; }

    public bool? sip_isk2 { get; set; }

    public bool? sip_isk3 { get; set; }

    public bool? sip_isk4 { get; set; }

    public bool? sip_isk5 { get; set; }

    public bool? sip_isk6 { get; set; }

    public bool? sip_mas1 { get; set; }

    public bool? sip_mas2 { get; set; }

    public bool? sip_mas3 { get; set; }

    public bool? sip_mas4 { get; set; }

    public string? sip_Exp_Imp_Kodu { get; set; }

    public double? sip_kar_orani { get; set; }

    public byte? sip_durumu { get; set; }

    public Guid? sip_stal_uid { get; set; }

    public double? sip_planlananmiktar { get; set; }

    public Guid? sip_teklif_uid { get; set; }

    public string? sip_parti_kodu { get; set; }

    public int? sip_lot_no { get; set; }

    public string? sip_projekodu { get; set; }

    public int? sip_fiyat_liste_no { get; set; }

    public byte? sip_Otv_Pntr { get; set; }

    public double? sip_Otv_Vergi { get; set; }

    public double? sip_otvtutari { get; set; }

    public byte? sip_OtvVergisiz_Fl { get; set; }

    public string? sip_paket_kod { get; set; }

    public Guid? sip_Rez_uid { get; set; }

    public byte? sip_harekettipi { get; set; }

    public Guid? sip_yetkili_uid { get; set; }

    public string? sip_kapatmanedenkod { get; set; }

    public DateTime? sip_gecerlilik_tarihi { get; set; }

    public byte? sip_onodeme_evrak_tip { get; set; }

    public string? sip_onodeme_evrak_seri { get; set; }

    public int? sip_onodeme_evrak_sira { get; set; }

    public double? sip_rezervasyon_miktari { get; set; }

    public double? sip_rezerveden_teslim_edilen { get; set; }

    public string? sip_HareketGrupKodu1 { get; set; }

    public string? sip_HareketGrupKodu2 { get; set; }

    public string? sip_HareketGrupKodu3 { get; set; }

    public double? sip_Olcu1 { get; set; }

    public double? sip_Olcu2 { get; set; }

    public double? sip_Olcu3 { get; set; }

    public double? sip_Olcu4 { get; set; }

    public double? sip_Olcu5 { get; set; }

    public byte? sip_FormulMiktarNo { get; set; }

    public double? sip_FormulMiktar { get; set; }

    public byte? sip_satis_fiyat_doviz_cinsi { get; set; }

    public double? sip_satis_fiyat_doviz_kuru { get; set; }

    public string? sip_eticaret_kanal_kodu { get; set; }

    public byte? sip_Tevkifat_turu { get; set; }

    public byte? sip_otv_tevkifat_turu { get; set; }

    public double? sip_otv_tevkifat_tutari { get; set; }

    public bool? sip_tevkifat_sifirlandi_fl { get; set; }
}
