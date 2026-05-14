using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class CARI_HESAP_HAREKETLERI
{
    public Guid cha_Guid { get; set; }

    public short cha_DBCno { get; set; }

    public int? cha_SpecRecNo { get; set; }

    public bool? cha_iptal { get; set; }

    public short? cha_fileid { get; set; }

    public bool? cha_hidden { get; set; }

    public bool? cha_kilitli { get; set; }

    public bool? cha_degisti { get; set; }

    public int? cha_CheckSum { get; set; }

    public short? cha_create_user { get; set; }

    public DateTime cha_create_date { get; set; }

    public short? cha_lastup_user { get; set; }

    public DateTime? cha_lastup_date { get; set; }

    public string? cha_special1 { get; set; }

    public string? cha_special2 { get; set; }

    public string? cha_special3 { get; set; }

    public int? cha_firmano { get; set; }

    public int? cha_subeno { get; set; }

    public byte? cha_evrak_tip { get; set; }

    public string? cha_evrakno_seri { get; set; }

    public int? cha_evrakno_sira { get; set; }

    public int? cha_satir_no { get; set; }

    public DateTime? cha_tarihi { get; set; }

    public byte? cha_tip { get; set; }

    public byte? cha_cinsi { get; set; }

    public byte? cha_normal_Iade { get; set; }

    public byte? cha_tpoz { get; set; }

    public byte? cha_ticaret_turu { get; set; }

    public string? cha_belge_no { get; set; }

    public DateTime? cha_belge_tarih { get; set; }

    public string? cha_aciklama { get; set; }

    public string? cha_satici_kodu { get; set; }

    public string? cha_EXIMkodu { get; set; }

    public string? cha_projekodu { get; set; }

    public string? cha_yat_tes_kodu { get; set; }

    public byte? cha_cari_cins { get; set; }

    public string? cha_kod { get; set; }

    public string? cha_ciro_cari_kodu { get; set; }

    public byte? cha_d_cins { get; set; }

    public double? cha_d_kur { get; set; }

    public double? cha_altd_kur { get; set; }

    public byte? cha_grupno { get; set; }

    public string? cha_srmrkkodu { get; set; }

    public byte? cha_kasa_hizmet { get; set; }

    public string? cha_kasa_hizkod { get; set; }

    public byte? cha_karsidcinsi { get; set; }

    public double? cha_karsid_kur { get; set; }

    public byte? cha_karsidgrupno { get; set; }

    public string? cha_karsisrmrkkodu { get; set; }

    public double? cha_miktari { get; set; }

    public double? cha_meblag { get; set; }

    public double? cha_aratoplam { get; set; }

    public int? cha_vade { get; set; }

    public double? cha_Vade_Farki_Yuz { get; set; }

    public double? cha_ft_iskonto1 { get; set; }

    public double? cha_ft_iskonto2 { get; set; }

    public double? cha_ft_iskonto3 { get; set; }

    public double? cha_ft_iskonto4 { get; set; }

    public double? cha_ft_iskonto5 { get; set; }

    public double? cha_ft_iskonto6 { get; set; }

    public double? cha_ft_masraf1 { get; set; }

    public double? cha_ft_masraf2 { get; set; }

    public double? cha_ft_masraf3 { get; set; }

    public double? cha_ft_masraf4 { get; set; }

    public byte? cha_isk_mas1 { get; set; }

    public byte? cha_isk_mas2 { get; set; }

    public byte? cha_isk_mas3 { get; set; }

    public byte? cha_isk_mas4 { get; set; }

    public byte? cha_isk_mas5 { get; set; }

    public byte? cha_isk_mas6 { get; set; }

    public byte? cha_isk_mas7 { get; set; }

    public byte? cha_isk_mas8 { get; set; }

    public byte? cha_isk_mas9 { get; set; }

    public byte? cha_isk_mas10 { get; set; }

    public bool? cha_sat_iskmas1 { get; set; }

    public bool? cha_sat_iskmas2 { get; set; }

    public bool? cha_sat_iskmas3 { get; set; }

    public bool? cha_sat_iskmas4 { get; set; }

    public bool? cha_sat_iskmas5 { get; set; }

    public bool? cha_sat_iskmas6 { get; set; }

    public bool? cha_sat_iskmas7 { get; set; }

    public bool? cha_sat_iskmas8 { get; set; }

    public bool? cha_sat_iskmas9 { get; set; }

    public bool? cha_sat_iskmas10 { get; set; }

    public double? cha_yuvarlama { get; set; }

    public byte? cha_StFonPntr { get; set; }

    public double? cha_stopaj { get; set; }

    public double? cha_savsandesfonu { get; set; }

    public double? cha_avansmak_damgapul { get; set; }

    public byte? cha_vergipntr { get; set; }

    public bool? cha_vergisiz_fl { get; set; }

    public double? cha_otvtutari { get; set; }

    public bool? cha_otvvergisiz_fl { get; set; }

    public byte? cha_oiv_pntr { get; set; }

    public double? cha_oivtutari { get; set; }

    public double? cha_oiv_vergi { get; set; }

    public bool? cha_oivergisiz_fl { get; set; }

    public DateTime? cha_fis_tarih { get; set; }

    public int? cha_fis_sirano { get; set; }

    public string? cha_trefno { get; set; }

    public byte? cha_sntck_poz { get; set; }

    public DateTime? cha_reftarihi { get; set; }

    public byte? cha_istisnakodu { get; set; }

    public byte? cha_pos_hareketi { get; set; }

    public byte? cha_meblag_ana_doviz_icin_gecersiz_fl { get; set; }

    public byte? cha_meblag_alt_doviz_icin_gecersiz_fl { get; set; }

    public byte? cha_meblag_orj_doviz_icin_gecersiz_fl { get; set; }

    public Guid? cha_sip_uid { get; set; }

    public Guid? cha_kirahar_uid { get; set; }

    public DateTime? cha_vardiya_tarihi { get; set; }

    public byte? cha_vardiya_no { get; set; }

    public byte? cha_vardiya_evrak_ti { get; set; }

    public byte? cha_ebelge_turu { get; set; }

    public double? cha_tevkifat_toplam { get; set; }

    public byte? cha_e_islem_turu { get; set; }

    public byte? cha_fatura_belge_turu { get; set; }

    public string? cha_diger_belge_adi { get; set; }

    public string? cha_uuid { get; set; }

    public int? cha_adres_no { get; set; }

    public double? cha_vergifon_toplam { get; set; }

    public DateTime? cha_ilk_belge_tarihi { get; set; }

    public double? cha_ilk_belge_doviz_kuru { get; set; }

    public string? cha_HareketGrupKodu1 { get; set; }

    public string? cha_HareketGrupKodu2 { get; set; }

    public string? cha_HareketGrupKodu3 { get; set; }

    public string? cha_ebelgeno_seri { get; set; }

    public int? cha_ebelgeno_sira { get; set; }

    public string? cha_hubid { get; set; }

    public string? cha_hubglbid { get; set; }

    public string? cha_disyazilimid { get; set; }

    public byte? cha_disyazilim_tip { get; set; }

    public byte? cha_bsba_e_belge_mi { get; set; }

    public string? cha_eticaret_kanal_kodu { get; set; }

    public short? cha_hizli_satis_kasa_no { get; set; }

    public byte? cha_ebelge_Islemturu { get; set; }

    public bool? cha_tevkifat_sifirlandi_fl { get; set; }

    public double? cha_vergi1 { get; set; }

    public double? cha_vergi2 { get; set; }

    public double? cha_vergi3 { get; set; }

    public double? cha_vergi4 { get; set; }

    public double? cha_vergi5 { get; set; }

    public double? cha_vergi6 { get; set; }

    public double? cha_vergi7 { get; set; }

    public double? cha_vergi8 { get; set; }

    public double? cha_vergi9 { get; set; }

    public double? cha_vergi10 { get; set; }

    public double? cha_vergi11 { get; set; }

    public double? cha_vergi12 { get; set; }

    public double? cha_vergi13 { get; set; }

    public double? cha_vergi14 { get; set; }

    public double? cha_vergi15 { get; set; }

    public double? cha_vergi16 { get; set; }

    public double? cha_vergi17 { get; set; }

    public double? cha_vergi18 { get; set; }

    public double? cha_vergi19 { get; set; }

    public double? cha_vergi20 { get; set; }

    public double? cha_ilave_edilecek_kdv1 { get; set; }

    public double? cha_ilave_edilecek_kdv2 { get; set; }

    public double? cha_ilave_edilecek_kdv3 { get; set; }

    public double? cha_ilave_edilecek_kdv4 { get; set; }

    public double? cha_ilave_edilecek_kdv5 { get; set; }

    public double? cha_ilave_edilecek_kdv6 { get; set; }

    public double? cha_ilave_edilecek_kdv7 { get; set; }

    public double? cha_ilave_edilecek_kdv8 { get; set; }

    public double? cha_ilave_edilecek_kdv9 { get; set; }

    public double? cha_ilave_edilecek_kdv10 { get; set; }

    public double? cha_ilave_edilecek_kdv11 { get; set; }

    public double? cha_ilave_edilecek_kdv12 { get; set; }

    public double? cha_ilave_edilecek_kdv13 { get; set; }

    public double? cha_ilave_edilecek_kdv14 { get; set; }

    public double? cha_ilave_edilecek_kdv15 { get; set; }

    public double? cha_ilave_edilecek_kdv16 { get; set; }

    public double? cha_ilave_edilecek_kdv17 { get; set; }

    public double? cha_ilave_edilecek_kdv18 { get; set; }

    public double? cha_ilave_edilecek_kdv19 { get; set; }

    public double? cha_ilave_edilecek_kdv20 { get; set; }

    public byte? cha_efatura_belge_tipi { get; set; }
}
