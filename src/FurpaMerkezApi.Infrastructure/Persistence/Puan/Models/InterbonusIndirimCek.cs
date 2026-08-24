namespace FurpaMerkezApi.Infrastructure.Persistence.Puan.Models;

public class InterbonusIndirimCek
{
    public long Id { get; set; }

    public string? Cek_No { get; set; }

    public string Cari_Kod { get; set; } = null!;

    public decimal? Tutar { get; set; }

    public decimal? Puan { get; set; }

    public DateTime? Baslangic { get; set; }

    public DateTime? Bitis { get; set; }

    public bool? Flag { get; set; }

    public string? Sube_Kodu { get; set; }

    public short? Kasa_No { get; set; }

    public bool? Musteri_Flag { get; set; }

    public decimal? Belge_Tutar { get; set; }

    public DateTime? DATECREATED { get; set; }

    public DateTime? DATEMODIFIED { get; set; }

    public long? SESSIONIDCREATED { get; set; }

    public long? SESSIONIDMODIFIED { get; set; }

    public byte? Kdv { get; set; }

    public string? Kampanya_Kodu { get; set; }

    public string? FATURA_GUID { get; set; }

    public string? KULLANICI { get; set; }

    public string? KASA_RG { get; set; }

    public string? KASA_REF_NO { get; set; }

    public string? BELGE_TIPI { get; set; }

    public short? EKU_NO { get; set; }

    public short? Z_NO { get; set; }

    public short? FIS_NO { get; set; }

    public byte? Kart_Tipi { get; set; }
}
