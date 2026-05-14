using System;
using System.Collections.Generic;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class STOK_HAREKETLERI_OZET
{
    public int sho_RECno { get; set; }

    public int? sho_firmano { get; set; }

    public int? sho_subeno { get; set; }

    public string? sho_StokKodu { get; set; }

    public string? sho_SrmMerkezi { get; set; }

    public string? sho_ProjeKodu { get; set; }

    public int? sho_Depo { get; set; }

    public int? sho_MaliYil { get; set; }

    public byte? sho_Donem { get; set; }

    public byte? sho_HareketCins { get; set; }

    public double? sho_GirisNormal { get; set; }

    public double? sho_GirisIade { get; set; }

    public double? sho_CikisNormal { get; set; }

    public double? sho_CikisIade { get; set; }

    public double? sho_GirisNormal_2 { get; set; }

    public double? sho_GirisIade_2 { get; set; }

    public double? sho_CikisNormal_2 { get; set; }

    public double? sho_CikisIade_2 { get; set; }

    public double? sho_Maliyet_Ana_Giris { get; set; }

    public double? sho_Maliyet_Ana_GirisIade { get; set; }

    public double? sho_Maliyet_Ana_Cikis { get; set; }

    public double? sho_Maliyet_Ana_CikisIade { get; set; }

    public double? sho_Maliyet_Alt_Giris { get; set; }

    public double? sho_Maliyet_Alt_GirisIade { get; set; }

    public double? sho_Maliyet_Alt_Cikis { get; set; }

    public double? sho_Maliyet_Alt_CikisIade { get; set; }

    public double? sho_Maliyet_Orj_Giris { get; set; }

    public double? sho_Maliyet_Orj_GirisIade { get; set; }

    public double? sho_Maliyet_Orj_Cikis { get; set; }

    public double? sho_Maliyet_Orj_CikisIade { get; set; }

    public double? sho_Belge_Ana_Giris { get; set; }

    public double? sho_Belge_Ana_GirisIade { get; set; }

    public double? sho_Belge_Ana_Cikis { get; set; }

    public double? sho_Belge_Ana_CikisIade { get; set; }

    public double? sho_Belge_Alt_Giris { get; set; }

    public double? sho_Belge_Alt_GirisIade { get; set; }

    public double? sho_Belge_Alt_Cikis { get; set; }

    public double? sho_Belge_Alt_CikisIade { get; set; }

    public double? sho_Belge_Orj_Giris { get; set; }

    public double? sho_Belge_Orj_GirisIade { get; set; }

    public double? sho_Belge_Orj_Cikis { get; set; }

    public double? sho_Belge_Orj_CikisIade { get; set; }
}
