using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class STOK_HAREKETLERI_EK
{
    public Guid sthek_Guid { get; set; }

    public short sthek_DBCno { get; set; }

    public int? sthek_SpecRECno { get; set; }

    public bool? sthek_iptal { get; set; }

    public short? sthek_fileid { get; set; }

    public bool? sthek_hidden { get; set; }

    public bool? sthek_kilitli { get; set; }

    public bool? sthek_degisti { get; set; }

    public int? sthek_checksum { get; set; }

    public short? sthek_create_user { get; set; }

    public DateTime sthek_create_date { get; set; }

    public short? sthek_lastup_user { get; set; }

    public DateTime? sthek_lastup_date { get; set; }

    public string? sthek_special1 { get; set; }

    public string? sthek_special2 { get; set; }

    public string? sthek_special3 { get; set; }

    public Guid? sthek_related_uid { get; set; }

    public Guid? sth_subesip_uid { get; set; }

    public Guid? sth_bkm_uid { get; set; }

    public Guid? sth_karsikons_uid { get; set; }

    public Guid? sth_rez_uid { get; set; }

    public Guid? sth_optamam_uid { get; set; }

    public Guid? sth_iadeTlp_uid { get; set; }

    public Guid? sth_HalSatis_uid { get; set; }

    public Guid? sth_ciroprim_uid { get; set; }

    public string? sth_iade_evrak_seri { get; set; }

    public int? sth_iade_evrak_sira { get; set; }

    public string? sth_yat_tes_kodu { get; set; }

    public string? sth_ihracat_kredi_kodu { get; set; }

    public string? sth_diib_belge_no { get; set; }

    public byte? sth_diib_satir_no { get; set; }

    public byte? sth_mensey_ulke_tipi { get; set; }

    public string? sth_mensey_ulke_kodu { get; set; }

    public double? sth_halrehmiktari { get; set; }

    public double? sth_halrehfiyati { get; set; }

    public double? sth_halsandikmiktari { get; set; }

    public double? sth_halsandikfiyati { get; set; }

    public double? sth_halsandikkdvtutari { get; set; }

    public double? sth_HalKomisyonuKdv { get; set; }

    public double? sth_HalRusum { get; set; }

    public byte? sth_satistipi { get; set; }

    public DateTime? sth_vardiya_tarihi { get; set; }

    public byte? sth_vardiya_no { get; set; }

    public double? sth_direkt_iscilik_1 { get; set; }

    public double? sth_direkt_iscilik_2 { get; set; }

    public double? sth_direkt_iscilik_3 { get; set; }

    public double? sth_direkt_iscilik_4 { get; set; }

    public double? sth_direkt_iscilik_5 { get; set; }

    public double? sth_genel_uretim_1 { get; set; }

    public double? sth_genel_uretim_2 { get; set; }

    public double? sth_genel_uretim_3 { get; set; }

    public double? sth_genel_uretim_4 { get; set; }

    public double? sth_genel_uretim_5 { get; set; }

    public DateTime? sth_fis_tarihi2 { get; set; }

    public int? sth_fis_sirano2 { get; set; }

    public string? sth_fiyfark_esas_evrak_seri { get; set; }

    public int? sth_fiyfark_esas_evrak_sira { get; set; }

    public int? sth_fiyfark_esas_satir_no { get; set; }

    public string? sth_istisna { get; set; }

    public byte? sth_otv_tevkifat_turu { get; set; }

    public double? sth_otv_tevkifat_tutari { get; set; }

    public Guid? sth_servishar_uid { get; set; }

    public Guid? sth_bakimsarf_uid { get; set; }

    public byte? sth_utsbildirimturu { get; set; }

    public byte? sth_utshekzayiatturu { get; set; }

    public byte? sth_utsimhabertarafgerekcesi { get; set; }

    public string? sth_utsdigergerekceaciklamasi { get; set; }

    public string? sth_hizlisatis_promosyonkodu_1 { get; set; }

    public string? sth_hizlisatis_promosyonkodu_2 { get; set; }

    public string? sth_hizlisatis_promosyonkodu_3 { get; set; }

    public string? sth_hks_kunye_no { get; set; }

    public string? sth_hks_carikodu { get; set; }

    public byte? sth_tevkifat_islem_turu_idx { get; set; }

    public string? sth_otv_istisnakodu { get; set; }

    public string? sth_karsi_program_kodu { get; set; }

    public string? sth_sas_kalem_no { get; set; }

    public int? sth_yerlilik_orani { get; set; }
}
