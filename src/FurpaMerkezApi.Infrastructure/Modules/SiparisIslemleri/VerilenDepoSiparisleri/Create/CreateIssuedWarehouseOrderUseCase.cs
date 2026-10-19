using System.Data;
using System.Diagnostics;
using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.Common;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

public sealed class CreateIssuedWarehouseOrderUseCase(
    AuthDbContext authDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    IOptionsMonitor<GreenGrocerProductCaseOptions> greenGrocerProductCaseOptions,
    IClock clock,
    MikroApiClient mikroApiClient,
    MobileOfflineSyncService mobileOfflineSyncService,
    ILogger<CreateIssuedWarehouseOrderUseCase> logger)
    : ICreateIssuedWarehouseOrderUseCase
{
    private const short FileId = 86;
    private const short MikroUserNo = 39;
    private const int GreenGrocerWarehouseNo = 56;
    private const int FirstDocumentOrderNo = 0;
    private const string DepolarArasiSiparisKaydetPath = "/Api/apiMethods/DepolarArasiSiparisKaydetV2";
    private const string OfflineOperationCode = "siparis-islemleri.verilen-depo-siparisleri.create";
    private static readonly DateTime MikroEmptyDate = new(1900, 1, 1);

    public async Task<CreateIssuedWarehouseOrderResponse> ExecuteAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        request = request with { ClientRequestId = request.ClientRequestId ?? Guid.NewGuid() };
        Validate(request);

        return await OfflineCreateGuard.ExecuteAsync(
            mobileOfflineSyncService,
            OfflineOperationCode,
            request.CreatedByUserId,
            request.InWarehouseNo,
            request.ClientRequestId,
            request,
            (storedRequestPayload, innerCancellationToken) => TryRecoverOfflineResponseAsync(
                storedRequestPayload is null
                    ? request
                    : MobileOfflineSyncService.DeserializeRequest<CreateIssuedWarehouseOrderRequest>(storedRequestPayload),
                innerCancellationToken),
            innerCancellationToken => ExecuteRoutedAsync(request, innerCancellationToken),
            cancellationToken,
            preventReexecutionAfterUncertainOutcome: true,
            verifyCompletedWithRecovery: true);
    }

    public Task<OfflineSyncStatusDto<CreateIssuedWarehouseOrderResponse>> GetOfflineSyncStatusAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken) =>
        mobileOfflineSyncService.GetStatusAsync<CreateIssuedWarehouseOrderResponse>(
            OfflineOperationCode,
            requestedByUserId,
            clientRequestId,
            (storedRequestPayload, innerCancellationToken) => TryRecoverOfflineResponseAsync(
                MobileOfflineSyncService.DeserializeRequest<CreateIssuedWarehouseOrderRequest>(storedRequestPayload),
                innerCancellationToken),
            cancellationToken,
            verifyCompletedWithRecovery: true);

    private Task<CreateIssuedWarehouseOrderResponse> ExecuteRoutedAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        return mikroWriteRoutingOptions.CurrentValue.IssuedWarehouseOrder switch
        {
            MikroWriteMode.Database => ExecuteDatabaseAsync(request, cancellationToken),
            MikroWriteMode.MikroApi => ExecuteMikroApiAsync(request, cancellationToken),
            MikroWriteMode.DualShadow => ExecuteDualShadowAsync(request, cancellationToken),
            var mode => throw new InvalidOperationException(
                $"Unsupported MikroWriteRouting:IssuedWarehouseOrder mode '{mode}'.")
        };
    }

    private async Task<CreateIssuedWarehouseOrderResponse> ExecuteDatabaseAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;
        var deliveryDate = (request.DeliveryDate ?? orderDate).Date;
        var documentSerie = $"F{request.InWarehouseNo}";
        var lines = request.Lines.ToArray();
        var traceParts = ResolveOfflineTraceParts(request.ClientRequestId);
        var totalStopwatch = Stopwatch.StartNew();
        var phaseStartedAt = Stopwatch.GetTimestamp();

        void LogCreatePhase(string phase, int? documentOrderNo)
        {
            var elapsed = Stopwatch.GetElapsedTime(phaseStartedAt);
            if (elapsed.TotalMilliseconds >= 500d)
            {
                logger.LogInformation(
                    "Issued warehouse order create phase {Phase} completed in {ElapsedMs} ms. TotalMs={TotalMs}, DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, InWarehouseNo={InWarehouseNo}, OutWarehouseNo={OutWarehouseNo}, LineCount={LineCount}, Route=Database",
                    phase,
                    elapsed.TotalMilliseconds,
                    totalStopwatch.Elapsed.TotalMilliseconds,
                    documentSerie,
                    documentOrderNo,
                    request.InWarehouseNo,
                    request.OutWarehouseNo,
                    lines.Length);
            }

            phaseStartedAt = Stopwatch.GetTimestamp();
        }

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        var writeResult = await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            LogCreatePhase("begin-transaction", null);

            try
            {
                var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
                LogCreatePhase("next-document-order-no", documentOrderNo);
                var entities = lines
                    .Select((line, rowNo) => CreateOrderLine(
                        request,
                        line,
                        rowNo,
                        now,
                        orderDate,
                        deliveryDate,
                        documentSerie,
                        documentOrderNo,
                        traceParts))
                    .ToArray();
                LogCreatePhase("build-order-rows", documentOrderNo);

                await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.AddRangeAsync(entities, cancellationToken);
                LogCreatePhase("track-entities", documentOrderNo);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                LogCreatePhase("save-changes", documentOrderNo);
                await transaction.CommitAsync(cancellationToken);
                LogCreatePhase("commit", documentOrderNo);

                return (
                    DocumentOrderNo: documentOrderNo,
                    LineGuidByRowNo: entities.ToDictionary(
                        entity => entity.ssip_satirno ?? 0,
                        entity => entity.ssip_Guid));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });

        await TryCreateGreenGrocerOrderLineSnapshotsAsync(
            request,
            lines,
            writeResult.LineGuidByRowNo,
            documentSerie,
            writeResult.DocumentOrderNo,
            orderDate,
            CancellationToken.None);
        LogCreatePhase("green-grocer-snapshots", writeResult.DocumentOrderNo);

        mikroWriteDbContext.ChangeTracker.Clear();
        return await RecoverMikroApiCreateResponseAsync(
            documentSerie,
            writeResult.DocumentOrderNo,
            request,
            lines,
            orderDate,
            deliveryDate,
            options.ConnectionStringName,
            rawResponse: string.Empty,
            uncertainWriteOutcome: false,
            cancellationToken: CancellationToken.None);
    }

    private async Task<CreateIssuedWarehouseOrderResponse> ExecuteMikroApiAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;
        var deliveryDate = (request.DeliveryDate ?? orderDate).Date;
        var documentSerie = $"F{request.InWarehouseNo}";
        var lines = request.Lines.ToArray();
        var traceParts = ResolveOfflineTraceParts(request.ClientRequestId);
        var totalStopwatch = Stopwatch.StartNew();
        var phaseStartedAt = Stopwatch.GetTimestamp();

        void LogCreatePhase(string phase, int? documentOrderNo)
        {
            var elapsed = Stopwatch.GetElapsedTime(phaseStartedAt);
            if (elapsed.TotalMilliseconds >= 500d)
            {
                logger.LogInformation(
                    "Issued warehouse order create phase {Phase} completed in {ElapsedMs} ms. TotalMs={TotalMs}, DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, InWarehouseNo={InWarehouseNo}, OutWarehouseNo={OutWarehouseNo}, LineCount={LineCount}, Route=MikroApi",
                    phase,
                    elapsed.TotalMilliseconds,
                    totalStopwatch.Elapsed.TotalMilliseconds,
                    documentSerie,
                    documentOrderNo,
                    request.InWarehouseNo,
                    request.OutWarehouseNo,
                    lines.Length);
            }

            phaseStartedAt = Stopwatch.GetTimestamp();
        }

        await using var sequenceLock = await MikroDocumentSequenceLock.AcquireAsync(
            mikroWriteDbContext,
            "IssuedWarehouseOrder",
            documentSerie,
            cancellationToken);
        var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
        LogCreatePhase("next-document-order-no", documentOrderNo);
        var payload = IssuedWarehouseOrderMikroApiPayloadFactory.Create(
            request,
            lines,
            orderDate,
            deliveryDate,
            documentSerie,
            documentOrderNo,
            traceParts.First,
            traceParts.Second,
            traceParts.Third);
        LogCreatePhase("build-payload", documentOrderNo);

        logger.LogInformation(
            "Issued warehouse order create is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, InWarehouseNo={InWarehouseNo}, OutWarehouseNo={OutWarehouseNo}, LineCount={LineCount}",
            DepolarArasiSiparisKaydetPath,
            documentSerie,
            documentOrderNo,
            request.InWarehouseNo,
            request.OutWarehouseNo,
            lines.Length);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            DepolarArasiSiparisKaydetPath,
            payload,
            cancellationToken);
        LogCreatePhase("mikro-api-post", documentOrderNo);

        var recoverableCreateOutcome = MikroApiWriteAuditService.IsRecoverableCreateOutcome(result);
        if (result.IsError && !recoverableCreateOutcome)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API issued warehouse order create failed.");
        }

        CreateIssuedWarehouseOrderResponse recovered;
        try
        {
            recovered = await RecoverMikroApiCreateResponseAsync(
                documentSerie,
                documentOrderNo,
                request,
                lines,
                orderDate,
                deliveryDate,
                options.ConnectionStringName,
                result.RawResponse,
                recoverableCreateOutcome,
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw MikroApiCreateConflictFactory.Create(result, exception);
        }
        LogCreatePhase("recover-response", documentOrderNo);

        var orderLineGuidByRowNo = await GetOrderLineGuidByRowNoAsync(
            documentSerie,
            documentOrderNo,
            request,
            cancellationToken);
        LogCreatePhase("load-created-line-guids", documentOrderNo);
        if (orderLineGuidByRowNo.Count == 0)
        {
            orderLineGuidByRowNo = TryMapMikroApiResultRowsByRowNo(result.RawResponse, lines.Length, out var responseLineGuids)
                ? responseLineGuids
                : orderLineGuidByRowNo;
        }

        await TryCreateGreenGrocerOrderLineSnapshotsAsync(
            request,
            lines,
            orderLineGuidByRowNo,
            recovered.DocumentSerie,
            recovered.DocumentOrderNo,
            recovered.OrderDate,
            CancellationToken.None);
        LogCreatePhase("green-grocer-snapshots", recovered.DocumentOrderNo);

        await mikroApiClient.MarkRecoveredAsync(
            result,
            $"{recovered.DocumentSerie}/{recovered.DocumentOrderNo}",
            cancellationToken: cancellationToken);
        LogCreatePhase("mark-recovered", recovered.DocumentOrderNo);

        if (result.IsError)
        {
            logger.LogWarning(
                "Recovered issued warehouse order after an unknown Mikro API write outcome. Document={DocumentSerie}/{DocumentOrderNo}, AuditId={AuditId}, RequestId={RequestId}",
                recovered.DocumentSerie,
                recovered.DocumentOrderNo,
                result.AuditId,
                result.RequestId);
        }

        return recovered;
    }

    private async Task<CreateIssuedWarehouseOrderResponse> ExecuteDualShadowAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "MikroWriteRouting:IssuedWarehouseOrder is DualShadow. DepolarArasiSiparisKaydetV2 has no dry-run contract, so only the database write path will run.");

        return await ExecuteDatabaseAsync(request, cancellationToken);
    }

    private async Task<CreateIssuedWarehouseOrderResponse> RecoverMikroApiCreateResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedWarehouseOrderRequest request,
        IReadOnlyList<CreateIssuedWarehouseOrderLineRequest> lines,
        DateTime orderDate,
        DateTime deliveryDate,
        string writeConnectionName,
        string rawResponse,
        bool uncertainWriteOutcome,
        CancellationToken cancellationToken)
    {
        var response = await MikroApiReadbackRetry.ExecuteAsync(
            uncertainWriteOutcome,
            readbackToken => TryRecoverWarehouseOrderResponseAsync(
                documentSerie,
                documentOrderNo,
                request,
                lines,
                orderDate,
                deliveryDate,
                writeConnectionName,
                readbackToken));

        return response ?? throw StockMovementRecoveryMatcher.OutcomeUnconfirmed();
    }

    private static bool TryRecoverWarehouseOrderResponseFromMikroApiResult(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedWarehouseOrderRequest request,
        IReadOnlyList<CreateIssuedWarehouseOrderLineRequest> lines,
        DateTime orderDate,
        DateTime deliveryDate,
        string writeConnectionName,
        string rawResponse,
        out CreateIssuedWarehouseOrderResponse response)
    {
        response = default!;
        var responseRows = MikroApiCreatedDocumentResultReader.ReadRows(rawResponse);
        if (responseRows.Count != lines.Count)
        {
            return false;
        }

        var firstRow = responseRows[0];
        response = new CreateIssuedWarehouseOrderResponse(
            firstRow.DocumentSerie ?? documentSerie,
            firstRow.DocumentOrderNo ?? documentOrderNo,
            orderDate,
            deliveryDate,
            request.InWarehouseNo,
            request.OutWarehouseNo,
            lines.Count,
            lines.Sum(line => line.Quantity),
            writeConnectionName);

        return true;
    }

    private static bool TryMapMikroApiResultRowsByRowNo(
        string rawResponse,
        int expectedLineCount,
        out Dictionary<int, Guid> result)
    {
        result = new Dictionary<int, Guid>(expectedLineCount);
        var responseRows = MikroApiCreatedDocumentResultReader.ReadRows(rawResponse);
        if (responseRows.Count < expectedLineCount)
        {
            return false;
        }

        for (var rowNo = 0; rowNo < expectedLineCount; rowNo++)
        {
            result[rowNo] = responseRows[rowNo].Guid;
        }

        return true;
    }

    private async Task<CreateIssuedWarehouseOrderResponse?> TryRecoverWarehouseOrderResponseAsync(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedWarehouseOrderRequest request,
        IReadOnlyList<CreateIssuedWarehouseOrderLineRequest> lines,
        DateTime orderDate,
        DateTime deliveryDate,
        string writeConnectionName,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_evrakno_seri == documentSerie &&
                order.ssip_evrakno_sira == documentOrderNo)
            .Select(order => new
            {
                order.ssip_tarih,
                order.ssip_teslim_tarih,
                order.ssip_evrakno_seri,
                order.ssip_evrakno_sira,
                order.ssip_girdepo,
                order.ssip_cikdepo,
                order.ssip_satirno,
                order.ssip_stok_kod,
                order.ssip_miktar,
                order.ssip_b_fiyat,
                order.ssip_tutar,
                order.ssip_birim_pntr,
                order.ssip_paket_kod,
                order.ssip_projekodu,
                order.ssip_sormerkezi,
                order.ssip_rezervasyon_miktari,
                order.ssip_iptal
            })
            .ToListAsync(cancellationToken);

        var match = IssuedWarehouseOrderRecoveryMatcher.Match(request, lines, rows.Select(row =>
            new IssuedWarehouseOrderRecoveryRow(
                row.ssip_tarih,
                row.ssip_teslim_tarih,
                row.ssip_evrakno_seri,
                row.ssip_evrakno_sira,
                row.ssip_satirno,
                row.ssip_stok_kod,
                row.ssip_miktar,
                row.ssip_b_fiyat,
                row.ssip_tutar,
                row.ssip_girdepo,
                row.ssip_cikdepo,
                row.ssip_birim_pntr,
                row.ssip_paket_kod,
                row.ssip_projekodu,
                row.ssip_sormerkezi,
                row.ssip_rezervasyon_miktari,
                row.ssip_iptal)).ToArray());

        if (match is IssuedWarehouseOrderMatch.Missing or IssuedWarehouseOrderMatch.Incomplete)
        {
            return null;
        }

        if (match == IssuedWarehouseOrderMatch.Mismatch)
        {
            throw StockMovementRecoveryMatcher.ContentMismatch();
        }

        var firstRow = rows[0];

        return new CreateIssuedWarehouseOrderResponse(
            firstRow.ssip_evrakno_seri ?? documentSerie,
            firstRow.ssip_evrakno_sira ?? documentOrderNo,
            firstRow.ssip_tarih?.Date ?? orderDate,
            rows.Max(row => row.ssip_teslim_tarih)?.Date ?? deliveryDate,
            firstRow.ssip_girdepo ?? request.InWarehouseNo,
            firstRow.ssip_cikdepo ?? request.OutWarehouseNo,
            rows.Count,
            rows.Sum(row => row.ssip_miktar ?? 0d),
            writeConnectionName);
    }

    private async Task<CreateIssuedWarehouseOrderResponse?> TryRecoverOfflineResponseAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.ClientRequestId.HasValue)
        {
            return null;
        }

        var traceParts = ResolveOfflineTraceParts(request.ClientRequestId);
        var traceRows = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_girdepo == request.InWarehouseNo &&
                order.ssip_cikdepo == request.OutWarehouseNo &&
                order.ssip_special1 == traceParts.First &&
                order.ssip_special2 == traceParts.Second &&
                order.ssip_special3 == traceParts.Third)
            .Select(order => new
            {
                order.ssip_evrakno_seri,
                order.ssip_evrakno_sira,
                order.ssip_tarih,
                order.ssip_teslim_tarih
            })
            .ToListAsync(cancellationToken);

        if (traceRows.Count == 0)
        {
            return null;
        }

        var documentKeys = traceRows
            .Select(row => new { row.ssip_evrakno_seri, row.ssip_evrakno_sira })
            .Distinct()
            .ToArray();
        if (documentKeys.Length != 1 ||
            string.IsNullOrWhiteSpace(documentKeys[0].ssip_evrakno_seri) ||
            !documentKeys[0].ssip_evrakno_sira.HasValue)
        {
            throw StockMovementRecoveryMatcher.ContentMismatch();
        }

        var orderDates = traceRows
            .Where(row => row.ssip_tarih.HasValue)
            .Select(row => row.ssip_tarih!.Value.Date)
            .Distinct()
            .ToArray();
        var deliveryDates = traceRows
            .Where(row => row.ssip_teslim_tarih.HasValue)
            .Select(row => row.ssip_teslim_tarih!.Value.Date)
            .Distinct()
            .ToArray();
        if (orderDates.Length > 1 || deliveryDates.Length > 1)
        {
            throw StockMovementRecoveryMatcher.ContentMismatch();
        }

        var recoveredOrderDate = orderDates.SingleOrDefault();
        var recoveredDeliveryDate = deliveryDates.SingleOrDefault();
        var recoveryRequest = request with
        {
            OrderDate = request.OrderDate ?? (recoveredOrderDate == default ? null : recoveredOrderDate),
            DeliveryDate = request.DeliveryDate ?? (recoveredDeliveryDate == default ? null : recoveredDeliveryDate)
        };
        var orderDate = (recoveryRequest.OrderDate ?? DateTime.Today).Date;
        var deliveryDate = (recoveryRequest.DeliveryDate ?? orderDate).Date;

        var documentOrderNo = documentKeys[0].ssip_evrakno_sira.GetValueOrDefault();
        return await TryRecoverWarehouseOrderResponseAsync(
            documentKeys[0].ssip_evrakno_seri!,
            documentOrderNo,
            recoveryRequest,
            recoveryRequest.Lines.ToArray(),
            orderDate,
            deliveryDate,
            mikroWriteOptions.Value.ConnectionStringName,
            cancellationToken);
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order => order.ssip_evrakno_seri == documentSerie)
            .MaxAsync(order => order.ssip_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<Dictionary<int, Guid>> GetOrderLineGuidByRowNoAsync(
        string documentSerie,
        int documentOrderNo,
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        return await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_evrakno_seri == documentSerie &&
                order.ssip_evrakno_sira == documentOrderNo &&
                order.ssip_girdepo == request.InWarehouseNo &&
                order.ssip_cikdepo == request.OutWarehouseNo &&
                order.ssip_satirno.HasValue)
            .Select(order => new
            {
                RowNo = order.ssip_satirno!.Value,
                order.ssip_Guid
            })
            .ToDictionaryAsync(row => row.RowNo, row => row.ssip_Guid, cancellationToken);
    }

    private async Task TryCreateGreenGrocerOrderLineSnapshotsAsync(
        CreateIssuedWarehouseOrderRequest request,
        IReadOnlyList<CreateIssuedWarehouseOrderLineRequest> lines,
        IReadOnlyDictionary<int, Guid> lineGuidByRowNo,
        string documentSerie,
        int documentOrderNo,
        DateTime orderDate,
        CancellationToken cancellationToken)
    {
        if (!greenGrocerProductCaseOptions.CurrentValue.Enabled ||
            request.OutWarehouseNo != GreenGrocerWarehouseNo ||
            !request.CreatedByUserId.HasValue ||
            lines.All(line => line.GreenGrocerCase is null))
        {
            return;
        }

        try
        {
            var requestedSnapshots = lines
                .Select((line, rowNo) => new { line, rowNo, line.GreenGrocerCase })
                .Where(item => item.GreenGrocerCase is not null &&
                               lineGuidByRowNo.ContainsKey(item.rowNo))
                .ToArray();

            if (requestedSnapshots.Length == 0)
            {
                return;
            }

            var lineGuids = requestedSnapshots
                .Select(item => lineGuidByRowNo[item.rowNo])
                .ToArray();
            var existingLineGuids = await authDbContext.GreenGrocerOrderLineSnapshots
                .AsNoTracking()
                .Where(snapshot => lineGuids.Contains(snapshot.WarehouseOrderLineGuid))
                .Select(snapshot => snapshot.WarehouseOrderLineGuid)
                .ToListAsync(cancellationToken);
            var existingLineGuidSet = existingLineGuids.ToHashSet();
            var createdAtUtc = clock.UtcNow;
            var snapshots = requestedSnapshots
                .Where(item => !existingLineGuidSet.Contains(lineGuidByRowNo[item.rowNo]))
                .Select(item =>
                {
                    var snapshot = item.GreenGrocerCase!;
                    return new GreenGrocerOrderLineSnapshot(
                        Guid.NewGuid(),
                        lineGuidByRowNo[item.rowNo],
                        documentSerie,
                        documentOrderNo,
                        item.rowNo,
                        orderDate,
                        request.OutWarehouseNo,
                        request.InWarehouseNo,
                        item.line.StockCode,
                        Round(snapshot.InputQuantity),
                        snapshot.InputMode,
                        snapshot.ConversionMode,
                        RoundOrNull(snapshot.AverageKgPerCase),
                        RoundOrNull(snapshot.UnitsPerCase),
                        Round(snapshot.EstimatedQuantity),
                        snapshot.MicroUnit,
                        snapshot.AverageSource,
                        snapshot.AverageRecordCount,
                        snapshot.AverageCaseCount,
                        RoundOrNull(snapshot.CoefficientOfVariation),
                        snapshot.Confidence,
                        request.CreatedByUserId.Value,
                        createdAtUtc);
                })
                .ToArray();

            if (snapshots.Length == 0)
            {
                return;
            }

            await authDbContext.GreenGrocerOrderLineSnapshots.AddRangeAsync(snapshots, cancellationToken);
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Green grocer order line snapshots could not be saved. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, InWarehouseNo={InWarehouseNo}, OutWarehouseNo={OutWarehouseNo}",
                documentSerie,
                documentOrderNo,
                request.InWarehouseNo,
                request.OutWarehouseNo);
        }
    }

    private static DEPOLAR_ARASI_SIPARISLER CreateOrderLine(
        CreateIssuedWarehouseOrderRequest request,
        CreateIssuedWarehouseOrderLineRequest line,
        int rowNo,
        DateTime now,
        DateTime orderDate,
        DateTime deliveryDate,
        string documentSerie,
        int documentOrderNo,
        (string First, string Second, string Third) traceParts)
    {
        var unitPrice = line.UnitPrice;
        var amount = line.Quantity * unitPrice;

        return new DEPOLAR_ARASI_SIPARISLER
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_DBCno = 0,
            ssip_SpecRECno = 0,
            ssip_iptal = false,
            ssip_fileid = FileId,
            ssip_hidden = false,
            ssip_kilitli = false,
            ssip_degisti = false,
            ssip_checksum = 0,
            ssip_create_user = MikroUserNo,
            ssip_create_date = now,
            ssip_lastup_user = MikroUserNo,
            ssip_lastup_date = now,
            ssip_special1 = traceParts.First,
            ssip_special2 = traceParts.Second,
            ssip_special3 = traceParts.Third,
            ssip_firmano = 0,
            ssip_subeno = 0,
            ssip_tarih = orderDate,
            ssip_teslim_tarih = deliveryDate,
            ssip_evrakno_seri = documentSerie,
            ssip_evrakno_sira = documentOrderNo,
            ssip_satirno = rowNo,
            ssip_belgeno = string.Empty,
            ssip_belge_tarih = orderDate,
            ssip_stok_kod = line.StockCode.Trim(),
            ssip_miktar = line.Quantity,
            ssip_b_fiyat = unitPrice,
            ssip_tutar = amount,
            ssip_teslim_miktar = 0d,
            ssip_aciklama = NormalizeText(line.Description ?? request.Description),
            ssip_girdepo = request.InWarehouseNo,
            ssip_cikdepo = request.OutWarehouseNo,
            ssip_kapat_fl = false,
            ssip_birim_pntr = Convert.ToByte(line.UnitPointer),
            ssip_fiyat_liste_no = 0,
            ssip_stal_uid = Guid.Empty,
            ssip_paket_kod = NormalizeText(line.PackageCode),
            ssip_kapatmanedenkod = string.Empty,
            ssip_projekodu = NormalizeText(line.ProjectCode),
            ssip_sormerkezi = NormalizeText(line.ResponsibilityCenter),
            ssip_gecerlilik_tarihi = MikroEmptyDate,
            ssip_rezervasyon_miktari = line.RecommendedQuantity ?? 0d,
            ssip_rezerveden_teslim_edilen = 0d
        };
    }

    private static void Validate(CreateIssuedWarehouseOrderRequest request)
    {
        if (request.InWarehouseNo <= 0)
        {
            throw new ArgumentException("In warehouse no must be greater than zero.", nameof(request.InWarehouseNo));
        }

        if (request.OutWarehouseNo <= 0)
        {
            throw new ArgumentException("Out warehouse no must be greater than zero.", nameof(request.OutWarehouseNo));
        }

        if (request.InWarehouseNo == request.OutWarehouseNo)
        {
            throw new ArgumentException("In warehouse and out warehouse can not be the same.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one order line is required.", nameof(request.Lines));
        }

        if (request.DeliveryDate.HasValue &&
            request.OrderDate.HasValue &&
            request.DeliveryDate.Value.Date < request.OrderDate.Value.Date)
        {
            throw new ArgumentException("Delivery date can not be earlier than order date.", nameof(request.DeliveryDate));
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

            CreateQuantityLimits.EnsureWithinMaximum(line.Quantity, nameof(request.Lines));

            if (line.UnitPrice < 0)
            {
                throw new ArgumentException("Line unit price can not be negative.", nameof(request.Lines));
            }

            if (line.UnitPointer is < 1 or > byte.MaxValue)
            {
                throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(request.Lines));
            }

            if (line.RecommendedQuantity is < 0)
            {
                throw new ArgumentException("Line recommended quantity can not be negative.", nameof(request.Lines));
            }

            if (line.RecommendedQuantity.HasValue)
            {
                CreateQuantityLimits.EnsureWithinMaximum(line.RecommendedQuantity.Value, nameof(request.Lines));
            }

            if (line.GreenGrocerCase is not null)
            {
                ValidateGreenGrocerSnapshot(line.GreenGrocerCase, line.Quantity);
            }
        }
    }

    private static void ValidateGreenGrocerSnapshot(
        GreenGrocerOrderLineSnapshotRequest snapshot,
        double lineQuantity)
    {
        if (snapshot.InputQuantity <= 0)
        {
            throw new ArgumentException("Green grocer input quantity must be greater than zero.");
        }

        CreateQuantityLimits.EnsureWithinMaximum(snapshot.InputQuantity, nameof(snapshot.InputQuantity));

        if (snapshot.EstimatedQuantity <= 0)
        {
            throw new ArgumentException("Green grocer estimated quantity must be greater than zero.");
        }

        CreateQuantityLimits.EnsureWithinMaximum(snapshot.EstimatedQuantity, nameof(snapshot.EstimatedQuantity));

        if (Math.Abs(snapshot.EstimatedQuantity - lineQuantity) > 0.0001d)
        {
            throw new ArgumentException("Green grocer estimated quantity must match the order line quantity.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.InputMode) ||
            string.IsNullOrWhiteSpace(snapshot.ConversionMode) ||
            string.IsNullOrWhiteSpace(snapshot.MicroUnit) ||
            string.IsNullOrWhiteSpace(snapshot.AverageSource) ||
            string.IsNullOrWhiteSpace(snapshot.Confidence))
        {
            throw new ArgumentException("Green grocer snapshot fields are required.");
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static (string First, string Second, string Third) ResolveOfflineTraceParts(
        Guid? clientRequestId)
    {
        if (!clientRequestId.HasValue)
        {
            return ("0", string.Empty, string.Empty);
        }

        var traceKey = MobileOfflineSyncService.ToTraceKey(clientRequestId.Value)[..12];
        return (traceKey[..4], traceKey[4..8], traceKey[8..12]);
    }

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double? RoundOrNull(double? value) =>
        value.HasValue ? Round(value.Value) : null;
}
