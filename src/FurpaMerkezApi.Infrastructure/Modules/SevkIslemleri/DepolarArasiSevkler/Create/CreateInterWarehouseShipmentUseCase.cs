using System.Data;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
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
    IOptionsMonitor<GreenGrocerProductCaseOptions> greenGrocerProductCaseOptions,
    MikroApiClient mikroApiClient,
    ILogger<CreateInterWarehouseShipmentUseCase> logger)
    : ICreateInterWarehouseShipmentUseCase
{
    private const short MovementFileId = 16;
    private const short MikroUserNo = 39;
    private const byte MovementType = 2;
    private const byte MovementGenre = 6;
    private const byte NormalMovement = 0;
    private const byte InterWarehouseShipmentDocumentType = 17;
    private const byte WaitingShippingState = 0;
    private const int FirstDocumentOrderNo = 0;
    private const string DahiliStokHareketKaydetPath = "/Api/apiMethods/DahiliStokHareketKaydetV2";
    private const string DepolarArasiSiparisKaydetPath = "/Api/apiMethods/DepolarArasiSiparisKaydetV2";
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
        var lines = await GreenGrocerShipmentLineNormalizer.DetachWarehouseOrderLinksAsync(
            mikroWriteDbContext,
            request,
            request.Lines.ToArray(),
            IsGreenGrocerOrderLinkingEnabled(),
            cancellationToken);
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
        var lines = await GreenGrocerShipmentLineNormalizer.DetachWarehouseOrderLinksAsync(
            mikroWriteDbContext,
            request,
            request.Lines.ToArray(),
            IsGreenGrocerOrderLinkingEnabled(),
            cancellationToken);

        mikroWriteDbContext.ChangeTracker.Clear();
        var linkedWarehouseOrderLines = await GetAndValidateLinkedOrderLinesAsync(
            request,
            lines,
            cancellationToken);
        mikroWriteDbContext.ChangeTracker.Clear();

        var automaticWarehouseOrderLineGuids = await CreateMikroApiAutomaticWarehouseOrderLineGuidsAsync(
            request,
            lines,
            movementDate,
            cancellationToken);
        var shipmentLines = ApplyAutomaticWarehouseOrderLineGuids(lines, automaticWarehouseOrderLineGuids);

        var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
        var payload = InterWarehouseShipmentMikroApiPayloadFactory.Create(
            request,
            shipmentLines,
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
            shipmentLines.Length);

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
            shipmentLines.Length,
            movementDate,
            documentDate,
            documentNo,
            cancellationToken);
        var totalLinkedWarehouseOrderLineCount =
            linkedWarehouseOrderLines.Count + automaticWarehouseOrderLineGuids.Count;

        var recoveredGuid = recovered.MovementGuidByRowNo.Values.FirstOrDefault();
        await mikroApiClient.MarkRecoveredAsync(
            result,
            recovered.DocumentNo,
            recoveredGuid == Guid.Empty ? null : recoveredGuid,
            cancellationToken: cancellationToken);

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
            totalLinkedWarehouseOrderLineCount,
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

    private async Task<Dictionary<int, Guid>> CreateMikroApiAutomaticWarehouseOrderLineGuidsAsync(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> lines,
        DateTime movementDate,
        CancellationToken cancellationToken)
    {
        var automaticRows = GetAutomaticWarehouseOrderRows(request, lines);
        if (automaticRows.Length == 0)
        {
            return new Dictionary<int, Guid>();
        }

        if (mikroWriteRoutingOptions.CurrentValue.IssuedWarehouseOrder != MikroWriteMode.MikroApi)
        {
            throw new InvalidOperationException(
                "Mikro API inter warehouse shipment automatic order creation requires MikroWriteRouting:IssuedWarehouseOrder to be MikroApi.");
        }

        var orderDate = movementDate;
        var deliveryDate = movementDate;
        var documentSerie = $"F{request.TargetWarehouseNo}";
        var documentOrderNo = await GetNextWarehouseOrderDocumentOrderNoAsync(
            documentSerie,
            cancellationToken);
        var orderRequest = new CreateIssuedWarehouseOrderRequest(
            request.TargetWarehouseNo,
            request.SourceWarehouseNo,
            orderDate,
            deliveryDate,
            request.Description,
            automaticRows
                .Select(row => new CreateIssuedWarehouseOrderLineRequest(
                    row.Line.StockCode,
                    row.Line.Quantity,
                    null,
                    row.Line.UnitPrice,
                    row.Line.UnitPointer,
                    row.Line.Description ?? request.Description,
                    null,
                    row.Line.ProjectCode,
                    row.Line.ProductResponsibilityCenter))
                .ToArray());
        var payload = IssuedWarehouseOrderMikroApiPayloadFactory.Create(
            orderRequest,
            orderRequest.Lines,
            orderDate,
            deliveryDate,
            documentSerie,
            documentOrderNo);

        logger.LogInformation(
            "Automatic warehouse order for inter warehouse shipment is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, InWarehouseNo={InWarehouseNo}, OutWarehouseNo={OutWarehouseNo}, LineCount={LineCount}",
            DepolarArasiSiparisKaydetPath,
            documentSerie,
            documentOrderNo,
            orderRequest.InWarehouseNo,
            orderRequest.OutWarehouseNo,
            automaticRows.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            DepolarArasiSiparisKaydetPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API automatic warehouse order create failed.");
        }

        var recovered = await RecoverMikroApiAutomaticWarehouseOrderLineGuidsAsync(
            documentSerie,
            documentOrderNo,
            orderRequest,
            automaticRows,
            cancellationToken);
        var recoveredGuid = recovered.Values.FirstOrDefault();
        await mikroApiClient.MarkRecoveredAsync(
            result,
            $"{documentSerie}/{documentOrderNo}",
            recoveredGuid == Guid.Empty ? null : recoveredGuid,
            cancellationToken: cancellationToken);

        return recovered;
    }

    private async Task<Dictionary<int, Guid>> RecoverMikroApiAutomaticWarehouseOrderLineGuidsAsync(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedWarehouseOrderRequest orderRequest,
        IReadOnlyCollection<AutomaticWarehouseOrderRow> expectedRows,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverMikroApiAutomaticWarehouseOrderLineGuidsAsync(
                documentSerie,
                documentOrderNo,
                orderRequest,
                expectedRows,
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
            "Mikro API automatic warehouse order create succeeded, but created DEPOLAR_ARASI_SIPARISLER rows could not be read back.");
    }

    private async Task<Dictionary<int, Guid>?> TryRecoverMikroApiAutomaticWarehouseOrderLineGuidsAsync(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedWarehouseOrderRequest orderRequest,
        IReadOnlyCollection<AutomaticWarehouseOrderRow> expectedRows,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_evrakno_seri == documentSerie &&
                order.ssip_evrakno_sira == documentOrderNo &&
                order.ssip_girdepo == orderRequest.InWarehouseNo &&
                order.ssip_cikdepo == orderRequest.OutWarehouseNo)
            .Select(order => new
            {
                order.ssip_Guid,
                order.ssip_satirno,
                order.ssip_stok_kod,
                order.ssip_miktar
            })
            .ToListAsync(cancellationToken);

        if (rows.Count < expectedRows.Count)
        {
            return null;
        }

        var duplicatedRowNo = rows
            .GroupBy(row => row.ssip_satirno ?? -1)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatedRowNo is not null)
        {
            throw new InvalidOperationException(
                "More than one automatic warehouse order line matched the same row number.");
        }

        var rowByOrderRowNo = rows
            .Where(row => row.ssip_satirno.HasValue)
            .ToDictionary(row => row.ssip_satirno!.Value);
        var result = new Dictionary<int, Guid>(expectedRows.Count);

        foreach (var expectedRow in expectedRows)
        {
            if (!rowByOrderRowNo.TryGetValue(expectedRow.OrderRowNo, out var row))
            {
                return null;
            }

            if (!string.Equals(
                    row.ssip_stok_kod?.Trim(),
                    expectedRow.Line.StockCode.Trim(),
                    StringComparison.OrdinalIgnoreCase) ||
                Math.Abs((row.ssip_miktar ?? 0d) - expectedRow.Line.Quantity) > 0.0001d)
            {
                throw new InvalidOperationException(
                    "Mikro API automatic warehouse order line could not be matched safely.");
            }

            result[expectedRow.OriginalRowNo] = row.ssip_Guid;
        }

        return result;
    }

    private AutomaticWarehouseOrderRow[] GetAutomaticWarehouseOrderRows(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> lines)
    {
        var automationOptions = axataOptions.Value.WarehouseOrderAutomation;
        if (GreenGrocerShipmentLineNormalizer.IsGreenGrocerSourceWarehouse(request.SourceWarehouseNo) ||
            !automationOptions.Enabled ||
            !automationOptions.CreateForInterWarehouseShipments ||
            !automationOptions.WarehouseNos.Contains(request.TargetWarehouseNo))
        {
            return [];
        }

        return lines
            .Select((line, rowNo) => new { line, rowNo })
            .Where(item => !item.line.WarehouseOrderLineGuid.HasValue)
            .Select((item, orderRowNo) => new AutomaticWarehouseOrderRow(
                item.rowNo,
                orderRowNo,
                item.line))
            .ToArray();
    }

    private bool IsGreenGrocerOrderLinkingEnabled()
    {
        var currentOptions = greenGrocerProductCaseOptions.CurrentValue;
        return currentOptions.Enabled && currentOptions.OrderLinkingEnabled;
    }

    private static CreateInterWarehouseShipmentLineRequest[] ApplyAutomaticWarehouseOrderLineGuids(
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> lines,
        IReadOnlyDictionary<int, Guid> automaticWarehouseOrderLineGuids)
    {
        if (automaticWarehouseOrderLineGuids.Count == 0)
        {
            return lines.ToArray();
        }

        return lines
            .Select((line, rowNo) => automaticWarehouseOrderLineGuids.TryGetValue(rowNo, out var warehouseOrderLineGuid)
                ? line with { WarehouseOrderLineGuid = warehouseOrderLineGuid }
                : line)
            .ToArray();
    }

    private async Task<Dictionary<int, DEPOLAR_ARASI_SIPARISLER>> CreateAutomaticWarehouseOrderLinesAsync(
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> lines,
        DateTime movementDate,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var automationOptions = axataOptions.Value.WarehouseOrderAutomation;
        if (GreenGrocerShipmentLineNormalizer.IsGreenGrocerSourceWarehouse(request.SourceWarehouseNo) ||
            !automationOptions.Enabled ||
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

    private sealed record AutomaticWarehouseOrderRow(
        int OriginalRowNo,
        int OrderRowNo,
        CreateInterWarehouseShipmentLineRequest Line);

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
