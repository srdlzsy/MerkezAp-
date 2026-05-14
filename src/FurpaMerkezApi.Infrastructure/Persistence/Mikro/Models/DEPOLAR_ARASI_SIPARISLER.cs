using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class DEPOLAR_ARASI_SIPARISLER
{
    public Guid ssip_Guid { get; set; }

    public short ssip_DBCno { get; set; }

    public int? ssip_SpecRECno { get; set; }

    public bool? ssip_iptal { get; set; }

    public short? ssip_fileid { get; set; }

    public bool? ssip_hidden { get; set; }

    public bool? ssip_kilitli { get; set; }

    public bool? ssip_degisti { get; set; }

    public int? ssip_checksum { get; set; }

    public short? ssip_create_user { get; set; }

    public DateTime ssip_create_date { get; set; }

    public short? ssip_lastup_user { get; set; }

    public DateTime? ssip_lastup_date { get; set; }

    public string? ssip_special1 { get; set; }

    public string? ssip_special2 { get; set; }

    public string? ssip_special3 { get; set; }

    public int? ssip_firmano { get; set; }

    public int? ssip_subeno { get; set; }

    public DateTime? ssip_tarih { get; set; }

    public DateTime? ssip_teslim_tarih { get; set; }

    public string? ssip_evrakno_seri { get; set; }

    public int? ssip_evrakno_sira { get; set; }

    public int? ssip_satirno { get; set; }

    public string? ssip_belgeno { get; set; }

    public DateTime? ssip_belge_tarih { get; set; }

    public string? ssip_stok_kod { get; set; }

    public double? ssip_miktar { get; set; }

    public double? ssip_b_fiyat { get; set; }

    public double? ssip_tutar { get; set; }

    public double? ssip_teslim_miktar { get; set; }

    public string? ssip_aciklama { get; set; }

    public int? ssip_girdepo { get; set; }

    public int? ssip_cikdepo { get; set; }

    public bool? ssip_kapat_fl { get; set; }

    public byte? ssip_birim_pntr { get; set; }

    public int? ssip_fiyat_liste_no { get; set; }

    public Guid? ssip_stal_uid { get; set; }

    public string? ssip_paket_kod { get; set; }

    public string? ssip_kapatmanedenkod { get; set; }

    public string? ssip_projekodu { get; set; }

    public string? ssip_sormerkezi { get; set; }

    public DateTime? ssip_gecerlilik_tarihi { get; set; }

    public double? ssip_rezervasyon_miktari { get; set; }

    public double? ssip_rezerveden_teslim_edilen { get; set; }
}
