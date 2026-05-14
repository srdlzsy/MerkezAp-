using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class CARI_HESAP_ADRESLERI
{
    public Guid adr_Guid { get; set; }

    public short adr_DBCno { get; set; }

    public int? adr_SpecRECno { get; set; }

    public bool? adr_iptal { get; set; }

    public short? adr_fileid { get; set; }

    public bool? adr_hidden { get; set; }

    public bool? adr_kilitli { get; set; }

    public bool? adr_degisti { get; set; }

    public int? adr_checksum { get; set; }

    public short? adr_create_user { get; set; }

    public DateTime adr_create_date { get; set; }

    public short? adr_lastup_user { get; set; }

    public DateTime? adr_lastup_date { get; set; }

    public string? adr_special1 { get; set; }

    public string? adr_special2 { get; set; }

    public string? adr_special3 { get; set; }

    public string? adr_cari_kod { get; set; }

    public int? adr_adres_no { get; set; }

    public bool? adr_aprint_fl { get; set; }

    public string? adr_cadde { get; set; }

    public string? adr_mahalle { get; set; }

    public string? adr_sokak { get; set; }

    public string? adr_Semt { get; set; }

    public string? adr_Apt_No { get; set; }

    public string? adr_Daire_No { get; set; }

    public string? adr_posta_kodu { get; set; }

    public string? adr_ilce { get; set; }

    public string? adr_il { get; set; }

    public string? adr_ulke { get; set; }

    public string? adr_Adres_kodu { get; set; }

    public string? adr_tel_ulke_kodu { get; set; }

    public string? adr_tel_bolge_kodu { get; set; }

    public string? adr_tel_no1 { get; set; }

    public string? adr_tel_no2 { get; set; }

    public string? adr_tel_faxno { get; set; }

    public string? adr_tel_modem { get; set; }

    public string? adr_yon_kodu { get; set; }

    public short? adr_uzaklik_kodu { get; set; }

    public string? adr_temsilci_kodu { get; set; }

    public string? adr_ozel_not { get; set; }

    public byte? adr_ziyaretperyodu { get; set; }

    public double? adr_ziyaretgunu { get; set; }

    public double? adr_gps_enlem { get; set; }

    public double? adr_gps_boylam { get; set; }

    public byte? adr_ziyarethaftasi { get; set; }

    public bool? adr_ziygunu2_1 { get; set; }

    public bool? adr_ziygunu2_2 { get; set; }

    public bool? adr_ziygunu2_3 { get; set; }

    public bool? adr_ziygunu2_4 { get; set; }

    public bool? adr_ziygunu2_5 { get; set; }

    public bool? adr_ziygunu2_6 { get; set; }

    public bool? adr_ziygunu2_7 { get; set; }

    public string? adr_efatura_alias { get; set; }

    public string? adr_eirsaliye_alias { get; set; }
}
