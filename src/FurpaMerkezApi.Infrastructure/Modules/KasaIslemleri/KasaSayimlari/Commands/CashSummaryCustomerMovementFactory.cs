using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;

internal static class CashSummaryCustomerMovementFactory
{
    internal const string CustomerMovementDocumentSerie = "X";
    internal const byte CustomerMovementDocumentType = 60;
    internal const string ZDifferenceAccountCode = "0002";
    internal const string ZReportDocumentName = "Z Raporu";
    internal const int CashPaymentTypeNo = 200;
    internal const int ZDifferencePaymentTypeNo = 300;
    internal const int ZReportTotalPaymentTypeNo = 400;
    internal const int MainLineNo = 0;

    private const short MikroUserNo = 39;
    private const short CustomerMovementFileId = 51;
    private const byte CustomerMovementDebitType = 0;
    private const byte CustomerMovementCreditType = 1;
    private const byte CustomerMovementGenre = 5;
    private const byte CustomerMovementNormalReturn = 0;
    private const byte CustomerMovementTpoz = 0;
    private const byte CustomerMovementTradeType = 0;
    private const byte BankCustomerGenus = 2;
    private const byte FoodCheckCustomerGenus = 0;
    private const byte NegativeDifferenceCustomerGenus = 1;
    private const byte CashCustomerGenus = 4;
    private const byte CustomerMovementInvoiceDocumentType = 3;
    private const double NegativeDifferenceThreshold = -3.50d;
    private static readonly DateTime MikroEmptyDate = new(1900, 1, 1);

    internal static string BuildLegacyDescription(string documentSerie, int documentOrderNo) =>
        $"{documentSerie}.{documentOrderNo}";

    internal static IReadOnlyCollection<CARI_HESAP_HAREKETLERI> CreateMovements(
        SummaryEntity header,
        IEnumerable<CashSummaryCustomerMovementLine> paymentLines,
        double zTotalValue,
        double documentTotal,
        int customerMovementDocumentOrderNo,
        DateTime now)
    {
        var movementLines = paymentLines
            .Concat(CreateZReportLines(header.WarehouseNo, zTotalValue, documentTotal))
            .Where(ShouldWriteLine)
            .ToArray();
        var movements = new List<CARI_HESAP_HAREKETLERI>(movementLines.Length);
        var description = BuildLegacyDescription(header.DocumentSerie, header.DocumentOrderNo);

        for (var rowNo = 0; rowNo < movementLines.Length; rowNo++)
        {
            movements.Add(CreateMovement(
                header,
                movementLines[rowNo],
                customerMovementDocumentOrderNo,
                rowNo,
                description,
                now));
        }

        return movements;
    }

    internal static double ResolveExistingZTotalValue(
        IEnumerable<CARI_HESAP_HAREKETLERI> movements,
        int warehouseNo)
    {
        var warehouseCode = warehouseNo.ToString();
        var zTotalMovement = movements
            .Where(item => item.cha_evrak_tip == CustomerMovementDocumentType)
            .Where(item => item.cha_tip == CustomerMovementCreditType)
            .Where(item => item.cha_cari_cins == CashCustomerGenus)
            .FirstOrDefault(item => string.Equals(item.cha_kod, warehouseCode, StringComparison.OrdinalIgnoreCase));

        return zTotalMovement?.cha_meblag is > 0d
            ? Math.Round(zTotalMovement.cha_meblag.Value, 2)
            : 0d;
    }

    private static IEnumerable<CashSummaryCustomerMovementLine> CreateZReportLines(
        int warehouseNo,
        double zTotalValue,
        double documentTotal)
    {
        yield return new CashSummaryCustomerMovementLine(
            ZDifferencePaymentTypeNo,
            ZDifferenceAccountCode,
            Math.Round(documentTotal - zTotalValue, 2));

        yield return new CashSummaryCustomerMovementLine(
            ZReportTotalPaymentTypeNo,
            warehouseNo.ToString(),
            Math.Round(zTotalValue, 2));
    }

    private static bool ShouldWriteLine(CashSummaryCustomerMovementLine line) =>
        !string.IsNullOrWhiteSpace(line.AccountCode) &&
        !IsZero(line.Amount);

    private static CARI_HESAP_HAREKETLERI CreateMovement(
        SummaryEntity header,
        CashSummaryCustomerMovementLine line,
        int customerMovementDocumentOrderNo,
        int rowNo,
        string description,
        DateTime now)
    {
        var warehouseCode = header.WarehouseNo.ToString();
        var isNegativeSyntheticDifference = line.PaymentTypeNo >= 100 && line.Amount <= NegativeDifferenceThreshold;
        var customerCode = isNegativeSyntheticDifference
            ? header.CashierNo.ToString()
            : NormalizeText(line.AccountCode, 25);

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
            cha_evrakno_seri = CustomerMovementDocumentSerie,
            cha_evrakno_sira = customerMovementDocumentOrderNo,
            cha_satir_no = rowNo,
            cha_tarihi = header.SummaryDate.Date,
            cha_tip = IsCreditLine(line.PaymentTypeNo) ? CustomerMovementCreditType : CustomerMovementDebitType,
            cha_cinsi = CustomerMovementGenre,
            cha_normal_Iade = CustomerMovementNormalReturn,
            cha_tpoz = CustomerMovementTpoz,
            cha_ticaret_turu = CustomerMovementTradeType,
            cha_belge_no = string.Empty,
            cha_belge_tarih = header.SummaryDate.Date,
            cha_aciklama = NormalizeText(description, 40),
            cha_satici_kodu = string.Empty,
            cha_EXIMkodu = string.Empty,
            cha_projekodu = string.Empty,
            cha_yat_tes_kodu = string.Empty,
            cha_cari_cins = ResolveCustomerGenus(line, isNegativeSyntheticDifference),
            cha_kod = customerCode,
            cha_ciro_cari_kodu = string.Empty,
            cha_d_cins = 0,
            cha_d_kur = 1d,
            cha_altd_kur = 1d,
            cha_grupno = line.PaymentTypeNo < 50 ? (byte)7 : (byte)0,
            cha_srmrkkodu = warehouseCode,
            cha_kasa_hizmet = 0,
            cha_kasa_hizkod = string.Empty,
            cha_karsidcinsi = 0,
            cha_karsid_kur = 1d,
            cha_karsidgrupno = 0,
            cha_karsisrmrkkodu = warehouseCode,
            cha_miktari = 0d,
            cha_meblag = Math.Round(line.Amount, 2),
            cha_aratoplam = 0d,
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
            cha_reftarihi = MikroEmptyDate,
            cha_istisnakodu = 0,
            cha_pos_hareketi = 0,
            cha_meblag_ana_doviz_icin_gecersiz_fl = 0,
            cha_meblag_alt_doviz_icin_gecersiz_fl = 0,
            cha_meblag_orj_doviz_icin_gecersiz_fl = 0,
            cha_sip_uid = Guid.Empty,
            cha_kirahar_uid = Guid.Empty,
            cha_vardiya_tarihi = MikroEmptyDate,
            cha_vardiya_no = 0,
            cha_vardiya_evrak_ti = 0,
            cha_ebelge_turu = 0,
            cha_tevkifat_toplam = 0d,
            cha_e_islem_turu = 0,
            cha_fatura_belge_turu = CustomerMovementInvoiceDocumentType,
            cha_diger_belge_adi = ZReportDocumentName,
            cha_uuid = string.Empty,
            cha_adres_no = 0,
            cha_vergifon_toplam = 0d,
            cha_ilk_belge_tarihi = MikroEmptyDate,
            cha_ilk_belge_doviz_kuru = 0d,
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
            cha_hizli_satis_kasa_no = 0,
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
            cha_ilave_edilecek_kdv10 = 0d
        };
    }

    private static bool IsCreditLine(int paymentTypeNo) =>
        paymentTypeNo is ZDifferencePaymentTypeNo or ZReportTotalPaymentTypeNo;

    private static byte ResolveCustomerGenus(
        CashSummaryCustomerMovementLine line,
        bool isNegativeDifference)
    {
        if (line.PaymentTypeNo < 50)
        {
            return BankCustomerGenus;
        }

        if (line.PaymentTypeNo < 100)
        {
            return FoodCheckCustomerGenus;
        }

        return isNegativeDifference
            ? NegativeDifferenceCustomerGenus
            : CashCustomerGenus;
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static bool IsZero(double value) =>
        Math.Abs(value) < 0.000_001d;
}

internal sealed record CashSummaryCustomerMovementLine(
    int PaymentTypeNo,
    string AccountCode,
    double Amount);
