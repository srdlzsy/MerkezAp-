using System.Globalization;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

internal static class IssuedWarehouseOrderMikroApiPayloadFactory
{
    internal static IssuedWarehouseOrderMikroApiPayload Create(
        CreateIssuedWarehouseOrderRequest request,
        IReadOnlyCollection<CreateIssuedWarehouseOrderLineRequest> lines,
        DateTime orderDate,
        DateTime deliveryDate,
        string documentSerie,
        int documentOrderNo)
    {
        var satirlar = lines
            .Select((line, rowNo) =>
            {
                var unitPrice = line.UnitPrice;
                var quantity = line.Quantity;
                var amount = quantity * unitPrice;

                return new IssuedWarehouseOrderMikroApiLine(
                    FormatDate(orderDate),
                    FormatDate(deliveryDate),
                    FormatDate(orderDate),
                    string.Empty,
                    NormalizeText(documentSerie, 20),
                    documentOrderNo,
                    rowNo,
                    NormalizeText(line.StockCode, 25),
                    unitPrice,
                    quantity,
                    amount,
                    0d,
                    request.InWarehouseNo,
                    request.OutWarehouseNo,
                    NormalizeText(line.Description ?? request.Description, 50),
                    line.UnitPointer,
                    NormalizeText(line.PackageCode, 25),
                    NormalizeText(line.ProjectCode, 25),
                    NormalizeText(line.ResponsibilityCenter, 25),
                    string.Empty,
                    line.RecommendedQuantity ?? 0d);
            })
            .ToArray();

        return new IssuedWarehouseOrderMikroApiPayload(
            [
                new IssuedWarehouseOrderMikroApiDocument(satirlar)
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

internal sealed record IssuedWarehouseOrderMikroApiPayload(
    IReadOnlyCollection<IssuedWarehouseOrderMikroApiDocument> evraklar);

internal sealed record IssuedWarehouseOrderMikroApiDocument(
    IReadOnlyCollection<IssuedWarehouseOrderMikroApiLine> satirlar);

internal sealed record IssuedWarehouseOrderMikroApiLine(
    string ssip_tarih,
    string ssip_teslim_tarih,
    string ssip_belge_tarih,
    string ssip_belgeno,
    string ssip_evrakno_seri,
    int ssip_evrakno_sira,
    int ssip_satirno,
    string ssip_stok_kod,
    double ssip_b_fiyat,
    double ssip_miktar,
    double ssip_tutar,
    double ssip_teslim_miktar,
    int ssip_girdepo,
    int ssip_cikdepo,
    string ssip_aciklama,
    int ssip_birim_pntr,
    string ssip_paket_kod,
    string ssip_projekodu,
    string ssip_sormerkezi,
    string ssip_gecerlilik_tarihi,
    double ssip_rezervasyon_miktari);
