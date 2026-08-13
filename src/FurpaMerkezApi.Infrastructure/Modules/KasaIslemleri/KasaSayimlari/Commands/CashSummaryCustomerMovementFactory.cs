using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;

internal static class CashSummaryCustomerMovementFactory
{
    internal const int MainLineNo = 0;
    internal const int ZDifferenceLineNo = 1;
    internal const int ZReportTotalLineNo = 2;
    internal const string ZDifferenceAccountCode = "0002";
    internal const string ZReportDocumentName = "Z Raporu";

    private const short MikroUserNo = 39;
    private const short CustomerMovementFileId = 51;
    private const byte CustomerMovementDocumentType = 60;
    private const byte CustomerMovementDebitType = 0;
    private const byte CustomerMovementCreditType = 1;
    private const byte CustomerMovementGenre = 5;
    private const byte CustomerMovementNormalReturn = 0;
    private const byte CustomerMovementTpoz = 0;
    private const byte CustomerMovementTradeType = 0;
    private const byte CustomerMovementCashCustomerGenus = 4;
    private const byte CustomerMovementInvoiceDocumentType = 3;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    internal static IEnumerable<CARI_HESAP_HAREKETLERI> CreateMovements(
        CreateCashSummaryRequest request,
        DateTime summaryDate,
        string documentSerie,
        int documentOrderNo,
        double documentTotal,
        DateTime now)
    {
        yield return CreateMovement(
            request,
            summaryDate,
            documentSerie,
            documentOrderNo,
            MainLineNo,
            CustomerMovementDebitType,
            documentTotal,
            $"Kasa sayimi {documentSerie}/{documentOrderNo}",
            $"KASA-{request.WarehouseNo}",
            request.CashNo.ToString(),
            now);

        if (IsZero(request.ZTotalValue))
        {
            yield break;
        }

        var zDifference = Math.Round(documentTotal - request.ZTotalValue, 2);

        yield return CreateMovement(
            request,
            summaryDate,
            documentSerie,
            documentOrderNo,
            ZDifferenceLineNo,
            CustomerMovementCreditType,
            zDifference,
            $"Z Rapor Farki {documentSerie}/{documentOrderNo}",
            ZDifferenceAccountCode,
            string.Empty,
            now);

        yield return CreateMovement(
            request,
            summaryDate,
            documentSerie,
            documentOrderNo,
            ZReportTotalLineNo,
            CustomerMovementCreditType,
            Math.Round(request.ZTotalValue, 2),
            $"Z Rapor Toplami {documentSerie}/{documentOrderNo}",
            request.WarehouseNo.ToString(),
            string.Empty,
            now);
    }

    internal static bool IsMainMovement(CARI_HESAP_HAREKETLERI movement) =>
        movement.cha_satir_no is null or MainLineNo;

    internal static bool IsZDifferenceMovement(CARI_HESAP_HAREKETLERI movement) =>
        movement.cha_satir_no == ZDifferenceLineNo;

    internal static bool IsZReportTotalMovement(CARI_HESAP_HAREKETLERI movement) =>
        movement.cha_satir_no == ZReportTotalLineNo;

    private static CARI_HESAP_HAREKETLERI CreateMovement(
        CreateCashSummaryRequest request,
        DateTime summaryDate,
        string documentSerie,
        int documentOrderNo,
        int rowNo,
        byte movementType,
        double amount,
        string description,
        string customerCode,
        string cashServiceCode,
        DateTime now)
    {
        var warehouseCode = request.WarehouseNo.ToString();

        return new()
        {
            cha_Guid = Guid.NewGuid(),
            cha_DBCno = 0,
            cha_SpecRecNo = 0,
            cha_iptal = false,
            cha_fileid = CustomerMovementFileId,
            cha_hidden = false,
            cha_kilitli = false,
            cha_degisti = false,
            cha_CheckSum = 0,
            cha_create_user = MikroUserNo,
            cha_create_date = now,
            cha_lastup_user = MikroUserNo,
            cha_lastup_date = now,
            cha_special1 = string.Empty,
            cha_special2 = string.Empty,
            cha_special3 = string.Empty,
            cha_firmano = 0,
            cha_subeno = 0,
            cha_evrak_tip = CustomerMovementDocumentType,
            cha_evrakno_seri = documentSerie,
            cha_evrakno_sira = documentOrderNo,
            cha_satir_no = rowNo,
            cha_tarihi = summaryDate,
            cha_tip = movementType,
            cha_cinsi = CustomerMovementGenre,
            cha_normal_Iade = CustomerMovementNormalReturn,
            cha_tpoz = CustomerMovementTpoz,
            cha_ticaret_turu = CustomerMovementTradeType,
            cha_belge_no = $"{request.CashNo}-{request.ZReportNo}",
            cha_belge_tarih = summaryDate,
            cha_aciklama = NormalizeText(description, 40),
            cha_satici_kodu = request.CashierNo.ToString(),
            cha_cari_cins = CustomerMovementCashCustomerGenus,
            cha_kod = customerCode,
            cha_d_cins = 0,
            cha_d_kur = 1d,
            cha_altd_kur = 1d,
            cha_grupno = 0,
            cha_srmrkkodu = warehouseCode,
            cha_kasa_hizmet = 0,
            cha_kasa_hizkod = cashServiceCode,
            cha_karsidcinsi = 0,
            cha_karsid_kur = 1d,
            cha_karsidgrupno = 0,
            cha_karsisrmrkkodu = warehouseCode,
            cha_miktari = 1d,
            cha_meblag = amount,
            cha_aratoplam = amount,
            cha_vade = 0,
            cha_Vade_Farki_Yuz = 0d,
            cha_ft_iskonto1 = 0d,
            cha_ft_iskonto2 = 0d,
            cha_ft_iskonto3 = 0d,
            cha_ft_iskonto4 = 0d,
            cha_ft_iskonto5 = 0d,
            cha_ft_iskonto6 = 0d,
            cha_ft_masraf1 = 0d,
            cha_ft_masraf2 = 0d,
            cha_ft_masraf3 = 0d,
            cha_ft_masraf4 = 0d,
            cha_isk_mas1 = 0,
            cha_isk_mas2 = 0,
            cha_isk_mas3 = 0,
            cha_isk_mas4 = 0,
            cha_isk_mas5 = 0,
            cha_isk_mas6 = 0,
            cha_isk_mas7 = 0,
            cha_isk_mas8 = 0,
            cha_isk_mas9 = 0,
            cha_isk_mas10 = 0,
            cha_sat_iskmas1 = false,
            cha_sat_iskmas2 = false,
            cha_sat_iskmas3 = false,
            cha_sat_iskmas4 = false,
            cha_sat_iskmas5 = false,
            cha_sat_iskmas6 = false,
            cha_sat_iskmas7 = false,
            cha_sat_iskmas8 = false,
            cha_sat_iskmas9 = false,
            cha_sat_iskmas10 = false,
            cha_yuvarlama = 0d,
            cha_StFonPntr = 0,
            cha_stopaj = 0d,
            cha_savsandesfonu = 0d,
            cha_avansmak_damgapul = 0d,
            cha_vergipntr = 0,
            cha_vergisiz_fl = false,
            cha_otvtutari = 0d,
            cha_otvvergisiz_fl = false,
            cha_oiv_pntr = 0,
            cha_oivtutari = 0d,
            cha_oiv_vergi = 0d,
            cha_oivergisiz_fl = false,
            cha_fis_tarih = MikroEmptyDate,
            cha_fis_sirano = 0,
            cha_trefno = string.Empty,
            cha_sntck_poz = 0,
            cha_reftarihi = summaryDate,
            cha_istisnakodu = 0,
            cha_pos_hareketi = 0,
            cha_meblag_ana_doviz_icin_gecersiz_fl = 0,
            cha_meblag_alt_doviz_icin_gecersiz_fl = 0,
            cha_meblag_orj_doviz_icin_gecersiz_fl = 0,
            cha_sip_uid = Guid.Empty,
            cha_kirahar_uid = Guid.Empty,
            cha_vardiya_tarihi = summaryDate,
            cha_vardiya_no = Convert.ToByte(Math.Clamp(request.CashNo, 0, byte.MaxValue)),
            cha_vardiya_evrak_ti = 0,
            cha_ebelge_turu = 0,
            cha_tevkifat_toplam = 0d,
            cha_e_islem_turu = 0,
            cha_fatura_belge_turu = CustomerMovementInvoiceDocumentType,
            cha_diger_belge_adi = ZReportDocumentName,
            cha_uuid = string.Empty,
            cha_adres_no = 0,
            cha_vergifon_toplam = 0d,
            cha_ilk_belge_tarihi = summaryDate,
            cha_ilk_belge_doviz_kuru = 1d,
            cha_HareketGrupKodu1 = string.Empty,
            cha_HareketGrupKodu2 = string.Empty,
            cha_HareketGrupKodu3 = string.Empty,
            cha_ebelgeno_seri = string.Empty,
            cha_ebelgeno_sira = 0,
            cha_hubid = string.Empty,
            cha_hubglbid = string.Empty,
            cha_disyazilimid = string.Empty,
            cha_disyazilim_tip = 0,
            cha_bsba_e_belge_mi = 0,
            cha_eticaret_kanal_kodu = string.Empty,
            cha_hizli_satis_kasa_no = Convert.ToInt16(Math.Clamp(request.CashNo, 0, short.MaxValue)),
            cha_ebelge_Islemturu = 0,
            cha_tevkifat_sifirlandi_fl = false,
            cha_vergi1 = 0d,
            cha_vergi2 = 0d,
            cha_vergi3 = 0d,
            cha_vergi4 = 0d,
            cha_vergi5 = 0d,
            cha_vergi6 = 0d,
            cha_vergi7 = 0d,
            cha_vergi8 = 0d,
            cha_vergi9 = 0d,
            cha_vergi10 = 0d,
            cha_vergi11 = 0d,
            cha_vergi12 = 0d,
            cha_vergi13 = 0d,
            cha_vergi14 = 0d,
            cha_vergi15 = 0d,
            cha_vergi16 = 0d,
            cha_vergi17 = 0d,
            cha_vergi18 = 0d,
            cha_vergi19 = 0d,
            cha_vergi20 = 0d,
            cha_ilave_edilecek_kdv1 = 0d,
            cha_ilave_edilecek_kdv2 = 0d,
            cha_ilave_edilecek_kdv3 = 0d,
            cha_ilave_edilecek_kdv4 = 0d,
            cha_ilave_edilecek_kdv5 = 0d,
            cha_ilave_edilecek_kdv6 = 0d,
            cha_ilave_edilecek_kdv7 = 0d,
            cha_ilave_edilecek_kdv8 = 0d,
            cha_ilave_edilecek_kdv9 = 0d,
            cha_ilave_edilecek_kdv10 = 0d,
            cha_ilave_edilecek_kdv11 = 0d,
            cha_ilave_edilecek_kdv12 = 0d,
            cha_ilave_edilecek_kdv13 = 0d,
            cha_ilave_edilecek_kdv14 = 0d,
            cha_ilave_edilecek_kdv15 = 0d,
            cha_ilave_edilecek_kdv16 = 0d,
            cha_ilave_edilecek_kdv17 = 0d,
            cha_ilave_edilecek_kdv18 = 0d,
            cha_ilave_edilecek_kdv19 = 0d,
            cha_ilave_edilecek_kdv20 = 0d,
            cha_efatura_belge_tipi = 0
        };
    }

    private static bool IsZero(double value) =>
        Math.Abs(value) < 0.005d;

    private static string NormalizeText(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }
}
