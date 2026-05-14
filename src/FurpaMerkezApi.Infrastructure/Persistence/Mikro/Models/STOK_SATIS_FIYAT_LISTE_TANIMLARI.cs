using System;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class STOK_SATIS_FIYAT_LISTE_TANIMLARI
{
    public Guid sfl_Guid { get; set; }

    public short sfl_DBCno { get; set; }

    public int? sfl_SpecRECno { get; set; }

    public bool? sfl_iptal { get; set; }

    public short? sfl_fileid { get; set; }

    public bool? sfl_hidden { get; set; }

    public bool? sfl_kilitli { get; set; }

    public bool? sfl_degisti { get; set; }

    public int? sfl_checksum { get; set; }

    public short? sfl_create_user { get; set; }

    public DateTime sfl_create_date { get; set; }

    public short? sfl_lastup_user { get; set; }

    public DateTime? sfl_lastup_date { get; set; }

    public string? sfl_special1 { get; set; }

    public string? sfl_special2 { get; set; }

    public string? sfl_special3 { get; set; }

    public int? sfl_sirano { get; set; }

    public string? sfl_aciklama { get; set; }

    public byte? sfl_fiyatuygulama { get; set; }

    public string? sfl_fiyatformul { get; set; }

    public byte? sfl_odepluygulama { get; set; }

    public string? sfl_odeplformul { get; set; }

    public int? sfl_sabit_odeme_plani { get; set; }

    public bool? sfl_kdvdahil { get; set; }

    public DateTime? sfl_ilktarih { get; set; }

    public DateTime? sfl_sontarih { get; set; }

    public int? sfl_yerineuygulanacakfiyat { get; set; }

    public byte? sfl_kurhesaplamasekli { get; set; }

    public byte? sfl_doviz_uygulama { get; set; }

    public byte? sfl_sabit_doviz { get; set; }

    public byte? sfl_iskonto_uygulama { get; set; }

    public string? sfl_sabit_iskonto { get; set; }

    public double? sfl_sabit_kur { get; set; }

    public byte? sfl_kampanya_uygulama { get; set; }

    public string? sfl_sabit_kampanya { get; set; }

    public bool? sfl_kampanya_vade_gozardi { get; set; }

    public bool? sfl_kampanya_iskonto_gozardi { get; set; }

    public bool? sfl_otvdahil { get; set; }

    public bool? sfl_oivdahil { get; set; }
}
