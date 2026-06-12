using System.Globalization;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

namespace FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

internal static class CompanyMovementIrsaliyeMikroApiPayloadFactory
{
    private const byte CompanyDispatchDocumentType = 1;
    private const byte OutgoingMovementType = 1;
    private const byte MovementGenre = 0;

    internal static CompanyMovementIrsaliyeMikroApiPayload Create(
        CreateCompanyMovementRequest request,
        IReadOnlyCollection<CreateCompanyMovementLineRequest> lines,
        string customerCode,
        int customerAddressNo,
        byte returnType,
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

                return new CompanyMovementIrsaliyeMikroApiLine(
                    FormatDate(movementDate),
                    OutgoingMovementType,
                    MovementGenre,
                    returnType,
                    CompanyDispatchDocumentType,
                    NormalizeText(documentSerie, 20),
                    documentOrderNo,
                    rowNo,
                    NormalizeText(documentNo, 50),
                    FormatDate(documentDate),
                    NormalizeText(line.StockCode, 25),
                    0,
                    NormalizeText(customerCode, 25),
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
                    0,
                    request.WarehouseNo,
                    FormatDate(movementDate),
                    NormalizeText(line.Description ?? description, 50),
                    customerAddressNo,
                    NormalizeText(line.PartyCode, 25),
                    line.LotNo,
                    NormalizeText(line.ProjectCode, 25),
                    NormalizeText(line.CustomerResponsibilityCenter, 25),
                    NormalizeText(line.ProductResponsibilityCenter, 25),
                    1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    FormatDate(movementDate),
                    string.Empty,
                    string.Empty,
                    [],
                    []);
            })
            .ToArray();

        var documentDescriptions = string.IsNullOrWhiteSpace(description)
            ? null
            : new[]
            {
                new CompanyMovementIrsaliyeMikroApiDocumentDescription(
                    NormalizeText(description, 127))
            };

        return new CompanyMovementIrsaliyeMikroApiPayload(
            [
                new CompanyMovementIrsaliyeMikroApiDocument(
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

internal sealed record CompanyMovementIrsaliyeMikroApiPayload(
    IReadOnlyCollection<CompanyMovementIrsaliyeMikroApiDocument> evraklar);

internal sealed record CompanyMovementIrsaliyeMikroApiDocument(
    IReadOnlyCollection<CompanyMovementIrsaliyeMikroApiLine> satirlar,
    IReadOnlyCollection<CompanyMovementIrsaliyeMikroApiDocumentDescription>? evrak_aciklamalari = null);

internal sealed record CompanyMovementIrsaliyeMikroApiDocumentDescription(
    string aciklama);

internal sealed record CompanyMovementIrsaliyeMikroApiLine(
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
    string sth_yetkili_uid,
    string sth_HareketGrupKodu1,
    string sth_HareketGrupKodu2,
    string sth_HareketGrupKodu3,
    string sth_teslim_tarihi,
    string seriler,
    IReadOnlyCollection<CompanyMovementIrsaliyeMikroApiColorSizeLine> renk_beden,
    IReadOnlyCollection<CompanyMovementIrsaliyeMikroApiUserTableLine> user_tablo);

internal sealed record CompanyMovementIrsaliyeMikroApiColorSizeLine;

internal sealed record CompanyMovementIrsaliyeMikroApiUserTableLine;
