using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class DEPOLAR
{
    public Guid dep_Guid { get; set; }

    public short dep_DBCno { get; set; }

    public int? dep_SpecRECno { get; set; }

    public bool? dep_iptal { get; set; }

    public short? dep_fileid { get; set; }

    public bool? dep_hidden { get; set; }

    public bool? dep_kilitli { get; set; }

    public bool? dep_degisti { get; set; }

    public int? dep_checksum { get; set; }

    public short? dep_create_user { get; set; }

    public DateTime dep_create_date { get; set; }

    public short? dep_lastup_user { get; set; }

    public DateTime? dep_lastup_date { get; set; }

    public string? dep_special1 { get; set; }

    public string? dep_special2 { get; set; }

    public string? dep_special3 { get; set; }

    public int? dep_firmano { get; set; }

    public int? dep_subeno { get; set; }

    public int? dep_no { get; set; }

    public string? dep_adi { get; set; }

    public string? dep_grup_kodu { get; set; }

    public byte? dep_tipi { get; set; }

    public byte? dep_DepoSevkOtoFiyat { get; set; }

    public byte? dep_hareket_tipi { get; set; }

    public string? dep_muh_kodu { get; set; }

    public string? dep_sor_mer_kodu { get; set; }

    public string? dep_proje_kodu { get; set; }

    public int? dep_DepoSevkUygFiyat { get; set; }

    public DateTime? dep_KilitTarihi { get; set; }

    public string? dep_cadde { get; set; }

    public string? dep_mahalle { get; set; }

    public string? dep_sokak { get; set; }

    public string? dep_Semt { get; set; }

    public string? dep_Apt_No { get; set; }

    public string? dep_Daire_No { get; set; }

    public string? dep_posta_Kodu { get; set; }

    public string? dep_Ilce { get; set; }

    public string? dep_Il { get; set; }

    public string? dep_Ulke { get; set; }

    public string? dep_Adres_kodu { get; set; }

    public double? dep_gps_enlem { get; set; }

    public double? dep_gps_boylam { get; set; }

    public double? dep_alani { get; set; }

    public double? dep_rafhacmi { get; set; }

    public string? dep_yetkili_email { get; set; }

    public double? dep_satis_alani { get; set; }

    public double? dep_sergi_alani { get; set; }

    public double? dep_otopark_alani { get; set; }

    public int? dep_otopark_kapasite { get; set; }

    public int? dep_kasa_sayisi { get; set; }

    public double? dep_kamyon_kasa_hacmi { get; set; }

    public double? dep_kamyon_istiab_haddi { get; set; }

    public string? dep_dizin_adi { get; set; }

    public string? dep_tel_ulke_kodu { get; set; }

    public string? dep_tel_bolge_kodu { get; set; }

    public string? dep_tel_no1 { get; set; }

    public string? dep_tel_no2 { get; set; }

    public string? dep_tel_faxno { get; set; }

    public string? dep_tel_modem { get; set; }

    public bool? dep_envanter_harici_fl { get; set; }

    public byte? dep_detay_takibi { get; set; }

    public string? dep_barkod_yazici_yolu { get; set; }

    public string? dep_fason_sor_mer_kodu { get; set; }

    public byte? dep_EksiyeDusurenStkHar { get; set; }

    public byte? dep_BagliOrtakliklaraSatisUygFiyat { get; set; }

    public string? dep_bolge_kodu { get; set; }

    public byte? dep_NakliyefisiSatisFiyatTipi { get; set; }

    public bool? dep_gidiste_eirsaliye_fl { get; set; }

    public bool? dep_geliste_eirsaliye_fl { get; set; }

    public bool? dep_fytdegfis_kullanilmaz_fl { get; set; }

    public byte? dep_seribag_detay_takibi { get; set; }

    public bool? dep_dikeycozum_raftakibi_zorunlu_fl { get; set; }
}
