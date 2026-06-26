using System.Globalization;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

internal static class StockMovementMikroApiPayloadFactory
{
    private const byte StockReceiptDocumentType = 0;
    private const byte VirmanDocumentType = 6;
    private const byte IncomingMovementType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte IncomingOutgoingMovementType = 2;
    private const byte VirmanMovementGenre = 3;
    private const byte NormalMovement = 0;
    private static readonly DateTime VirmanLegacyDeliveryDate = new(1900, 1, 1);

    internal static StockMovementMikroApiPayload CreateStockReceipt(
        CreateStockReceiptRequest request,
        IReadOnlyCollection<CreateStockReceiptLineRequest> lines,
        byte movementGenre,
        string workOrderExpenseCode,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string documentSerie,
        int documentOrderNo,
        string creator,
        string acceptor,
        string description)
    {
        var satirlar = lines
            .Select((line, rowNo) => new StockMovementMikroApiLine(
                FormatDate(movementDate),
                OutgoingMovementType,
                movementGenre,
                NormalMovement,
                StockReceiptDocumentType,
                NormalizeText(documentSerie, 20),
                documentOrderNo,
                rowNo,
                NormalizeText(documentNo, 50),
                FormatDate(documentDate),
                NormalizeText(line.StockCode, 25),
                0,
                string.Empty,
                NormalizeText(workOrderExpenseCode, 25),
                line.Quantity,
                0d,
                line.UnitPointer,
                0d,
                0,
                0d,
                false,
                0,
                1,
                0,
                request.WarehouseNo,
                NormalizeText(line.Description ?? description, 50),
                NormalizeText(line.PartyCode, 25),
                line.LotNo,
                NormalizeText(line.ProjectCode, 25),
                1,
                NormalizeText(creator, 25),
                NormalizeText(acceptor, 25),
                string.Empty,
                FormatDate(movementDate)))
            .ToArray();

        return new StockMovementMikroApiPayload(
            [
                new StockMovementMikroApiDocument(satirlar)
            ]);
    }

    internal static StockMovementMikroApiPayload CreateVirman(
        CreateVirmanRequest request,
        IReadOnlyCollection<CreateVirmanLineRequest> lines,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string documentSerie,
        int documentOrderNo,
        string description)
    {
        var satirlar = new List<StockMovementMikroApiLine>(lines.Count * 2);
        foreach (var line in lines)
        {
            foreach (var movementType in ExpandVirmanMovementTypes(line.MovementType))
            {
                satirlar.Add(new StockMovementMikroApiLine(
                    FormatDate(movementDate),
                    movementType,
                    VirmanMovementGenre,
                    NormalMovement,
                    VirmanDocumentType,
                    NormalizeText(documentSerie, 20),
                    documentOrderNo,
                    satirlar.Count,
                    NormalizeText(documentNo, 50),
                    FormatDate(documentDate),
                    NormalizeText(line.StockCode, 25),
                    0,
                    string.Empty,
                    string.Empty,
                    line.Quantity,
                    0d,
                    line.UnitPointer,
                    0d,
                    0,
                    0d,
                    false,
                    0,
                    0,
                    request.WarehouseNo,
                    request.WarehouseNo,
                    NormalizeText(line.Description ?? description, 50),
                    NormalizeText(line.PartyCode, 25),
                    line.LotNo,
                    NormalizeText(line.ProjectCode, 25),
                    -1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    FormatDate(VirmanLegacyDeliveryDate)));
            }
        }

        return new StockMovementMikroApiPayload(
            [
                new StockMovementMikroApiDocument(satirlar.ToArray())
            ]);
    }

    private static IEnumerable<byte> ExpandVirmanMovementTypes(byte movementType)
    {
        if (movementType == IncomingOutgoingMovementType)
        {
            yield return OutgoingMovementType;
            yield return IncomingMovementType;
            yield break;
        }

        yield return movementType;
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

internal sealed record StockMovementMikroApiPayload(
    IReadOnlyCollection<StockMovementMikroApiDocument> evraklar);

internal sealed record StockMovementMikroApiDocument(
    IReadOnlyCollection<StockMovementMikroApiLine> satirlar);

internal sealed record StockMovementMikroApiLine(
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
    int sth_isk_mas1,
    int sth_isk_mas2,
    int sth_giris_depo_no,
    int sth_cikis_depo_no,
    string sth_aciklama,
    string sth_parti_kodu,
    int sth_lot_no,
    string sth_proje_kodu,
    int sth_fiyat_liste_no,
    string sth_HareketGrupKodu1,
    string sth_HareketGrupKodu2,
    string sth_HareketGrupKodu3,
    string sth_teslim_tarihi);
