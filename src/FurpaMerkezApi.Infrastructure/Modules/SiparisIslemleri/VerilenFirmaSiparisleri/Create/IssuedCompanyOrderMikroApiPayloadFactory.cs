using System.Globalization;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;

internal static class IssuedCompanyOrderMikroApiPayloadFactory
{
    private const byte IssuedOrderType = 1;
    private const byte NormalOrderGenre = 0;

    internal static IssuedCompanyOrderMikroApiPayload Create(
        CreateIssuedCompanyOrderRequest request,
        IReadOnlyCollection<CreateIssuedCompanyOrderLineRequest> lines,
        int customerPaymentPlanNo,
        bool customerCanBeCalled,
        DateTime orderDate,
        DateTime deliveryDate,
        string documentSerie,
        int documentOrderNo)
    {
        var customerCode = NormalizeText(request.CustomerCode, 25);
        var satirlar = lines
            .Select((line, rowNo) =>
            {
                var unitPrice = line.UnitPrice;
                var quantity = line.Quantity;
                var amount = quantity * unitPrice;

                return new IssuedCompanyOrderMikroApiLine(
                    FormatDate(orderDate),
                    FormatDate(deliveryDate),
                    IssuedOrderType,
                    NormalOrderGenre,
                    NormalizeText(documentSerie, 20),
                    documentOrderNo,
                    rowNo,
                    string.Empty,
                    FormatDate(orderDate),
                    string.Empty,
                    customerCode,
                    NormalizeText(line.StockCode, 25),
                    unitPrice,
                    quantity,
                    line.UnitPointer,
                    0d,
                    amount,
                    2,
                    0d,
                    0,
                    0d,
                    customerPaymentPlanNo,
                    NormalizeText(line.Description1 ?? request.Description1, 50),
                    NormalizeText(line.Description2 ?? request.Description2, 50),
                    request.WarehouseNo,
                    false,
                    false,
                    false,
                    NormalizeText(line.CustomerResponsibilityCenter, 25),
                    NormalizeText(line.ProductResponsibilityCenter, 25),
                    customerCanBeCalled,
                    NormalizeText(line.ProjectCode, 25),
                    -1,
                    NormalizeText(line.PackageCode, 25),
                    string.Empty,
                    line.RecommendedQuantity ?? 0d,
                    0d,
                    NormalizeText(request.Deliverer, 25),
                    NormalizeText(request.Receiver, 25));
            })
            .ToArray();

        return new IssuedCompanyOrderMikroApiPayload(
            [
                new IssuedCompanyOrderMikroApiDocument(satirlar)
            ]);
    }

    private static string FormatDate(DateTime value) =>
        value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

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

internal sealed record IssuedCompanyOrderMikroApiPayload(
    IReadOnlyCollection<IssuedCompanyOrderMikroApiDocument> evraklar);

internal sealed record IssuedCompanyOrderMikroApiDocument(
    IReadOnlyCollection<IssuedCompanyOrderMikroApiLine> satirlar);

internal sealed record IssuedCompanyOrderMikroApiLine(
    string sip_tarih,
    string sip_teslim_tarih,
    byte sip_tip,
    byte sip_cins,
    string sip_evrakno_seri,
    int sip_evrakno_sira,
    int sip_satirno,
    string sip_belgeno,
    string sip_belge_tarih,
    string sip_satici_kod,
    string sip_musteri_kod,
    string sip_stok_kod,
    double sip_b_fiyat,
    double sip_miktar,
    int sip_birim_pntr,
    double sip_teslim_miktar,
    double sip_tutar,
    int sip_vergi_pntr,
    double sip_vergi,
    int sip_masvergi_pntr,
    double sip_masvergi,
    int sip_opno,
    string sip_aciklama,
    string sip_aciklama2,
    int sip_depono,
    bool sip_vergisiz_fl,
    bool sip_kapat_fl,
    bool sip_promosyon_fl,
    string sip_cari_sormerk,
    string sip_stok_sormerk,
    bool sip_cagrilabilir_fl,
    string sip_projekodu,
    int sip_fiyat_liste_no,
    string sip_paket_kod,
    string sip_gecerlilik_tarihi,
    double sip_rezervasyon_miktari,
    double sip_rezerveden_teslim_edilen,
    string sip_HareketGrupKodu2,
    string sip_HareketGrupKodu3);
