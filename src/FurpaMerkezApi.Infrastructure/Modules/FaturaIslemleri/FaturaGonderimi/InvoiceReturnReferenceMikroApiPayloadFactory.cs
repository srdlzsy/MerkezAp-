namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;

internal static class InvoiceReturnReferenceMikroApiPayloadFactory
{
    internal const string TableNo = "597";

    internal static object Create(
        Guid? existingRowGuid,
        Guid returnInvoiceGuid,
        string returnInvoiceNo,
        DateTime? returnInvoiceDate)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TabloNo"] = TableNo,
            ["KayitTipi"] = existingRowGuid.HasValue ? "1" : "0",
            ["ebh_Guid"] = existingRowGuid ?? Guid.NewGuid(),
            ["ebh_hareket_tipi"] = 1,
            ["ebh_related_uid"] = returnInvoiceGuid,
            ["ebh_iade_fat_no1"] = returnInvoiceNo.Trim(),
            ["ebh_iade_fat_tarihi1"] = returnInvoiceDate?.ToString("yyyyMMdd") ?? string.Empty
        };

        if (!existingRowGuid.HasValue)
        {
            row["ebh_odeme_sekli"] = 0;
            row["ebh_odeme_aciklama"] = string.Empty;
            row["ebh_odeme_aracisi"] = string.Empty;
            row["ebh_satisin_webadresi"] = string.Empty;
            row["ebh_gonderi_tarihi"] = DateTime.Today.ToString("yyyyMMdd");
            row["ebh_gonderi_tasiyan"] = string.Empty;
            row["ebh_gonderi_no"] = string.Empty;
            row["ebh_ekli_dosya"] = string.Empty;
            row["ebh_mukellefiyetdosyano"] = string.Empty;
            row["ebh_mukellefiyetdonembasi"] = "18991230";
            row["ebh_mukellefiyetdonemsonu"] = "18991230";
            row["ebh_konaklamafaturasi_fl"] = false;
            row["ebh_ImeI_no"] = string.Empty;
            row["ebh_mac_no"] = string.Empty;
            row["ebh_enerjifaturatipi"] = 0;
            row["ebh_arac_plakano"] = string.Empty;
            row["ebh_arac_kimlikno"] = string.Empty;
            row["ebh_sarjunite_serino"] = string.Empty;
            row["ebh_sarj_baslama"] = "18991230";
            row["ebh_sarj_bitis"] = "18991230";
            row["ebh_esurapor_id"] = string.Empty;
            row["ebh_esuRapor_tarihi"] = "18991230";
            row["ebh_Internet_satis_fl"] = false;
        }

        return new { Kayit = new[] { row } };
    }
}
