using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;

public sealed class CreateInterWarehouseShipmentUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptions<AxataSynchronizationOptions> axataOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<CreateInterWarehouseShipmentUseCase> logger)
    : ICreateInterWarehouseShipmentUseCase
{
    private const short MovementFileId = 16;
    private const short MovementExtraFileId = 590;
    private const short MikroUserNo = 39;
    private const byte MovementType = 2;
    private const byte MovementGenre = 6;
    private const byte NormalMovement = 0;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte WaitingShippingState = 0;
    private const int FirstDocumentOrderNo = 0;
    private const string DahiliStokHareketKaydetPath = "/Api/apiMethods/DahiliStokHareketKaydetV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<CreateInterWarehouseShipmentResponse> ExecuteAsync(
        CreateInterWarehouseShipmentRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        return mikroWriteRoutingOptions.CurrentValue.InterWarehouseShipment switch
        {
            MikroWriteMode.Database => await ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => await ExecuteMikroApiAsync(request, cancellationToken),
            MikroWriteMode.DualShadow => await ExecuteDualShadowAsync(request, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:InterWarehouseShipment mode '{mode}'.")
        };
    }

    private async Task<CreateInterWarehouseShipmentResponse> ExecuteDatabaseAsync(
        CreateInterWarehouseShipmentRequest request,
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
                var linkedOrderLines = await GetAndValidateLinkedOrderLinesAsync(request, lines, cancellationToken);
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
                    var line = lines[rowNo];
                    var movement = CreateMovement(
                        request,
                        line,
                        rowNo,
                        now,
                        movementDate,
                        documentDate,
                        documentNo,
                        documentSerie,
                        documentOrderNo);

                    movements.Add(movement);

                    var warehouseOrderLineGuid = line.WarehouseOrderLineGuid;
                    if (!warehouseOrderLineGuid.HasValue &&
                        automaticOrderLines.TryGetValue(rowNo, out var automaticOrderLine))
                    {
                        warehouseOrderLineGuid = automaticOrderLine.ssip_Guid;
                    }

                    if (warehouseOrderLineGuid.HasValue)
                    {
                        movementExtras.Add(AutomaticWarehouseOrderFactory.CreateMovementExtra(
                            movement.sth_Guid,
                            warehouseOrderLineGuid.Value,
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

                if (ShouldUpdateLinkedOrderDeliveredQuantities(request, lines))
                {
                    ApplyLinkedOrderDeliveredQuantities(lines, linkedOrderLines, now);
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateInterWarehouseShipmentResponse(
                    documentSerie,
                    documentOrderNo,
                    movementDate,
                    documentDate,
                    documentNo,
                    request.SourceWarehouseNo,
                    request.TargetWarehouseNo,
                    request.TransitWarehouseNo,
                    movements.Count,
                    linkedOrderLines.Count + automaticOrderLines.Count,
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

    private async Task<CreateInterWarehouseShipmentResponse> ExecuteMikroApiAsync(
        CreateInterWarehouseShipmentRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var documentSerie = $"F{request.SourceWarehouseNo}";
        var documentNo = NormalizeText(request.DocumentNo);
        var description = NormalizeText(request.Description);
        var lines = request.Lines.ToArray();

        mikroWriteDbContext.ChangeTracker.Clear();
        await GetAndValidateLinkedOrderLinesAsync(request, lines, cancellationToken);
        mikroWriteDbContext.ChangeTracker.Clear();

        var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
        var payload = InterWarehouseShipmentMikroApiPayloadFactory.Create(
            request,
            lines,
            movementDate,
            documentDate,
            documentNo,
            documentSerie,
            documentOrderNo,
            description);

        logger.LogInformation(
            "Inter warehouse shipment create is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, SourceWarehouseNo={SourceWarehouseNo}, TargetWarehouseNo={TargetWarehouseNo}, TransitWarehouseNo={TransitWarehouseNo}, LineCount={LineCount}",
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
                result.ErrorMessage ?? "Mikro API inter warehouse shipment create failed.");
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
        var linkedWarehouseOrderLineCount = await ApplyMikroApiWarehouseOrderLinksAsync(
            request,
            lines,
            recovered.MovementGuidByRowNo,
            movementDate,
            cancellationToken);

        return new CreateInterWarehouseShipmentResponse(
            recovered.DocumentSerie,
            recovered.DocumentOrderNo,
            recovered.MovementDate,
            recovered.DocumentDate,
            recovered.DocumentNo,
            recovered.SourceWarehouseNo,
            recovered.TargetWarehouseNo,
            recovered.TransitWarehouseNo,
            recovered.LineCount,
            linkedWarehouseOrderLineCount,
            recovered.TotalQuantity,
            recovered.TotalAmount,
            options.ConnectionStringName);
    }

    private async Task<CreateInterWarehouseShipmentResponse> ExecuteDualShadowAsync(
        CreateInterWarehouseShipmentRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:InterWarehouseShipment is DualShadow. DahiliStokHareketKaydetV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, cancellationToken);
    }

    private async Task<RecoveredInterWarehouseShipmentCreate> RecoverMikroApiCreateResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateInterWarehouseShipmentRequest request,
        int expectedLineCount,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverInterWarehouseShipmentResponseAsync(
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
            "Mikro API inter warehouse shipment create succeeded, but created STOK_HAREKETLERI rows could not be read back.");
    }

    private async Task<RecoveredInterWarehouseShipmentCreate?> TryRecoverInterWarehouseShipmentResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateInterWarehouseShipmentRequest request,
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
                movement.sth_normal_iade == NormalMovement &&
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
                "More than one inter warehouse shipment matched the same serie and order number.");
        }

        var duplicatedRowNo = rows
            .GroupBy(row => row.sth_satirno ?? -1)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatedRowNo is not null)
        {
            throw new InvalidOperationException(
                "More than one inter warehouse shipment line matched the same row number.");
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

        return new RecoveredInterWarehouseShipmentCreate(
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

    private async Task<int> ApplyMikroApiWarehouseOrderLinksAsync(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> lines,
        IReadOnlyDictionary<int, Guid> movementGuidByRowNo,
        DateTime movementDate,
        CancellationToken cancellationToken)
    {
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var now = DateTime.Now;
                var linkedOrderLines = await GetAndValidateLinkedOrderLinesAsync(request, lines, cancellationToken);
                var automaticOrderLines = await CreateAutomaticWarehouseOrderLinesAsync(
                    request,
                    lines,
                    movementDate,
                    now,
                    cancellationToken);
                var movementExtras = new List<STOK_HAREKETLERI_EK>();

                for (var rowNo = 0; rowNo < lines.Count; rowNo++)
                {
                    var line = lines[rowNo];
                    var warehouseOrderLineGuid = line.WarehouseOrderLineGuid;
                    if (!warehouseOrderLineGuid.HasValue &&
                        automaticOrderLines.TryGetValue(rowNo, out var automaticOrderLine))
                    {
                        warehouseOrderLineGuid = automaticOrderLine.ssip_Guid;
                    }

                    if (!warehouseOrderLineGuid.HasValue)
                    {
                        continue;
                    }

                    if (!movementGuidByRowNo.TryGetValue(rowNo, out var movementGuid))
                    {
                        throw new InvalidOperationException(
                            "Mikro API inter warehouse shipment line could not be matched to the created movement row.");
                    }

                    movementExtras.Add(AutomaticWarehouseOrderFactory.CreateMovementExtra(
                        movementGuid,
                        warehouseOrderLineGuid.Value,
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

                if (ShouldUpdateLinkedOrderDeliveredQuantities(request, lines))
                {
                    ApplyLinkedOrderDeliveredQuantities(lines, linkedOrderLines, now);
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return linkedOrderLines.Count + automaticOrderLines.Count;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Dictionary<int, DEPOLAR_ARASI_SIPARISLER>> CreateAutomaticWarehouseOrderLinesAsync(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> lines,
        DateTime movementDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var automationOptions = axataOptions.Value.WarehouseOrderAutomation;
        if (!automationOptions.Enabled ||
            !automationOptions.CreateForInterWarehouseShipments ||
            !automationOptions.WarehouseNos.Contains(request.TargetWarehouseNo))
        {
            return new Dictionary<int, DEPOLAR_ARASI_SIPARISLER>();
        }

        var unlinkedRows = lines
            .Select((line, rowNo) => new { line, rowNo })
            .Where(item => !item.line.WarehouseOrderLineGuid.HasValue)
            .ToArray();

        if (unlinkedRows.Length == 0)
        {
            return new Dictionary<int, DEPOLAR_ARASI_SIPARISLER>();
        }

        var documentSerie = $"F{request.TargetWarehouseNo}";
        var documentOrderNo = await GetNextWarehouseOrderDocumentOrderNoAsync(documentSerie, cancellationToken);
        var result = new Dictionary<int, DEPOLAR_ARASI_SIPARISLER>(unlinkedRows.Length);

        for (var orderRowNo = 0; orderRowNo < unlinkedRows.Length; orderRowNo++)
        {
            var item = unlinkedRows[orderRowNo];
            result[item.rowNo] = AutomaticWarehouseOrderFactory.CreateOrderLine(
                request.TargetWarehouseNo,
                request.SourceWarehouseNo,
                movementDate,
                movementDate,
                documentSerie,
                documentOrderNo,
                orderRowNo,
                now,
                item.line.StockCode,
                item.line.Quantity,
                item.line.UnitPrice,
                item.line.UnitPointer,
                item.line.Description ?? request.Description,
                item.line.ProjectCode,
                item.line.ProductResponsibilityCenter);
        }

        return result;
    }

    private async Task<Dictionary<Guid, DEPOLAR_ARASI_SIPARISLER>> GetAndValidateLinkedOrderLinesAsync(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyCollection<CreateInterWarehouseShipmentLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var linkedGuids = lines
            .Where(line => line.WarehouseOrderLineGuid.HasValue)
            .Select(line => line.WarehouseOrderLineGuid!.Value)
            .Distinct()
            .ToArray();

        if (linkedGuids.Length == 0)
        {
            return new Dictionary<Guid, DEPOLAR_ARASI_SIPARISLER>();
        }

        var orderLines = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order => linkedGuids.Contains(order.ssip_Guid))
            .ToDictionaryAsync(order => order.ssip_Guid, cancellationToken);

        var missingGuid = linkedGuids.FirstOrDefault(guid => !orderLines.ContainsKey(guid));
        if (missingGuid != Guid.Empty)
        {
            throw new KeyNotFoundException($"Warehouse order line was not found: {missingGuid}");
        }

        foreach (var group in lines
                     .Where(line => line.WarehouseOrderLineGuid.HasValue)
                     .GroupBy(line => line.WarehouseOrderLineGuid!.Value))
        {
            var orderLine = orderLines[group.Key];
            var requestedQuantity = group.Sum(line => line.Quantity);
            var remainingQuantity = (orderLine.ssip_miktar ?? 0d) - (orderLine.ssip_teslim_miktar ?? 0d);

            if (orderLine.ssip_girdepo != request.TargetWarehouseNo ||
                orderLine.ssip_cikdepo != request.SourceWarehouseNo)
            {
                throw new InvalidOperationException(
                    "Linked warehouse order line does not match the selected source and target warehouses.");
            }

            if (orderLine.ssip_kapat_fl == true)
            {
                throw new InvalidOperationException("Linked warehouse order line is already closed.");
            }

            if (requestedQuantity > remainingQuantity)
            {
                throw new InvalidOperationException(
                    "Shipment quantity can not be greater than linked warehouse order remaining quantity.");
            }

            foreach (var line in group)
            {
                if (!string.Equals(
                        orderLine.ssip_stok_kod?.Trim(),
                        line.StockCode.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Shipment stock code must match the linked warehouse order line stock code.");
                }
            }
        }

        return orderLines;
    }

    private static bool ShouldUpdateLinkedOrderDeliveredQuantities(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyCollection<CreateInterWarehouseShipmentLineRequest> lines) =>
        request.UpdateLinkedOrderDeliveredQuantities ||
        lines.Any(line => line.WarehouseOrderLineGuid.HasValue);

    private static void ApplyLinkedOrderDeliveredQuantities(
        IReadOnlyCollection<CreateInterWarehouseShipmentLineRequest> lines,
        IReadOnlyDictionary<Guid, DEPOLAR_ARASI_SIPARISLER> linkedOrderLines,
        DateTime now)
    {
        foreach (var group in lines
                     .Where(line => line.WarehouseOrderLineGuid.HasValue)
                     .GroupBy(line => line.WarehouseOrderLineGuid!.Value))
        {
            if (!linkedOrderLines.TryGetValue(group.Key, out var orderLine))
            {
                continue;
            }

            var deliveredQuantity = (orderLine.ssip_teslim_miktar ?? 0d) + group.Sum(line => line.Quantity);
            var totalQuantity = orderLine.ssip_miktar ?? 0d;

            orderLine.ssip_teslim_miktar = totalQuantity > 0d
                ? Math.Min(deliveredQuantity, totalQuantity)
                : deliveredQuantity;
            orderLine.ssip_kapat_fl = totalQuantity > 0d &&
                orderLine.ssip_teslim_miktar >= totalQuantity;
            orderLine.ssip_lastup_user = MikroUserNo;
            orderLine.ssip_lastup_date = now;
        }
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
        CreateInterWarehouseShipmentRequest request,
        CreateInterWarehouseShipmentLineRequest line,
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
            sth_normal_iade = NormalMovement,
            sth_evraktip = InterWarehouseShipmentDocumentType,
            sth_evrakno_seri = documentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = "",
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

    private static STOK_HAREKETLERI_EK CreateMovementExtra(
        Guid movementGuid,
        Guid warehouseOrderLineGuid,
        DateTime now) =>
        new()
        {
            sthek_Guid = Guid.NewGuid(),
            sthek_DBCno = 0,
            sthek_SpecRECno = 0,
            sthek_iptal = false,
            sthek_fileid = MovementExtraFileId,
            sthek_hidden = false,
            sthek_kilitli = false,
            sthek_degisti = false,
            sthek_checksum = 0,
            sthek_create_user = MikroUserNo,
            sthek_create_date = now,
            sthek_lastup_user = MikroUserNo,
            sthek_lastup_date = now,
            sthek_special1 = string.Empty,
            sthek_special2 = string.Empty,
            sthek_special3 = string.Empty,
            sthek_related_uid = movementGuid,
            sth_subesip_uid = warehouseOrderLineGuid,
            sth_bkm_uid = Guid.Empty,
            sth_karsikons_uid = Guid.Empty,
            sth_rez_uid = Guid.Empty,
            sth_optamam_uid = Guid.Empty,
            sth_iadeTlp_uid = Guid.Empty,
            sth_HalSatis_uid = Guid.Empty,
            sth_ciroprim_uid = Guid.Empty,
            sth_iade_evrak_seri = string.Empty,
            sth_iade_evrak_sira = 0,
            sth_yat_tes_kodu = string.Empty,
            sth_ihracat_kredi_kodu = string.Empty,
            sth_diib_belge_no = string.Empty,
            sth_diib_satir_no = 0,
            sth_mensey_ulke_tipi = 0,
            sth_mensey_ulke_kodu = string.Empty,
            sth_halrehmiktari = 0d,
            sth_halrehfiyati = 0d,
            sth_halsandikmiktari = 0d,
            sth_halsandikfiyati = 0d,
            sth_halsandikkdvtutari = 0d,
            sth_HalKomisyonuKdv = 0d,
            sth_HalRusum = 0d,
            sth_satistipi = 0,
            sth_vardiya_tarihi = MikroEmptyDate,
            sth_vardiya_no = 0,
            sth_direkt_iscilik_1 = 0d,
            sth_direkt_iscilik_2 = 0d,
            sth_direkt_iscilik_3 = 0d,
            sth_direkt_iscilik_4 = 0d,
            sth_direkt_iscilik_5 = 0d,
            sth_genel_uretim_1 = 0d,
            sth_genel_uretim_2 = 0d,
            sth_genel_uretim_3 = 0d,
            sth_genel_uretim_4 = 0d,
            sth_genel_uretim_5 = 0d,
            sth_fis_tarihi2 = MikroEmptyDate,
            sth_fis_sirano2 = 0,
            sth_fiyfark_esas_evrak_seri = string.Empty,
            sth_fiyfark_esas_evrak_sira = 0,
            sth_fiyfark_esas_satir_no = 0,
            sth_istisna = string.Empty,
            sth_otv_tevkifat_turu = 0,
            sth_otv_tevkifat_tutari = 0d,
            sth_servishar_uid = Guid.Empty,
            sth_bakimsarf_uid = Guid.Empty,
            sth_utsbildirimturu = 0,
            sth_utshekzayiatturu = 0,
            sth_utsimhabertarafgerekcesi = 0,
            sth_utsdigergerekceaciklamasi = string.Empty,
            sth_hizlisatis_promosyonkodu_1 = string.Empty,
            sth_hizlisatis_promosyonkodu_2 = string.Empty,
            sth_hizlisatis_promosyonkodu_3 = string.Empty,
            sth_hks_kunye_no = string.Empty,
            sth_hks_carikodu = string.Empty,
            sth_tevkifat_islem_turu_idx = 0,
            sth_otv_istisnakodu = string.Empty,
            sth_karsi_program_kodu = string.Empty,
            sth_sas_kalem_no = string.Empty,
            sth_yerlilik_orani = 0
        };

    private static void Validate(CreateInterWarehouseShipmentRequest request)
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
            throw new ArgumentException("At least one shipment line is required.", nameof(request.Lines));
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

    private sealed record RecoveredInterWarehouseShipmentCreate(
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
