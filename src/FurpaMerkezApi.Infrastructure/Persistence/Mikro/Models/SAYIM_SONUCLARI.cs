using System;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class SAYIM_SONUCLARI
{
    public Guid sym_Guid { get; set; }

    public short sym_DBCno { get; set; }

    public int? sym_SpecRECno { get; set; }

    public bool? sym_iptal { get; set; }

    public short? sym_fileid { get; set; }

    public bool? sym_hidden { get; set; }

    public bool? sym_kilitli { get; set; }

    public bool? sym_degisti { get; set; }

    public int? sym_checksum { get; set; }

    public short? sym_create_user { get; set; }

    public DateTime sym_create_date { get; set; }

    public short? sym_lastup_user { get; set; }

    public DateTime? sym_lastup_date { get; set; }

    public string? sym_special1 { get; set; }

    public string? sym_special2 { get; set; }

    public string? sym_special3 { get; set; }

    public DateTime? sym_tarihi { get; set; }

    public int? sym_depono { get; set; }

    public int? sym_evrakno { get; set; }

    public int? sym_satirno { get; set; }

    public string? sym_Stokkodu { get; set; }

    public string? sym_reyonkodu { get; set; }

    public string? sym_koridorkodu { get; set; }

    public string? sym_rafkodu { get; set; }

    public double? sym_miktar1 { get; set; }

    public double? sym_miktar2 { get; set; }

    public double? sym_miktar3 { get; set; }

    public double? sym_miktar4 { get; set; }

    public double? sym_miktar5 { get; set; }

    public byte? sym_birim_pntr { get; set; }

    public string? sym_barkod { get; set; }

    public int? sym_renkno { get; set; }

    public int? sym_bedenno { get; set; }

    public string? sym_parti_kodu { get; set; }

    public int? sym_lot_no { get; set; }

    public string? sym_serino { get; set; }
}
