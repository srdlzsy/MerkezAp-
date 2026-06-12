using System.Globalization;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

internal static class CompanyReceivingIrsaliyeMikroApiPayloadFactory
{
    internal static CompanyReceivingIrsaliyeMikroApiPayload Create(
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        string description)
    {
        var satirlar = movements
            .OrderBy(movement => movement.sth_satirno ?? 0)
            .Select(movement => new CompanyReceivingIrsaliyeMikroApiLine(
                FormatDate(movement.sth_tarih),
                movement.sth_tip ?? 0,
                movement.sth_cins ?? 0,
                movement.sth_normal_iade ?? 0,
                movement.sth_evraktip ?? 0,
                NormalizeText(movement.sth_evrakno_seri, 20),
                movement.sth_evrakno_sira ?? 0,
                movement.sth_satirno ?? 0,
                NormalizeText(movement.sth_belge_no, 50),
                FormatDate(movement.sth_belge_tarih),
                NormalizeText(movement.sth_stok_kod, 25),
                movement.sth_cari_cinsi ?? 0,
                NormalizeText(movement.sth_cari_kodu, 25),
                NormalizeText(movement.sth_isemri_gider_kodu, 25),
                movement.sth_miktar ?? 0d,
                movement.sth_miktar2 ?? 0d,
                movement.sth_birim_pntr ?? 1,
                movement.sth_tutar ?? 0d,
                movement.sth_vergi_pntr ?? 0,
                movement.sth_vergi ?? 0d,
                movement.sth_vergisiz_fl ?? false,
                movement.sth_iskonto1 ?? 0d,
                movement.sth_iskonto2 ?? 0d,
                movement.sth_isk_mas1 ?? 0,
                movement.sth_isk_mas2 ?? 1,
                movement.sth_giris_depo_no ?? 0,
                movement.sth_cikis_depo_no ?? 0,
                FormatDate(movement.sth_malkbl_sevk_tarihi),
                NormalizeText(movement.sth_aciklama, 50),
                movement.sth_adres_no ?? 1,
                NormalizeText(movement.sth_parti_kodu, 25),
                movement.sth_lot_no ?? 0,
                NormalizeText(movement.sth_proje_kodu, 25),
                NormalizeText(movement.sth_cari_srm_merkezi, 25),
                NormalizeText(movement.sth_stok_srm_merkezi, 25),
                movement.sth_fiyat_liste_no ?? -1,
                NormalizeGuid(movement.sth_sip_uid),
                NormalizeText(movement.sth_HareketGrupKodu1, 25),
                NormalizeText(movement.sth_HareketGrupKodu2, 25),
                NormalizeText(movement.sth_HareketGrupKodu3, 25),
                FormatDate(movement.sth_teslim_tarihi),
                NormalizeText(movement.sth_eticaret_kanal_kodu, 25),
                string.Empty,
                [],
                []))
            .ToArray();

        var documentDescriptions = string.IsNullOrWhiteSpace(description)
            ? null
            : new[]
            {
                new CompanyReceivingIrsaliyeMikroApiDocumentDescription(
                    NormalizeText(description, 127))
            };

        return new CompanyReceivingIrsaliyeMikroApiPayload(
            [
                new CompanyReceivingIrsaliyeMikroApiDocument(
                    satirlar,
                    documentDescriptions)
            ]);
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue
            ? value.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : string.Empty;

    private static string NormalizeGuid(Guid? value) =>
        value.HasValue && value.Value != Guid.Empty ? value.Value.ToString() : string.Empty;

    private static string NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

internal sealed record CompanyReceivingIrsaliyeMikroApiPayload(
    IReadOnlyCollection<CompanyReceivingIrsaliyeMikroApiDocument> evraklar);

internal sealed record CompanyReceivingIrsaliyeMikroApiDocument(
    IReadOnlyCollection<CompanyReceivingIrsaliyeMikroApiLine> satirlar,
    IReadOnlyCollection<CompanyReceivingIrsaliyeMikroApiDocumentDescription>? evrak_aciklamalari = null);

internal sealed record CompanyReceivingIrsaliyeMikroApiDocumentDescription(
    string aciklama);

internal sealed record CompanyReceivingIrsaliyeMikroApiLine(
    string sth_tarih,
    byte sth_tip,
    byte sth_cins,
    byte sth_normal_iade,
    byte sth_evraktip,
    string sth_evrakno_seri,
    int sth_evrakno_sira,
    int sth_satirno,
    string sth_belge_no,
    string sth_belge_tarih,
    string sth_stok_kod,
    int sth_cari_cinsi,
    string sth_cari_kodu,
    string sth_isemri_gider_kodu,
    double sth_miktar,
    double sth_miktar2,
    int sth_birim_pntr,
    double sth_tutar,
    int sth_vergi_pntr,
    double sth_vergi,
    bool sth_vergisiz_fl,
    double sth_iskonto1,
    double sth_iskonto2,
    int sth_isk_mas1,
    int sth_isk_mas2,
    int sth_giris_depo_no,
    int sth_cikis_depo_no,
    string sth_malkbl_sevk_tarihi,
    string sth_aciklama,
    int sth_adres_no,
    string sth_parti_kodu,
    int sth_lot_no,
    string sth_proje_kodu,
    string sth_cari_srm_merkezi,
    string sth_stok_srm_merkezi,
    int sth_fiyat_liste_no,
    string sth_sip_uid,
    string sth_HareketGrupKodu1,
    string sth_HareketGrupKodu2,
    string sth_HareketGrupKodu3,
    string sth_teslim_tarihi,
    string sth_eticaret_kanal_kodu,
    string seriler,
    IReadOnlyCollection<CompanyReceivingIrsaliyeMikroApiColorSizeLine> renk_beden,
    IReadOnlyCollection<CompanyReceivingIrsaliyeMikroApiUserTableLine> user_tablo);

internal sealed record CompanyReceivingIrsaliyeMikroApiColorSizeLine;

internal sealed record CompanyReceivingIrsaliyeMikroApiUserTableLine;
