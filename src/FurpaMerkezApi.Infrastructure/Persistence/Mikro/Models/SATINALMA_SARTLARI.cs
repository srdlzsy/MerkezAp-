using System;

namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public partial class SATINALMA_SARTLARI
{
    public Guid sas_Guid { get; set; }

    public string? sas_stok_kod { get; set; }

    public string? sas_cari_kod { get; set; }

    public DateTime? sas_belge_tarih { get; set; }

    public DateTime? sas_create_date { get; set; }

    public double? sas_net_alis_kdvli { get; set; }

    public double? sas_isk_miktar1 { get; set; }

    public double? sas_isk_miktar2 { get; set; }

    public double? sas_isk_miktar3 { get; set; }

    public double? sas_isk_miktar4 { get; set; }

    public double? sas_isk_miktar5 { get; set; }

    public double? sas_isk_miktar6 { get; set; }
}
