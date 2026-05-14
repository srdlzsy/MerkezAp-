using System;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class EVRAK_ACIKLAMALARI
{
    public Guid egk_Guid { get; set; }

    public short egk_DBCno { get; set; }

    public int? egk_SpecRECno { get; set; }

    public bool? egk_iptal { get; set; }

    public short? egk_fileid { get; set; }

    public bool? egk_hidden { get; set; }

    public bool? egk_kilitli { get; set; }

    public bool? egk_degisti { get; set; }

    public int? egk_checksum { get; set; }

    public short? egk_create_user { get; set; }

    public DateTime egk_create_date { get; set; }

    public short? egk_lastup_user { get; set; }

    public DateTime? egk_lastup_date { get; set; }

    public string? egk_special1 { get; set; }

    public string? egk_special2 { get; set; }

    public string? egk_special3 { get; set; }

    public short? egk_dosyano { get; set; }

    public byte? egk_hareket_tip { get; set; }

    public byte? egk_evr_tip { get; set; }

    public string? egk_evr_seri { get; set; }

    public int? egk_evr_sira { get; set; }

    public string? egk_evr_ustkod { get; set; }

    public short? egk_evr_doksayisi { get; set; }

    public string? egk_evracik1 { get; set; }

    public string? egk_evracik2 { get; set; }

    public string? egk_evracik3 { get; set; }

    public string? egk_evracik4 { get; set; }

    public string? egk_evracik5 { get; set; }

    public string? egk_evracik6 { get; set; }

    public string? egk_evracik7 { get; set; }

    public string? egk_evracik8 { get; set; }

    public string? egk_evracik9 { get; set; }

    public string? egk_evracik10 { get; set; }

    public double? egk_sipgenkarorani { get; set; }

    public string? egk_kargokodu { get; set; }

    public string? egk_kargono { get; set; }

    public DateTime? egk_tesaltarihi { get; set; }

    public string? egk_tesalkisi { get; set; }

    public short? egk_prevwiewsayisi { get; set; }

    public short? egk_emailsayisi { get; set; }

    public bool? egk_Evrakopno_verildi_fl { get; set; }
}
