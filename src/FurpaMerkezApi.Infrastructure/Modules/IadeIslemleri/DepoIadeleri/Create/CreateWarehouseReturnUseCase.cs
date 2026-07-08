using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Create;

public sealed class CreateWarehouseReturnUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptions<AxataSynchronizationOptions> axataOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<CreateWarehouseReturnUseCase> logger)
    : ICreateWarehouseReturnUseCase
{
    private const short MovementFileId = 16;
    private const short MikroUserNo = 39;
    private const byte MovementType = 2;
    private const byte MovementGenre = 6;
    private const byte ReturnMovement = 1;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte WaitingShippingState = 0;
    private const int FirstDocumentOrderNo = 0;
    private const string DahiliStokHareketKaydetPath = "/Api/apiMethods/DahiliStokHareketKaydetV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<CreateWarehouseReturnResponse> ExecuteAsync(
        CreateWarehouseReturnRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return mikroWriteRoutingOptions.CurrentValue.WarehouseReturn switch
        {
            MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => await ExecuteMikroApiAsync(request, cancellationToken),
            MikroWriteMode.DualShadow => await ExecuteDualShadowAsync(request, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:WarehouseReturn mode '{mode}'.")
        };
    }

    private async Task<CreateWarehouseReturnResponse> ExecuteDatabaseAsync(
        CreateWarehouseReturnRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var documentSerie = $"F{request.SourceWarehouseNo}";
        var documentNo = NormalizeText(request.DocumentNo);
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
                var automaticOrderLines = await CreateAutomaticWarehouseOrderLinesAsync(
                    request,
                    lines,
                    movementDate,
                    now,
                    cancellationToken);
                var movements = new List<STOK_HAREKETLERI>(lines.Length);
                var movementExtras = new List<STOK_HAREKETLERI_EK>();

                for (var rowNo = 0; rowNo < lines.Length; rowNo++)
                {
                    var movement = CreateMovement(
                        request,
                        lines[rowNo],
                        rowNo,
                        now,
                        movementDate,
                        documentDate,
                        documentNo,
                        documentSerie,
                        documentOrderNo);

                    movements.Add(movement);

                    if (automaticOrderLines.TryGetValue(rowNo, out var automaticOrderLine))
                    {
                        movementExtras.Add(AutomaticWarehouseOrderFactory.CreateMovementExtra(
                            movement.sth_Guid,
                            automaticOrderLine.ssip_Guid,
                            now));
                    }
                }

                if (automaticOrderLines.Count > 0)
                {
                    await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.AddRangeAsync(
                        automaticOrderLines.Values,
                        cancellationToken);
                }

                await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(movements, cancellationToken);
                if (movementExtras.Count > 0)
                {
                    await mikroWriteDbContext.STOK_HAREKETLERI_EKs.AddRangeAsync(movementExtras, cancellationToken);
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateWarehouseReturnResponse(
                    documentSerie,
                    documentOrderNo,
                    movementDate,
                    documentDate,
                    documentNo,
                    request.SourceWarehouseNo,
                    request.TargetWarehouseNo,
                    request.TransitWarehouseNo,
                    movements.Count,
                    movements.Sum(movement => movement.sth_miktar ?? 0d),
                    movements.Sum(movement => movement.sth_tutar ?? 0d),
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<CreateWarehouseReturnResponse> ExecuteMikroApiAsync(
        CreateWarehouseReturnRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var documentSerie = $"F{request.SourceWarehouseNo}";
        var documentNo = NormalizeText(request.DocumentNo);
        var description = NormalizeText(request.Description);
        var lines = request.Lines.ToArray();
        var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
        var payload = WarehouseReturnMikroApiPayloadFactory.Create(
            request,
            lines,
            movementDate,
            documentDate,
            documentNo,
            documentSerie,
            documentOrderNo,
            description);

        logger.LogInformation(
            "Warehouse return create is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, SourceWarehouseNo={SourceWarehouseNo}, TargetWarehouseNo={TargetWarehouseNo}, TransitWarehouseNo={TransitWarehouseNo}, LineCount={LineCount}",
            DahiliStokHareketKaydetPath,
            documentSerie,
            documentOrderNo,
            request.SourceWarehouseNo,
            request.TargetWarehouseNo,
            request.TransitWarehouseNo,
            lines.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            DahiliStokHareketKaydetPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API warehouse return create failed.");
        }

        var recovered = await RecoverMikroApiCreateResponseAsync(
            documentSerie,
            documentOrderNo,
            request,
            lines.Length,
            movementDate,
            documentDate,
            documentNo,
            cancellationToken);

        await ApplyMikroApiAutomaticWarehouseOrderLinksAsync(
            request,
            lines,
            recovered.MovementGuidByRowNo,
            movementDate,
            cancellationToken);

        var recoveredGuid = recovered.MovementGuidByRowNo.Values.FirstOrDefault();
        await mikroApiClient.MarkRecoveredAsync(
            result,
            recovered.DocumentNo,
            recoveredGuid == Guid.Empty ? null : recoveredGuid,
            cancellationToken: cancellationToken);

        return new CreateWarehouseReturnResponse(
            recovered.DocumentSerie,
            recovered.DocumentOrderNo,
            recovered.MovementDate,
            recovered.DocumentDate,
            recovered.DocumentNo,
            recovered.SourceWarehouseNo,
            recovered.TargetWarehouseNo,
            recovered.TransitWarehouseNo,
            recovered.LineCount,
            recovered.TotalQuantity,
            recovered.TotalAmount,
            options.ConnectionStringName);
    }

    private async Task<CreateWarehouseReturnResponse> ExecuteDualShadowAsync(
        CreateWarehouseReturnRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:WarehouseReturn is DualShadow. DahiliStokHareketKaydetV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, cancellationToken);
    }

    private async Task<RecoveredWarehouseReturnCreate> RecoverMikroApiCreateResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateWarehouseReturnRequest request,
        int expectedLineCount,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverWarehouseReturnResponseAsync(
                documentSerie,
                documentOrderNo,
                request,
                expectedLineCount,
                movementDate,
                documentDate,
                documentNo,
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
            "Mikro API warehouse return create succeeded, but created STOK_HAREKETLERI rows could not be read back.");
    }

    private async Task<RecoveredWarehouseReturnCreate?> TryRecoverWarehouseReturnResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateWarehouseReturnRequest request,
        int expectedLineCount,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_tip == MovementType &&
                movement.sth_cins == MovementGenre &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo &&
                movement.sth_cikis_depo_no == request.SourceWarehouseNo &&
                movement.sth_giris_depo_no == request.TransitWarehouseNo &&
                movement.sth_nakliyedeposu == request.TargetWarehouseNo)
            .Select(movement => new
            {
                movement.sth_Guid,
                movement.sth_tarih,
                movement.sth_belge_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_satirno,
                movement.sth_cikis_depo_no,
                movement.sth_giris_depo_no,
                movement.sth_nakliyedeposu,
                movement.sth_nakliyedurumu,
                movement.sth_miktar,
                movement.sth_tutar
            })
            .ToListAsync(cancellationToken);

        if (rows.Count < expectedLineCount)
        {
            return null;
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cikis_depo_no,
                row.sth_giris_depo_no,
                row.sth_nakliyedeposu,
                row.sth_nakliyedurumu
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one warehouse return matched the same serie and order number.");
        }

        var duplicatedRowNo = rows
            .GroupBy(row => row.sth_satirno ?? -1)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatedRowNo is not null)
        {
            throw new InvalidOperationException(
                "More than one warehouse return line matched the same row number.");
        }

        var movementGuidByRowNo = rows
            .Where(row => row.sth_satirno.HasValue)
            .ToDictionary(row => row.sth_satirno!.Value, row => row.sth_Guid);

        for (var rowNo = 0; rowNo < expectedLineCount; rowNo++)
        {
            if (!movementGuidByRowNo.ContainsKey(rowNo))
            {
                return null;
            }
        }

        var firstRow = rows[0];

        return new RecoveredWarehouseReturnCreate(
            firstRow.sth_evrakno_seri ?? documentSerie,
            firstRow.sth_evrakno_sira ?? documentOrderNo,
            firstRow.sth_tarih?.Date ?? movementDate,
            firstRow.sth_belge_tarih?.Date ?? documentDate,
            firstRow.sth_belge_no ?? documentNo,
            firstRow.sth_cikis_depo_no ?? request.SourceWarehouseNo,
            firstRow.sth_nakliyedeposu ?? request.TargetWarehouseNo,
            firstRow.sth_giris_depo_no ?? request.TransitWarehouseNo,
            rows.Count,
            rows.Sum(row => row.sth_miktar ?? 0d),
            rows.Sum(row => row.sth_tutar ?? 0d),
            movementGuidByRowNo);
    }

    private async Task ApplyMikroApiAutomaticWarehouseOrderLinksAsync(
        CreateWarehouseReturnRequest request,
        IReadOnlyList<CreateWarehouseReturnLineRequest> lines,
        IReadOnlyDictionary<int, Guid> movementGuidByRowNo,
        DateTime movementDate,
        CancellationToken cancellationToken)
    {
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var now = DateTime.Now;
                var automaticOrderLines = await CreateAutomaticWarehouseOrderLinesAsync(
                    request,
                    lines,
                    movementDate,
                    now,
                    cancellationToken);
                var movementExtras = new List<STOK_HAREKETLERI_EK>();

                foreach (var (rowNo, automaticOrderLine) in automaticOrderLines)
                {
                    if (!movementGuidByRowNo.TryGetValue(rowNo, out var movementGuid))
                    {
                        throw new InvalidOperationException(
                            "Mikro API warehouse return line could not be matched to the created movement row.");
                    }

                    movementExtras.Add(AutomaticWarehouseOrderFactory.CreateMovementExtra(
                        movementGuid,
                        automaticOrderLine.ssip_Guid,
                        now));
                }

                if (automaticOrderLines.Count > 0)
                {
                    await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.AddRangeAsync(
                        automaticOrderLines.Values,
                        cancellationToken);
                }

                if (movementExtras.Count > 0)
                {
                    await mikroWriteDbContext.STOK_HAREKETLERI_EKs.AddRangeAsync(
                        movementExtras,
                        cancellationToken);
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Dictionary<int, DEPOLAR_ARASI_SIPARISLER>> CreateAutomaticWarehouseOrderLinesAsync(
        CreateWarehouseReturnRequest request,
        IReadOnlyList<CreateWarehouseReturnLineRequest> lines,
        DateTime movementDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var automationOptions = axataOptions.Value.WarehouseOrderAutomation;
        if (!automationOptions.Enabled ||
            !automationOptions.CreateForWarehouseReturns ||
            !automationOptions.WarehouseNos.Contains(request.TargetWarehouseNo))
        {
            return new Dictionary<int, DEPOLAR_ARASI_SIPARISLER>();
        }

        var documentSerie = $"F{request.TargetWarehouseNo}";
        var documentOrderNo = await GetNextWarehouseOrderDocumentOrderNoAsync(documentSerie, cancellationToken);
        var result = new Dictionary<int, DEPOLAR_ARASI_SIPARISLER>(lines.Count);

        for (var rowNo = 0; rowNo < lines.Count; rowNo++)
        {
            var line = lines[rowNo];
            result[rowNo] = AutomaticWarehouseOrderFactory.CreateOrderLine(
                request.TargetWarehouseNo,
                request.SourceWarehouseNo,
                movementDate,
                movementDate,
                documentSerie,
                documentOrderNo,
                rowNo,
                now,
                line.StockCode,
                line.Quantity,
                line.UnitPrice,
                line.UnitPointer,
                line.Description ?? request.Description,
                line.ProjectCode,
                line.ProductResponsibilityCenter);
        }

        return result;
    }

    private async Task<int> GetNextWarehouseOrderDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order => order.ssip_evrakno_seri == documentSerie)
            .MaxAsync(order => order.ssip_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == InterWarehouseShipmentDocumentType &&
                movement.sth_evrakno_seri == documentSerie)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private static STOK_HAREKETLERI CreateMovement(
        CreateWarehouseReturnRequest request,
        CreateWarehouseReturnLineRequest line,
        int rowNo,
        DateTime now,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        string documentSerie,
        int documentOrderNo)
    {
        var unitPrice = line.UnitPrice;
        var amount = line.Quantity * unitPrice;

        return new STOK_HAREKETLERI
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
            sth_tip = MovementType,
            sth_cins = MovementGenre,
            sth_normal_iade = ReturnMovement,
            sth_evraktip = InterWarehouseShipmentDocumentType,
            sth_evrakno_seri = documentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = documentNo,
            sth_belge_tarih = documentDate,
            sth_stok_kod = line.StockCode.Trim(),
            sth_isk_mas1 = 0,
            sth_isk_mas2 = 1,
            sth_isk_mas3 = 1,
            sth_isk_mas4 = 1,
            sth_isk_mas5 = 1,
            sth_isk_mas6 = 1,
            sth_isk_mas7 = 1,
            sth_isk_mas8 = 1,
            sth_isk_mas9 = 1,
            sth_isk_mas10 = 1,
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
            sth_tutar = amount,
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
            sth_aciklama = NormalizeText(line.Description ?? request.Description),
            sth_sip_uid = Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = request.TransitWarehouseNo,
            sth_cikis_depo_no = request.SourceWarehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = NormalizeText(line.CustomerResponsibilityCenter),
            sth_stok_srm_merkezi = NormalizeText(line.ProductResponsibilityCenter),
            sth_fis_tarihi = MikroEmptyDate,
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = 1,
            sth_parti_kodu = NormalizeText(line.PartyCode),
            sth_lot_no = line.LotNo,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = NormalizeText(line.ProjectCode),
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
            sth_nakliyedeposu = request.TargetWarehouseNo,
            sth_nakliyedurumu = WaitingShippingState,
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
            sth_teslim_tarihi = movementDate,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = string.Empty,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private static void Validate(CreateWarehouseReturnRequest request)
    {
        if (request.SourceWarehouseNo <= 0)
        {
            throw new ArgumentException("Source warehouse no must be greater than zero.", nameof(request.SourceWarehouseNo));
        }

        if (request.TargetWarehouseNo <= 0)
        {
            throw new ArgumentException("Target warehouse no must be greater than zero.", nameof(request.TargetWarehouseNo));
        }

        if (request.TransitWarehouseNo <= 0)
        {
            throw new ArgumentException("Transit warehouse no must be greater than zero.", nameof(request.TransitWarehouseNo));
        }

        if (request.SourceWarehouseNo == request.TargetWarehouseNo)
        {
            throw new ArgumentException("Source warehouse and target warehouse can not be the same.");
        }

        if (request.DocumentDate.HasValue &&
            request.MovementDate.HasValue &&
            request.DocumentDate.Value.Date < request.MovementDate.Value.Date)
        {
            throw new ArgumentException("Document date can not be earlier than movement date.", nameof(request.DocumentDate));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one return line is required.", nameof(request.Lines));
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

            if (line.UnitPrice < 0)
            {
                throw new ArgumentException("Line unit price can not be negative.", nameof(request.Lines));
            }

            if (line.UnitPointer is < 1 or > byte.MaxValue)
            {
                throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(request.Lines));
            }

            if (line.LotNo < 0)
            {
                throw new ArgumentException("Line lot no can not be negative.", nameof(request.Lines));
            }
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private sealed record RecoveredWarehouseReturnCreate(
        string DocumentSerie,
        int DocumentOrderNo,
        DateTime MovementDate,
        DateTime DocumentDate,
        string DocumentNo,
        int SourceWarehouseNo,
        int TargetWarehouseNo,
        int TransitWarehouseNo,
        int LineCount,
        double TotalQuantity,
        double TotalAmount,
        IReadOnlyDictionary<int, Guid> MovementGuidByRowNo);
}
