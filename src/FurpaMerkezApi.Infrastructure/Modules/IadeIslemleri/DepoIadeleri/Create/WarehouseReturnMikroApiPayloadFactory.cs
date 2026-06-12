using System.Globalization;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Create;

internal static class WarehouseReturnMikroApiPayloadFactory
{
    private const byte MovementType = 2;
    private const byte MovementGenre = 6;
    private const byte ReturnMovement = 1;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte WaitingShippingState = 0;

    internal static WarehouseReturnMikroApiPayload Create(
        CreateWarehouseReturnRequest request,
        IReadOnlyCollection<CreateWarehouseReturnLineRequest> lines,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string documentSerie,
        int documentOrderNo,
        string description)
    {
        var satirlar = lines
            .Select((line, rowNo) =>
            {
                var amount = line.Quantity * line.UnitPrice;

                return new WarehouseReturnMikroApiLine(
                    FormatDate(movementDate),
                    MovementType,
                    MovementGenre,
                    ReturnMovement,
                    InterWarehouseShipmentDocumentType,
                    NormalizeText(documentSerie, 20),
                    documentOrderNo,
                    rowNo,
                    NormalizeText(documentNo, 50),
                    FormatDate(documentDate),
                    NormalizeText(line.StockCode, 25),
                    0,
                    string.Empty,
                    string.Empty,
                    line.Quantity,
                    0d,
                    line.UnitPointer,
                    amount,
                    0,
                    0d,
                    false,
                    0d,
                    0d,
                    0,
                    1,
                    request.TransitWarehouseNo,
                    request.SourceWarehouseNo,
                    FormatDate(movementDate),
                    NormalizeText(line.Description ?? description, 50),
                    NormalizeText(line.CustomerResponsibilityCenter, 25),
                    NormalizeText(line.ProductResponsibilityCenter, 25),
                    NormalizeText(line.PartyCode, 25),
                    line.LotNo,
                    NormalizeText(line.ProjectCode, 25),
                    -1,
                    request.TargetWarehouseNo,
                    WaitingShippingState,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    FormatDate(movementDate),
                    string.Empty,
                    [],
                    []);
            })
            .ToArray();

        var documentDescriptions = string.IsNullOrWhiteSpace(description)
            ? null
            : new[]
            {
                new WarehouseReturnMikroApiDocumentDescription(
                    NormalizeText(description, 127))
            };

        return new WarehouseReturnMikroApiPayload(
            [
                new WarehouseReturnMikroApiDocument(
                    satirlar,
                    documentDescriptions)
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

internal sealed record WarehouseReturnMikroApiPayload(
    IReadOnlyCollection<WarehouseReturnMikroApiDocument> evraklar);

internal sealed record WarehouseReturnMikroApiDocument(
    IReadOnlyCollection<WarehouseReturnMikroApiLine> satirlar,
    IReadOnlyCollection<WarehouseReturnMikroApiDocumentDescription>? evrak_aciklamalari = null);

internal sealed record WarehouseReturnMikroApiDocumentDescription(
    string aciklama);

internal sealed record WarehouseReturnMikroApiLine(
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
    string sth_cari_srm_merkezi,
    string sth_stok_srm_merkezi,
    string sth_parti_kodu,
    int sth_lot_no,
    string sth_proje_kodu,
    int sth_fiyat_liste_no,
    int sth_nakliyedeposu,
    byte sth_nakliyedurumu,
    string sth_yetkili_uid,
    string sth_HareketGrupKodu1,
    string sth_HareketGrupKodu2,
    string sth_HareketGrupKodu3,
    string sth_teslim_tarihi,
    string seriler,
    IReadOnlyCollection<WarehouseReturnMikroApiColorSizeLine> renk_beden,
    IReadOnlyCollection<WarehouseReturnMikroApiUserTableLine> user_tablo);

internal sealed record WarehouseReturnMikroApiColorSizeLine;

internal sealed record WarehouseReturnMikroApiUserTableLine;
