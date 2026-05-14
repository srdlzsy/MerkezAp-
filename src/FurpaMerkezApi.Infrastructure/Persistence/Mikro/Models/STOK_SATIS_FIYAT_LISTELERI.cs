using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class STOK_SATIS_FIYAT_LISTELERI
{
    public Guid sfiyat_Guid { get; set; }

    public short sfiyat_DBCno { get; set; }

    public int? sfiyat_SpecRECno { get; set; }

    public bool? sfiyat_iptal { get; set; }

    public short? sfiyat_fileid { get; set; }

    public bool? sfiyat_hidden { get; set; }

    public bool? sfiyat_kilitli { get; set; }

    public bool? sfiyat_degisti { get; set; }

    public int? sfiyat_checksum { get; set; }

    public short? sfiyat_create_user { get; set; }

    public DateTime sfiyat_create_date { get; set; }

    public short? sfiyat_lastup_user { get; set; }

    public DateTime? sfiyat_lastup_date { get; set; }

    public string? sfiyat_special1 { get; set; }

    public string? sfiyat_special2 { get; set; }

    public string? sfiyat_special3 { get; set; }

    public string? sfiyat_stokkod { get; set; }

    public int? sfiyat_listesirano { get; set; }

    public int? sfiyat_deposirano { get; set; }

    public int? sfiyat_odemeplan { get; set; }

    public byte? sfiyat_birim_pntr { get; set; }

    public double? sfiyat_fiyati { get; set; }

    public byte? sfiyat_doviz { get; set; }

    public string? sfiyat_iskontokod { get; set; }

    public byte? sfiyat_deg_nedeni { get; set; }

    public double? sfiyat_primyuzdesi { get; set; }

    public string? sfiyat_kampanyakod { get; set; }

    public double? sfiyat_doviz_kuru { get; set; }
}
