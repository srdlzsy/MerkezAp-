using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar;

public sealed class VirmanWriteService(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<VirmanWriteService> logger)
{
    private const short MovementFileId = 16;
    private const short MikroUserNo = 39;
    private const byte VirmanDocumentType = 6;
    private const byte VirmanMovementGenre = 3;
    private const byte IncomingMovementType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte IncomingOutgoingMovementType = 2;
    private const byte NormalMovement = 0;
    private const int FirstDocumentOrderNo = 0;
    private const string DahiliStokHareketKaydetPath = "/Api/apiMethods/DahiliStokHareketKaydetV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;
    private const int StockSummaryDuplicateRetryAttemptCount = 3;
    private const int StockSummaryDuplicateRetryDelayMilliseconds = 150;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);
    private static readonly DateTime LegacyDeliveryDate = new(1900, 1, 1);

    public async Task<CreateVirmanResponse> ExecuteAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return mikroWriteRoutingOptions.CurrentValue.Virman switch
        {
            MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => await ExecuteMikroApiAsync(request, cancellationToken),
            MikroWriteMode.DualShadow => await ExecuteDualShadowAsync(request, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:Virman mode '{mode}'.")
        };
    }

    private async Task<CreateVirmanResponse> ExecuteDatabaseAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= StockSummaryDuplicateRetryAttemptCount; attempt++)
        {
            try
            {
                return await ExecuteDatabaseOnceAsync(request, cancellationToken);
            }
            catch (DbUpdateException exception) when (
                attempt < StockSummaryDuplicateRetryAttemptCount &&
                IsStockSummaryDuplicateKeyException(exception))
            {
                logger.LogWarning(
                    exception,
                    "Virman database write hit STOK_HAREKETLERI_OZET duplicate key. Retrying attempt {Attempt}/{AttemptCount}.",
                    attempt + 1,
                    StockSummaryDuplicateRetryAttemptCount);

                await Task.Delay(
                    TimeSpan.FromMilliseconds(StockSummaryDuplicateRetryDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        return await ExecuteDatabaseOnceAsync(request, cancellationToken);
    }

    private async Task<CreateVirmanResponse> ExecuteDatabaseOnceAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var documentSerie = $"F{request.WarehouseNo}";
        var documentNo = NormalizeText(request.DocumentNo, 50);
        var description = NormalizeText(request.Description, 50);
        var lines = request.Lines.ToArray();
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
                var movements = new List<STOK_HAREKETLERI>(lines.Length * 2);
                foreach (var line in lines)
                {
                    foreach (var movementType in ExpandMovementTypes(line.MovementType))
                    {
                        movements.Add(CreateMovement(
                            movementType,
                            request.WarehouseNo,
                            line,
                            description,
                            movements.Count,
                            now,
                            movementDate,
                            documentDate,
                            documentNo,
                            documentSerie,
                            documentOrderNo));
                    }
                }

                var movementRows = movements.ToArray();

                await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(movementRows, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateVirmanResponse(
                    documentSerie,
                    documentOrderNo,
                    movementDate,
                    documentDate,
                    documentNo,
                    request.WarehouseNo,
                    movementRows
                        .Select(movement => movement.sth_tip ?? 0)
                        .Distinct()
                        .OrderBy(movementType => movementType)
                        .ToArray(),
                    movementRows.Length,
                    movementRows.Sum(movement => movement.sth_miktar ?? 0d),
                    movementRows.Sum(movement => movement.sth_tutar ?? 0d),
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<CreateVirmanResponse> ExecuteMikroApiAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var documentSerie = $"F{request.WarehouseNo}";
        var documentNo = NormalizeText(request.DocumentNo, 50);
        var description = NormalizeText(request.Description, 50);
        var lines = request.Lines.ToArray();
        var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
        var payload = StockMovementMikroApiPayloadFactory.CreateVirman(
            request,
            lines,
            movementDate,
            documentDate,
            documentNo,
            documentSerie,
            documentOrderNo,
            description);

        logger.LogInformation(
            "Virman create is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, WarehouseNo={WarehouseNo}, LineCount={LineCount}",
            DahiliStokHareketKaydetPath,
            documentSerie,
            documentOrderNo,
            request.WarehouseNo,
            lines.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            DahiliStokHareketKaydetPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API virman create failed.");
        }

        var recovered = await RecoverMikroApiCreateResponseAsync(
            documentSerie,
            documentOrderNo,
            request,
            movementDate,
            documentDate,
            documentNo,
            options.ConnectionStringName,
            cancellationToken);

        await mikroApiClient.MarkRecoveredAsync(
            result,
            recovered.DocumentNo,
            cancellationToken: cancellationToken);
        return recovered;
    }

    private async Task<CreateVirmanResponse> ExecuteDualShadowAsync(
        CreateVirmanRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:Virman is DualShadow. DahiliStokHareketKaydetV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, cancellationToken);
    }

    private async Task<CreateVirmanResponse> RecoverMikroApiCreateResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateVirmanRequest request,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string writeConnectionName,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverVirmanResponseAsync(
                documentSerie,
                documentOrderNo,
                request,
                movementDate,
                documentDate,
                documentNo,
                writeConnectionName,
                cancellationToken);

            if (response is not null)
            {
                return response;
            }

            if (attempt < MikroApiRecoveryAttemptCount)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(MikroApiRecoveryDelayMilliseconds * attempt),
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "Mikro API virman create succeeded, but created STOK_HAREKETLERI rows could not be read back.");
    }

    private async Task<CreateVirmanResponse?> TryRecoverVirmanResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateVirmanRequest request,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string writeConnectionName,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == VirmanDocumentType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cins == VirmanMovementGenre &&
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo &&
                movement.sth_cikis_depo_no == request.WarehouseNo)
            .Select(movement => new
            {
                movement.sth_tarih,
                movement.sth_belge_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                movement.sth_tip,
                movement.sth_miktar,
                movement.sth_tutar
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cikis_depo_no
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one virman document matched the same serie and order number.");
        }

        var firstRow = rows[0];

        return new CreateVirmanResponse(
            firstRow.sth_evrakno_seri ?? documentSerie,
            firstRow.sth_evrakno_sira ?? documentOrderNo,
            firstRow.sth_tarih?.Date ?? movementDate,
            firstRow.sth_belge_tarih?.Date ?? documentDate,
            firstRow.sth_belge_no ?? documentNo,
            firstRow.sth_cikis_depo_no ?? request.WarehouseNo,
            rows
                .Select(row => row.sth_tip ?? 0)
                .Distinct()
                .OrderBy(movementType => movementType)
                .ToArray(),
            rows.Count,
            rows.Sum(row => row.sth_miktar ?? 0d),
            rows.Sum(row => row.sth_tutar ?? 0d),
            writeConnectionName);
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == VirmanDocumentType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cins == VirmanMovementGenre &&
                movement.sth_evrakno_seri == documentSerie)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private static STOK_HAREKETLERI CreateMovement(
        byte movementType,
        int warehouseNo,
        CreateVirmanLineRequest line,
        string description,
        int rowNo,
        DateTime now,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string documentSerie,
        int documentOrderNo) =>
        new()
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_SpecRECno = 0,
            sth_iptal = false,
            sth_fileid = MovementFileId,
            sth_hidden = false,
            sth_kilitli = false,
            sth_degisti = false,
            sth_checksum = 0,
            sth_create_user = MikroUserNo,
            sth_create_date = now,
            sth_lastup_user = MikroUserNo,
            sth_lastup_date = now,
            sth_special1 = string.Empty,
            sth_special2 = string.Empty,
            sth_special3 = string.Empty,
            sth_firmano = 0,
            sth_subeno = 0,
            sth_tarih = movementDate,
            sth_tip = movementType,
            sth_cins = VirmanMovementGenre,
            sth_normal_iade = NormalMovement,
            sth_evraktip = VirmanDocumentType,
            sth_evrakno_seri = documentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = documentNo,
            sth_belge_tarih = documentDate,
            sth_stok_kod = NormalizeText(line.StockCode, 25),
            sth_isk_mas1 = 0,
            sth_isk_mas2 = 0,
            sth_isk_mas3 = 0,
            sth_isk_mas4 = 0,
            sth_isk_mas5 = 0,
            sth_isk_mas6 = 0,
            sth_isk_mas7 = 0,
            sth_isk_mas8 = 0,
            sth_isk_mas9 = 0,
            sth_isk_mas10 = 0,
            sth_sat_iskmas1 = false,
            sth_sat_iskmas2 = false,
            sth_sat_iskmas3 = false,
            sth_sat_iskmas4 = false,
            sth_sat_iskmas5 = false,
            sth_sat_iskmas6 = false,
            sth_sat_iskmas7 = false,
            sth_sat_iskmas8 = false,
            sth_sat_iskmas9 = false,
            sth_sat_iskmas10 = false,
            sth_pos_satis = 0,
            sth_promosyon_fl = false,
            sth_cari_cinsi = 0,
            sth_cari_kodu = string.Empty,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = line.Quantity,
            sth_miktar2 = 0d,
            sth_birim_pntr = Convert.ToByte(line.UnitPointer),
            sth_tutar = 0d,
            sth_iskonto1 = 0d,
            sth_iskonto2 = 0d,
            sth_iskonto3 = 0d,
            sth_iskonto4 = 0d,
            sth_iskonto5 = 0d,
            sth_iskonto6 = 0d,
            sth_masraf1 = 0d,
            sth_masraf2 = 0d,
            sth_masraf3 = 0d,
            sth_masraf4 = 0d,
            sth_vergi_pntr = 0,
            sth_vergi = 0d,
            sth_masraf_vergi_pntr = 0,
            sth_masraf_vergi = 0d,
            sth_netagirlik = 0d,
            sth_odeme_op = 0,
            sth_aciklama = NormalizeText(line.Description ?? description, 50),
            sth_sip_uid = Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = warehouseNo,
            sth_cikis_depo_no = warehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = string.Empty,
            sth_stok_srm_merkezi = string.Empty,
            sth_fis_tarihi = MikroEmptyDate,
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = 1,
            sth_parti_kodu = NormalizeText(line.PartyCode, 25),
            sth_lot_no = line.LotNo,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = NormalizeText(line.ProjectCode, 25),
            sth_exim_kodu = string.Empty,
            sth_otv_pntr = 0,
            sth_otv_vergi = 0d,
            sth_brutagirlik = 0d,
            sth_disticaret_turu = 0,
            sth_otvtutari = 0d,
            sth_otvvergisiz_fl = false,
            sth_oiv_pntr = 0,
            sth_oiv_vergi = 0d,
            sth_oivvergisiz_fl = false,
            sth_fiyat_liste_no = -1,
            sth_oivtutari = 0d,
            sth_Tevkifat_turu = 0,
            sth_nakliyedeposu = 0,
            sth_nakliyedurumu = 0,
            sth_yetkili_uid = Guid.Empty,
            sth_taxfree_fl = false,
            sth_ilave_edilecek_kdv = 0d,
            sth_ismerkezi_kodu = string.Empty,
            sth_HareketGrupKodu1 = string.Empty,
            sth_HareketGrupKodu2 = string.Empty,
            sth_HareketGrupKodu3 = string.Empty,
            sth_Olcu1 = 0d,
            sth_Olcu2 = 0d,
            sth_Olcu3 = 0d,
            sth_Olcu4 = 0d,
            sth_Olcu5 = 0d,
            sth_FormulMiktarNo = 0,
            sth_FormulMiktar = 0d,
            sth_eirs_senaryo = 0,
            sth_eirs_tipi = 0,
            sth_teslim_tarihi = LegacyDeliveryDate,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = string.Empty,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };

    private static void Validate(CreateVirmanRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.DocumentDate.HasValue &&
            request.MovementDate.HasValue &&
            request.DocumentDate.Value.Date < request.MovementDate.Value.Date)
        {
            throw new ArgumentException("Document date can not be earlier than movement date.", nameof(request.DocumentDate));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one virman line is required.", nameof(request.Lines));
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.StockCode))
            {
                throw new ArgumentException("Stock code is required.", nameof(request.Lines));
            }

            if (line.Quantity <= 0)
            {
                throw new ArgumentException("Line quantity must be greater than zero.", nameof(request.Lines));
            }

            if (line.UnitPointer is < 1 or > byte.MaxValue)
            {
                throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(request.Lines));
            }

            if (line.MovementType is not (IncomingMovementType or OutgoingMovementType or IncomingOutgoingMovementType))
            {
                throw new ArgumentException("Virman movement type must be 0, 1, or 2.", nameof(request.Lines));
            }

            if (line.LotNo < 0)
            {
                throw new ArgumentException("Line lot no can not be negative.", nameof(request.Lines));
            }
        }
    }

    private static IEnumerable<byte> ExpandMovementTypes(byte movementType)
    {
        if (movementType == IncomingOutgoingMovementType)
        {
            yield return OutgoingMovementType;
            yield return IncomingMovementType;
            yield break;
        }

        yield return movementType;
    }

    private static string NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static bool IsStockSummaryDuplicateKeyException(DbUpdateException exception)
    {
        var sqlException = exception.GetBaseException() as SqlException;
        if (sqlException is null || sqlException.Number is not (2601 or 2627))
        {
            return false;
        }

        return sqlException.Message.Contains(
            "STOK_HAREKETLERI_OZET",
            StringComparison.OrdinalIgnoreCase);
    }
}
