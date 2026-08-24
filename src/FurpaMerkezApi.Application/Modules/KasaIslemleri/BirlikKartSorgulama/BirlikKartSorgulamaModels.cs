namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.BirlikKartSorgulama;

public sealed class BirlikKartSorgulamaRequest
{
    public string? KartNo { get; set; }
}

public sealed class BirlikKartSorgulamaResponse
{
    public bool IsFound { get; set; }
    public string? KartNo { get; set; }
    public string? CekNo { get; set; }
    public string? CariKod { get; set; }
    public decimal? Tutar { get; set; }
    public decimal? Puan { get; set; }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public bool? Flag { get; set; }
    public string? SubeKodu { get; set; }
    public short? KasaNo { get; set; }
    public byte? KartTipi { get; set; }
    public string? Message { get; set; }
}

public sealed class BirlikKartSorgulamaGuncelleRequest
{
    public string? CekNo { get; set; }
    public string? CariKod { get; set; }
    public decimal? Tutar { get; set; }
    public decimal? Puan { get; set; }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public bool? Flag { get; set; }
    public string? SubeKodu { get; set; }
    public short? KasaNo { get; set; }
    public byte? KartTipi { get; set; }
}

public sealed class BirlikKartGuncelleResponse
{
    public bool IsUpdated { get; set; }
    public string? Message { get; set; }
}

public sealed class BirlikKartDetayRequest
{
    public string? CekNo { get; set; }
}

public sealed class BirlikKartDetayResponse
{
    public bool IsFound { get; set; }
    public string? KartNo { get; set; }
    public string? CekNo { get; set; }
    public string? CariKod { get; set; }
    public decimal? Tutar { get; set; }
    public decimal? Puan { get; set; }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public bool? Flag { get; set; }
    public string? SubeKodu { get; set; }
    public short? KasaNo { get; set; }
    public byte? KartTipi { get; set; }
    public string? Message { get; set; }
}
