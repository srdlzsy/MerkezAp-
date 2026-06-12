using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.Common.OfflineSync;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

public sealed class CreateCompanyReceivingUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions,
    MobileOfflineSyncService mobileOfflineSyncService,
    IOptionsMonitor<MikroWriteRoutingOptions> mikroWriteRoutingOptions,
    MikroApiClient mikroApiClient,
    ILogger<CreateCompanyReceivingUseCase> logger)
    : ICreateCompanyReceivingUseCase
{
    private const short MovementFileId = 16;
    private const short MikroUserNo = 39;
    private const byte ReceivingReceiptDocumentType = 13;
    private const byte CompanyDispatchDocumentType = 1;
    private const byte IncomingMovementType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte MovementGenre = 0;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const byte IssuedCompanyOrderType = 1;
    private const byte NormalOrderGenre = 0;
    private const int FirstDocumentOrderNo = 0;
    private const int DerivedDocumentOrderNoLength = 9;
    private const int MaxDocumentSerieLength = 20;
    private const double QuantityTolerance = 0.000001d;
    private const string OfflineOperationCode = "mal-kabul-islemleri.firma-mal-kabulleri.create";
    private const string ReturnStatusNone = "Yok";
    private const string ReturnStatusPending = "IadeBekliyor";
    private const string ReturnStatusCreated = "IadeOlusturuldu";
    private const string ReturnEDespatchStatusNone = "Yok";
    private const string ReturnEDespatchStatusPending = "GonderimBekliyor";
    private const string IrsaliyeKaydetPath = "/Api/apiMethods/IrsaliyeKaydetV2";
    private const int MikroApiRecoveryAttemptCount = 5;
    private const int MikroApiRecoveryDelayMilliseconds = 250;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<CreateCompanyReceivingResponse> ExecuteAsync(
        CreateCompanyReceivingRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        if (request.ClientRequestId.HasValue)
        {
            var acquireResult = await mobileOfflineSyncService.AcquireAsync<CreateCompanyReceivingRequest, CreateCompanyReceivingResponse>(
                OfflineOperationCode,
                request.RequestedByUserId,
                request.WarehouseNo,
                request.ClientRequestId.Value,
                request,
                (storedRequestPayload, innerCancellationToken) => TryRecoverOfflineResponseAsync(
                    request.WarehouseNo,
                    request.ClientRequestId.Value,
                    storedRequestPayload,
                    innerCancellationToken),
                cancellationToken);

            if (acquireResult.State == MobileOfflineSyncAcquireState.Completed)
            {
                return acquireResult.Response!;
            }

            if (acquireResult.State == MobileOfflineSyncAcquireState.Processing)
            {
                throw new InvalidOperationException(
                    "An offline company receiving sync request with the same clientRequestId is already being processed.");
            }
        }

        if (mikroWriteRoutingOptions.CurrentValue.CompanyReceiving == MikroWriteMode.MikroApi)
        {
            return await ExecuteMikroApiWithOfflineStateAsync(request, cancellationToken);
        }

        if (mikroWriteRoutingOptions.CurrentValue.CompanyReceiving == MikroWriteMode.DualShadow)
        {
            logger.LogWarning(
                "MikroWriteRouting:CompanyReceiving is DualShadow. IrsaliyeKaydetV2 has no dry-run contract, so only the database write path will run.");
        }

        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var customerCode = request.CustomerCode.Trim();
        var lines = request.Lines.ToArray();
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();
        var offlineTraceKey = request.ClientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value)
            : string.Empty;

        try
        {
            var response = await executionStrategy.ExecuteAsync(async () =>
            {
                mikroWriteDbContext.ChangeTracker.Clear();
                await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

                try
                {
                    var customer = await GetCustomerAsync(customerCode, cancellationToken);
                    var customerAddressNo = ResolveCustomerAddressNo(customer);
                    var resolvedDocumentIdentity = await ResolveDocumentIdentityAsync(
                        request.DocumentNo,
                        customer,
                        customerCode,
                        request.WarehouseNo,
                        cancellationToken);
                    var documentSerie = resolvedDocumentIdentity.DocumentSerie;
                    var documentOrderNo = resolvedDocumentIdentity.DocumentOrderNo;
                    var documentNo = BuildDocumentNo(documentSerie, documentOrderNo);
                    await EnsureDocumentDoesNotExistAsync(
                        request.WarehouseNo,
                        customerCode,
                        documentNo,
                        cancellationToken);

                    var ordersByGuid = await LoadOrdersAsync(
                        request,
                        lines,
                        customerCode,
                        cancellationToken);
                    await EnsureDocumentIdentityDoesNotExistAsync(
                        request.WarehouseNo,
                        documentSerie,
                        documentOrderNo,
                        cancellationToken);
                    var shouldCreateReturnDocument = request.AutoCreateReturnForPartialAcceptance &&
                        lines.Any(line => CalculateReturnQuantity(line) > QuantityTolerance);
                    var returnDocumentSerie = shouldCreateReturnDocument
                        ? BuildReturnDocumentSerie(request.WarehouseNo)
                        : null;
                    var returnDocumentOrderNo = shouldCreateReturnDocument
                        ? await GetNextReturnDocumentOrderNoAsync(returnDocumentSerie!, cancellationToken)
                        : (int?)null;
                    var movements = new List<STOK_HAREKETLERI>();
                    var returnMovements = new List<STOK_HAREKETLERI>();
                    var results = new List<CreateCompanyReceivingLineResultDto>();
                    var rowNo = 0;
                    var returnRowNo = 0;

                    for (var sourceLineNo = 0; sourceLineNo < lines.Length; sourceLineNo++)
                    {
                        var line = lines[sourceLineNo];
                        var orderGuid = NormalizeOrderGuid(line.OrderGuid);
                        var dispatchQuantity = ResolveDispatchQuantity(line);
                        var physicalAcceptedQuantity = ResolvePhysicalAcceptedQuantity(line);
                        var returnQuantity = CalculateReturnQuantity(line);
                        var physicalAcceptedRemaining = physicalAcceptedQuantity;
                        var returnInfoForNextMovement = CreateReturnInfo(
                            request,
                            line,
                            customerCode,
                            customerAddressNo,
                            returnDocumentSerie,
                            returnDocumentOrderNo,
                            returnMovements,
                            documentSerie,
                            documentOrderNo,
                            movementDate,
                            documentDate,
                            now,
                            sourceLineNo,
                            ref returnRowNo,
                            returnQuantity,
                            offlineTraceKey);

                        if (orderGuid is null)
                        {
                            var movementPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                                dispatchQuantity,
                                ref physicalAcceptedRemaining);
                            AddMovement(
                                movements,
                                results,
                                request,
                                line,
                                customerCode,
                                documentSerie,
                                documentOrderNo,
                                documentNo,
                                movementDate,
                                documentDate,
                                now,
                                sourceLineNo,
                                ref rowNo,
                                dispatchQuantity,
                                null,
                                "orderless",
                                dispatchQuantity,
                                0d,
                                0d,
                                0d,
                                movementPhysicalAcceptedQuantity,
                                ConsumeReturnInfo(ref returnInfoForNextMovement),
                                offlineTraceKey);

                            continue;
                        }

                        var order = ordersByGuid[orderGuid.Value];
                        var remainingBefore = CalculateRemainingQuantity(order);

                        if (remainingBefore <= QuantityTolerance)
                        {
                            throw new InvalidOperationException(
                                $"Order line has no remaining quantity for company receiving: {orderGuid.Value}");
                        }

                        if (dispatchQuantity > remainingBefore + QuantityTolerance)
                        {
                            if (!request.AllowOrderOverReceiving)
                            {
                                throw new InvalidOperationException(
                                    $"Receiving quantity is greater than remaining order quantity for order line: {orderGuid.Value}");
                            }

                            var movementPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                                remainingBefore,
                                ref physicalAcceptedRemaining);
                            AddMovement(
                                movements,
                                results,
                                request,
                                line,
                                customerCode,
                                documentSerie,
                                documentOrderNo,
                                documentNo,
                                movementDate,
                                documentDate,
                                now,
                                sourceLineNo,
                                ref rowNo,
                                remainingBefore,
                                orderGuid.Value,
                                "order-linked",
                                dispatchQuantity,
                                remainingBefore,
                                remainingBefore,
                                0d,
                                movementPhysicalAcceptedQuantity,
                                ConsumeReturnInfo(ref returnInfoForNextMovement),
                                offlineTraceKey);
                            ApplyOrderDelivery(order, remainingBefore, now);

                            var overflowQuantity = dispatchQuantity - remainingBefore;
                            movementPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                                overflowQuantity,
                                ref physicalAcceptedRemaining);
                            AddMovement(
                                movements,
                                results,
                                request,
                                line,
                                customerCode,
                                documentSerie,
                                documentOrderNo,
                                documentNo,
                                movementDate,
                                documentDate,
                                now,
                                sourceLineNo,
                                ref rowNo,
                                overflowQuantity,
                                null,
                                "order-overflow",
                                dispatchQuantity,
                                0d,
                                0d,
                                0d,
                                movementPhysicalAcceptedQuantity,
                                ConsumeReturnInfo(ref returnInfoForNextMovement),
                                offlineTraceKey);

                            continue;
                        }

                        var remainingAfter = remainingBefore - dispatchQuantity;
                        var linkedPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                            dispatchQuantity,
                            ref physicalAcceptedRemaining);
                        AddMovement(
                            movements,
                            results,
                            request,
                            line,
                            customerCode,
                            documentSerie,
                            documentOrderNo,
                            documentNo,
                            movementDate,
                            documentDate,
                            now,
                            sourceLineNo,
                            ref rowNo,
                            dispatchQuantity,
                            orderGuid.Value,
                            "order-linked",
                            dispatchQuantity,
                            dispatchQuantity,
                            remainingBefore,
                            remainingAfter,
                            linkedPhysicalAcceptedQuantity,
                            ConsumeReturnInfo(ref returnInfoForNextMovement),
                            offlineTraceKey);
                        ApplyOrderDelivery(order, dispatchQuantity, now);
                    }

                    await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(movements, cancellationToken);
                    if (returnMovements.Count > 0)
                    {
                        await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(returnMovements, cancellationToken);
                    }

                    await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return new CreateCompanyReceivingResponse(
                        documentSerie,
                        documentOrderNo,
                        movementDate,
                        documentDate,
                        documentNo,
                        request.WarehouseNo,
                        customerCode,
                        movements.Count,
                        movements.Sum(movement => movement.sth_miktar ?? 0d),
                        results.Sum(line => line.OrderLinkedQuantity),
                        results.Sum(line => line.OrderlessQuantity),
                        results
                            .Where(line => line.ReceivingMode == "order-overflow")
                            .Sum(line => line.AcceptedQuantity),
                        movements.Sum(movement => movement.sth_tutar ?? 0d),
                        options.ConnectionStringName,
                        results,
                        movements.Sum(movement => movement.sth_miktar ?? 0d),
                        results.Sum(line => line.PhysicalAcceptedQuantity),
                        results.Sum(line => line.ReturnQuantity),
                        returnMovements.Count,
                        returnMovements.Count > 0 ? returnDocumentSerie : null,
                        returnMovements.Count > 0 ? returnDocumentOrderNo : null,
                        returnMovements.Count > 0 ? ReturnEDespatchStatusPending : ReturnEDespatchStatusNone);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });

            if (request.ClientRequestId.HasValue)
            {
                await mobileOfflineSyncService.CompleteAsync(
                    OfflineOperationCode,
                    request.RequestedByUserId,
                    request.ClientRequestId.Value,
                    response,
                    cancellationToken);
            }

            return response;
        }
        catch (Exception exception)
        {
            if (request.ClientRequestId.HasValue)
            {
                await TryMarkFailedAsync(
                    request.RequestedByUserId,
                    request.ClientRequestId.Value,
                    exception.Message,
                    cancellationToken);
            }

            throw;
        }
    }

    private async Task<CreateCompanyReceivingResponse> ExecuteMikroApiWithOfflineStateAsync(
        CreateCompanyReceivingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExecuteMikroApiAsync(request, cancellationToken);

            if (request.ClientRequestId.HasValue)
            {
                await mobileOfflineSyncService.CompleteAsync(
                    OfflineOperationCode,
                    request.RequestedByUserId,
                    request.ClientRequestId.Value,
                    response,
                    cancellationToken);
            }

            return response;
        }
        catch (Exception exception)
        {
            if (request.ClientRequestId.HasValue)
            {
                await TryMarkFailedAsync(
                    request.RequestedByUserId,
                    request.ClientRequestId.Value,
                    exception.Message,
                    cancellationToken);
            }

            throw;
        }
    }

    private async Task<CreateCompanyReceivingResponse> ExecuteMikroApiAsync(
        CreateCompanyReceivingRequest request,
        CancellationToken cancellationToken)
    {
        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var movementDate = (request.MovementDate ?? DateTime.Today).Date;
        var documentDate = (request.DocumentDate ?? movementDate).Date;
        var customerCode = request.CustomerCode.Trim();
        var lines = request.Lines.ToArray();
        var offlineTraceKey = request.ClientRequestId.HasValue
            ? MobileOfflineSyncService.ToTraceKey(request.ClientRequestId.Value)
            : string.Empty;

        mikroWriteDbContext.ChangeTracker.Clear();

        var customer = await GetCustomerAsync(customerCode, cancellationToken);
        var customerAddressNo = ResolveCustomerAddressNo(customer);
        var resolvedDocumentIdentity = await ResolveDocumentIdentityAsync(
            request.DocumentNo,
            customer,
            customerCode,
            request.WarehouseNo,
            cancellationToken);
        var documentSerie = resolvedDocumentIdentity.DocumentSerie;
        var documentOrderNo = resolvedDocumentIdentity.DocumentOrderNo;
        var documentNo = BuildDocumentNo(documentSerie, documentOrderNo);
        await EnsureDocumentDoesNotExistAsync(
            request.WarehouseNo,
            customerCode,
            documentNo,
            cancellationToken);

        var ordersByGuid = await LoadOrdersAsync(
            request,
            lines,
            customerCode,
            cancellationToken);
        await EnsureDocumentIdentityDoesNotExistAsync(
            request.WarehouseNo,
            documentSerie,
            documentOrderNo,
            cancellationToken);

        var shouldCreateReturnDocument = request.AutoCreateReturnForPartialAcceptance &&
            lines.Any(line => CalculateReturnQuantity(line) > QuantityTolerance);
        var returnDocumentSerie = shouldCreateReturnDocument
            ? BuildReturnDocumentSerie(request.WarehouseNo)
            : null;
        var returnDocumentOrderNo = shouldCreateReturnDocument
            ? await GetNextReturnDocumentOrderNoAsync(returnDocumentSerie!, cancellationToken)
            : (int?)null;
        var movements = new List<STOK_HAREKETLERI>();
        var returnMovements = new List<STOK_HAREKETLERI>();
        var results = new List<CreateCompanyReceivingLineResultDto>();
        var rowNo = 0;
        var returnRowNo = 0;

        for (var sourceLineNo = 0; sourceLineNo < lines.Length; sourceLineNo++)
        {
            var line = lines[sourceLineNo];
            var orderGuid = NormalizeOrderGuid(line.OrderGuid);
            var dispatchQuantity = ResolveDispatchQuantity(line);
            var physicalAcceptedQuantity = ResolvePhysicalAcceptedQuantity(line);
            var returnQuantity = CalculateReturnQuantity(line);
            var physicalAcceptedRemaining = physicalAcceptedQuantity;
            var returnInfoForNextMovement = CreateReturnInfo(
                request,
                line,
                customerCode,
                customerAddressNo,
                returnDocumentSerie,
                returnDocumentOrderNo,
                returnMovements,
                documentSerie,
                documentOrderNo,
                movementDate,
                documentDate,
                now,
                sourceLineNo,
                ref returnRowNo,
                returnQuantity,
                offlineTraceKey);

            if (orderGuid is null)
            {
                var movementPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                    dispatchQuantity,
                    ref physicalAcceptedRemaining);
                AddMovement(
                    movements,
                    results,
                    request,
                    line,
                    customerCode,
                    documentSerie,
                    documentOrderNo,
                    documentNo,
                    movementDate,
                    documentDate,
                    now,
                    sourceLineNo,
                    ref rowNo,
                    dispatchQuantity,
                    null,
                    "orderless",
                    dispatchQuantity,
                    0d,
                    0d,
                    0d,
                    movementPhysicalAcceptedQuantity,
                    ConsumeReturnInfo(ref returnInfoForNextMovement),
                    offlineTraceKey);

                continue;
            }

            var order = ordersByGuid[orderGuid.Value];
            var remainingBefore = CalculateRemainingQuantity(order);

            if (remainingBefore <= QuantityTolerance)
            {
                throw new InvalidOperationException(
                    $"Order line has no remaining quantity for company receiving: {orderGuid.Value}");
            }

            if (dispatchQuantity > remainingBefore + QuantityTolerance)
            {
                if (!request.AllowOrderOverReceiving)
                {
                    throw new InvalidOperationException(
                        $"Receiving quantity is greater than remaining order quantity for order line: {orderGuid.Value}");
                }

                var movementPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                    remainingBefore,
                    ref physicalAcceptedRemaining);
                AddMovement(
                    movements,
                    results,
                    request,
                    line,
                    customerCode,
                    documentSerie,
                    documentOrderNo,
                    documentNo,
                    movementDate,
                    documentDate,
                    now,
                    sourceLineNo,
                    ref rowNo,
                    remainingBefore,
                    orderGuid.Value,
                    "order-linked",
                    dispatchQuantity,
                    remainingBefore,
                    remainingBefore,
                    0d,
                    movementPhysicalAcceptedQuantity,
                    ConsumeReturnInfo(ref returnInfoForNextMovement),
                    offlineTraceKey);
                ApplyOrderDelivery(order, remainingBefore, now);

                var overflowQuantity = dispatchQuantity - remainingBefore;
                movementPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                    overflowQuantity,
                    ref physicalAcceptedRemaining);
                AddMovement(
                    movements,
                    results,
                    request,
                    line,
                    customerCode,
                    documentSerie,
                    documentOrderNo,
                    documentNo,
                    movementDate,
                    documentDate,
                    now,
                    sourceLineNo,
                    ref rowNo,
                    overflowQuantity,
                    null,
                    "order-overflow",
                    dispatchQuantity,
                    0d,
                    0d,
                    0d,
                    movementPhysicalAcceptedQuantity,
                    ConsumeReturnInfo(ref returnInfoForNextMovement),
                    offlineTraceKey);

                continue;
            }

            var remainingAfter = remainingBefore - dispatchQuantity;
            var linkedPhysicalAcceptedQuantity = ConsumePhysicalAcceptedQuantity(
                dispatchQuantity,
                ref physicalAcceptedRemaining);
            AddMovement(
                movements,
                results,
                request,
                line,
                customerCode,
                documentSerie,
                documentOrderNo,
                documentNo,
                movementDate,
                documentDate,
                now,
                sourceLineNo,
                ref rowNo,
                dispatchQuantity,
                orderGuid.Value,
                "order-linked",
                dispatchQuantity,
                dispatchQuantity,
                remainingBefore,
                remainingAfter,
                linkedPhysicalAcceptedQuantity,
                ConsumeReturnInfo(ref returnInfoForNextMovement),
                offlineTraceKey);
            ApplyOrderDelivery(order, dispatchQuantity, now);
        }

        var recovered = movements.Count == 0
            ? RecoveredCompanyReceivingCreate.Empty(
                documentSerie,
                documentOrderNo,
                movementDate,
                documentDate,
                documentNo,
                request.WarehouseNo,
                customerCode)
            : await CreateCompanyReceivingMovementsWithMikroApiAsync(
                request,
                customerCode,
                documentSerie,
                documentOrderNo,
                documentNo,
                movementDate,
                documentDate,
                movements,
                cancellationToken);
        var adjustedResults = results
            .Select(result =>
            {
                if (!recovered.MovementGuidByRowNo.TryGetValue(result.MovementLineNo, out var movementGuid))
                {
                    throw new InvalidOperationException(
                        "Mikro API company receiving line could not be matched to the created movement row.");
                }

                return result with { MovementGuid = movementGuid };
            })
            .ToArray();

        await PersistMikroApiSideEffectsAsync(
            returnMovements,
            adjustedResults.Any(line => line.IsOrderLinked && line.OrderLinkedQuantity > QuantityTolerance),
            cancellationToken);

        return new CreateCompanyReceivingResponse(
            recovered.DocumentSerie,
            recovered.DocumentOrderNo,
            recovered.MovementDate,
            recovered.DocumentDate,
            recovered.DocumentNo,
            recovered.WarehouseNo,
            recovered.CustomerCode,
            recovered.LineCount,
            recovered.TotalQuantity,
            adjustedResults.Sum(line => line.OrderLinkedQuantity),
            adjustedResults.Sum(line => line.OrderlessQuantity),
            adjustedResults
                .Where(line => line.ReceivingMode == "order-overflow")
                .Sum(line => line.AcceptedQuantity),
            recovered.TotalAmount,
            options.ConnectionStringName,
            adjustedResults,
            recovered.TotalQuantity,
            adjustedResults.Sum(line => line.PhysicalAcceptedQuantity),
            adjustedResults.Sum(line => line.ReturnQuantity),
            returnMovements.Count,
            returnMovements.Count > 0 ? returnDocumentSerie : null,
            returnMovements.Count > 0 ? returnDocumentOrderNo : null,
            returnMovements.Count > 0 ? ReturnEDespatchStatusPending : ReturnEDespatchStatusNone);
    }

    public Task<OfflineSyncStatusDto<CreateCompanyReceivingResponse>> GetOfflineSyncStatusAsync(
        int warehouseNo,
        Guid requestedByUserId,
        Guid clientRequestId,
        CancellationToken cancellationToken) =>
        mobileOfflineSyncService.GetStatusAsync<CreateCompanyReceivingResponse>(
            OfflineOperationCode,
            requestedByUserId,
            clientRequestId,
            (storedRequestPayload, innerCancellationToken) => TryRecoverOfflineResponseAsync(
                warehouseNo,
                clientRequestId,
                storedRequestPayload,
                innerCancellationToken),
            cancellationToken);

    private async Task<RecoveredCompanyReceivingCreate> CreateCompanyReceivingMovementsWithMikroApiAsync(
        CreateCompanyReceivingRequest request,
        string customerCode,
        string documentSerie,
        int documentOrderNo,
        string documentNo,
        DateTime movementDate,
        DateTime documentDate,
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        CancellationToken cancellationToken)
    {
        var payload = CompanyReceivingIrsaliyeMikroApiPayloadFactory.Create(
            movements,
            NormalizeText(request.Description));

        logger.LogInformation(
            "Company receiving create is routed to Mikro API {Path}. DocumentSerie={DocumentSerie}, DocumentOrderNo={DocumentOrderNo}, WarehouseNo={WarehouseNo}, CustomerCode={CustomerCode}, LineCount={LineCount}",
            IrsaliyeKaydetPath,
            documentSerie,
            documentOrderNo,
            request.WarehouseNo,
            customerCode,
            movements.Count);

        var result = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            IrsaliyeKaydetPath,
            payload,
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                result.ErrorMessage ?? "Mikro API company receiving create failed.");
        }

        return await RecoverMikroApiCreateResponseAsync(
            documentSerie,
            documentOrderNo,
            request.WarehouseNo,
            customerCode,
            movements.Count,
            movementDate,
            documentDate,
            documentNo,
            cancellationToken);
    }

    private async Task<RecoveredCompanyReceivingCreate> RecoverMikroApiCreateResponseAsync(
        string documentSerie,
        int documentOrderNo,
        int warehouseNo,
        string customerCode,
        int expectedLineCount,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MikroApiRecoveryAttemptCount; attempt++)
        {
            var response = await TryRecoverCompanyReceivingResponseAsync(
                documentSerie,
                documentOrderNo,
                warehouseNo,
                customerCode,
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
            "Mikro API company receiving create succeeded, but created STOK_HAREKETLERI rows could not be read back.");
    }

    private async Task<RecoveredCompanyReceivingCreate?> TryRecoverCompanyReceivingResponseAsync(
        string documentSerie,
        int documentOrderNo,
        int warehouseNo,
        string customerCode,
        int expectedLineCount,
        DateTime movementDate,
        DateTime documentDate,
        string documentNo,
        CancellationToken cancellationToken)
    {
        var rows = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_cins == MovementGenre &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_evrakno_seri == documentSerie &&
                movement.sth_evrakno_sira == documentOrderNo &&
                movement.sth_giris_depo_no == warehouseNo &&
                movement.sth_cari_kodu == customerCode)
            .Select(movement => new
            {
                movement.sth_Guid,
                movement.sth_tarih,
                movement.sth_belge_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_satirno,
                movement.sth_giris_depo_no,
                movement.sth_cari_kodu,
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
                row.sth_giris_depo_no,
                row.sth_cari_kodu
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one company receiving document matched the same serie and order number.");
        }

        var duplicatedRowNo = rows
            .GroupBy(row => row.sth_satirno ?? -1)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatedRowNo is not null)
        {
            throw new InvalidOperationException(
                "More than one company receiving line matched the same row number.");
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

        return new RecoveredCompanyReceivingCreate(
            firstRow.sth_evrakno_seri ?? documentSerie,
            firstRow.sth_evrakno_sira ?? documentOrderNo,
            firstRow.sth_tarih?.Date ?? movementDate,
            firstRow.sth_belge_tarih?.Date ?? documentDate,
            firstRow.sth_belge_no ?? documentNo,
            firstRow.sth_giris_depo_no ?? warehouseNo,
            firstRow.sth_cari_kodu ?? customerCode,
            rows.Count,
            rows.Sum(row => row.sth_miktar ?? 0d),
            rows.Sum(row => row.sth_tutar ?? 0d),
            movementGuidByRowNo);
    }

    private async Task PersistMikroApiSideEffectsAsync(
        IReadOnlyCollection<STOK_HAREKETLERI> returnMovements,
        bool hasOrderDeliveryUpdates,
        CancellationToken cancellationToken)
    {
        if (returnMovements.Count == 0 && !hasOrderDeliveryUpdates)
        {
            return;
        }

        await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            if (returnMovements.Count > 0)
            {
                await mikroWriteDbContext.STOK_HAREKETLERIs.AddRangeAsync(
                    returnMovements,
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
    }

    private async Task<CARI_HESAPLAR> GetCustomerAsync(
        string customerCode,
        CancellationToken cancellationToken)
    {
        var customer = await mikroWriteDbContext.CARI_HESAPLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.cari_kod == customerCode, cancellationToken);

        if (customer is null)
        {
            throw new KeyNotFoundException("Customer was not found in Mikro write database.");
        }

        return customer;
    }

    private async Task EnsureDocumentDoesNotExistAsync(
        int warehouseNo,
        string customerCode,
        string documentNo,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(documentNo))
        {
            return;
        }

        var exists = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .AnyAsync(
                movement =>
                    movement.sth_evraktip == ReceivingReceiptDocumentType &&
                    movement.sth_tip == IncomingMovementType &&
                    movement.sth_normal_iade == NormalMovement &&
                    movement.sth_giris_depo_no == warehouseNo &&
                    movement.sth_cari_kodu == customerCode &&
                    movement.sth_belge_no == documentNo,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "Company receiving document already exists for the selected customer and document no.");
        }
    }

    private async Task EnsureDocumentIdentityDoesNotExistAsync(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        CancellationToken cancellationToken)
    {
        var exists = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .AnyAsync(
                movement =>
                    movement.sth_evraktip == ReceivingReceiptDocumentType &&
                    movement.sth_tip == IncomingMovementType &&
                    movement.sth_normal_iade == NormalMovement &&
                    movement.sth_giris_depo_no == warehouseNo &&
                    movement.sth_evrakno_seri == documentSerie &&
                    movement.sth_evrakno_sira == documentOrderNo,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "Company receiving document identity already exists for the selected warehouse.");
        }
    }

    private async Task<int> GetNextReturnDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_evrakno_seri == documentSerie)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<int> GetNextReceivingDocumentOrderNoAsync(
        int warehouseNo,
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_giris_depo_no == warehouseNo &&
                movement.sth_evrakno_seri == documentSerie)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task<Dictionary<Guid, SIPARISLER>> LoadOrdersAsync(
        CreateCompanyReceivingRequest request,
        IReadOnlyCollection<CreateCompanyReceivingLineRequest> lines,
        string customerCode,
        CancellationToken cancellationToken)
    {
        var requestedOrderGuids = lines
            .Select(line => NormalizeOrderGuid(line.OrderGuid))
            .Where(orderGuid => orderGuid.HasValue)
            .Select(orderGuid => orderGuid!.Value)
            .Distinct()
            .ToArray();

        if (requestedOrderGuids.Length == 0)
        {
            return new Dictionary<Guid, SIPARISLER>();
        }

        var ordersByGuid = await mikroWriteDbContext.SIPARISLERs
            .Where(order => requestedOrderGuids.Contains(order.sip_Guid))
            .ToDictionaryAsync(order => order.sip_Guid, cancellationToken);

        var missingOrderGuid = requestedOrderGuids.FirstOrDefault(orderGuid => !ordersByGuid.ContainsKey(orderGuid));
        if (missingOrderGuid != Guid.Empty)
        {
            throw new KeyNotFoundException($"Order line was not found in Mikro write database: {missingOrderGuid}");
        }

        foreach (var line in lines)
        {
            var orderGuid = NormalizeOrderGuid(line.OrderGuid);
            if (orderGuid is null)
            {
                continue;
            }

            var order = ordersByGuid[orderGuid.Value];
            ValidateOrderLine(request.WarehouseNo, customerCode, line.StockCode, order);
        }

        return ordersByGuid;
    }

    private async Task<ResolvedDocumentIdentity> ResolveDocumentIdentityAsync(
        string? documentNo,
        CARI_HESAPLAR customer,
        string customerCode,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentNo = NormalizeText(documentNo);
        if (TryResolveExplicitDocumentIdentity(normalizedDocumentNo, out var explicitIdentity))
        {
            return explicitIdentity;
        }

        var generatedDocumentSerie = BuildGeneratedDocumentSerie(
            normalizedDocumentNo,
            customer,
            customerCode,
            warehouseNo);
        var generatedDocumentOrderNo = await GetNextReceivingDocumentOrderNoAsync(
            warehouseNo,
            generatedDocumentSerie,
            cancellationToken);

        return new ResolvedDocumentIdentity(generatedDocumentSerie, generatedDocumentOrderNo);
    }

    private static bool TryResolveExplicitDocumentIdentity(
        string documentNo,
        out ResolvedDocumentIdentity identity)
    {
        identity = default!;

        if (string.IsNullOrWhiteSpace(documentNo) ||
            documentNo.Any(char.IsWhiteSpace) ||
            documentNo.Length <= DerivedDocumentOrderNoLength ||
            documentNo.Length > MaxDocumentSerieLength + DerivedDocumentOrderNoLength ||
            !int.TryParse(
                documentNo.AsSpan(documentNo.Length - DerivedDocumentOrderNoLength),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var derivedDocumentOrderNo))
        {
            return false;
        }

        var derivedDocumentSerie = documentNo[..^DerivedDocumentOrderNoLength].Trim();
        if (string.IsNullOrWhiteSpace(derivedDocumentSerie) ||
            derivedDocumentSerie.Length > MaxDocumentSerieLength)
        {
            return false;
        }

        identity = new ResolvedDocumentIdentity(derivedDocumentSerie, derivedDocumentOrderNo);
        return true;
    }

    private static void ValidateOrderLine(
        int warehouseNo,
        string customerCode,
        string stockCode,
        SIPARISLER order)
    {
        if (order.sip_iptal == true)
        {
            throw new InvalidOperationException($"Order line is cancelled: {order.sip_Guid}");
        }

        if (order.sip_kapat_fl == true)
        {
            throw new InvalidOperationException($"Order line is closed: {order.sip_Guid}");
        }

        if (order.sip_tip != IssuedCompanyOrderType || order.sip_cins != NormalOrderGenre)
        {
            throw new InvalidOperationException($"Order line is not an issued company order line: {order.sip_Guid}");
        }

        if (order.sip_depono != warehouseNo)
        {
            throw new InvalidOperationException($"Order line belongs to a different warehouse: {order.sip_Guid}");
        }

        if (!string.Equals(order.sip_musteri_kod, customerCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Order line belongs to a different customer: {order.sip_Guid}");
        }

        if (!string.Equals(order.sip_stok_kod, stockCode.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Order line stock code does not match receiving line: {order.sip_Guid}");
        }
    }

    private static PartialAcceptanceReturnInfo CreateReturnInfo(
        CreateCompanyReceivingRequest request,
        CreateCompanyReceivingLineRequest line,
        string customerCode,
        int customerAddressNo,
        string? returnDocumentSerie,
        int? returnDocumentOrderNo,
        ICollection<STOK_HAREKETLERI> returnMovements,
        string receivingDocumentSerie,
        int receivingDocumentOrderNo,
        DateTime movementDate,
        DateTime documentDate,
        DateTime now,
        int sourceLineNo,
        ref int returnRowNo,
        double returnQuantity,
        string offlineTraceKey)
    {
        if (returnQuantity <= QuantityTolerance)
        {
            return PartialAcceptanceReturnInfo.None;
        }

        if (!request.AutoCreateReturnForPartialAcceptance)
        {
            return new PartialAcceptanceReturnInfo(
                returnQuantity,
                ReturnStatusPending,
                null,
                null,
                null,
                ReturnEDespatchStatusNone);
        }

        if (string.IsNullOrWhiteSpace(returnDocumentSerie) || !returnDocumentOrderNo.HasValue)
        {
            throw new InvalidOperationException("Return document identity could not be resolved for partial company receiving.");
        }

        var returnMovement = CreatePurchaseReturnMovement(
            request,
            line,
            customerCode,
            customerAddressNo,
            returnDocumentSerie,
            returnDocumentOrderNo.Value,
            returnRowNo,
            returnQuantity,
            movementDate,
            documentDate,
            now,
            receivingDocumentSerie,
            receivingDocumentOrderNo,
            sourceLineNo,
            offlineTraceKey);
        returnMovements.Add(returnMovement);
        returnRowNo++;

        return new PartialAcceptanceReturnInfo(
            returnQuantity,
            ReturnStatusCreated,
            returnMovement.sth_Guid,
            returnDocumentSerie,
            returnDocumentOrderNo.Value,
            ReturnEDespatchStatusPending);
    }

    private static PartialAcceptanceReturnInfo ConsumeReturnInfo(ref PartialAcceptanceReturnInfo returnInfo)
    {
        var consumed = returnInfo;
        returnInfo = PartialAcceptanceReturnInfo.None;
        return consumed;
    }

    private static double ConsumePhysicalAcceptedQuantity(
        double movementQuantity,
        ref double physicalAcceptedRemaining)
    {
        if (physicalAcceptedRemaining <= QuantityTolerance)
        {
            return 0d;
        }

        var consumed = Math.Min(movementQuantity, physicalAcceptedRemaining);
        physicalAcceptedRemaining -= consumed;
        return consumed;
    }

    private static void AddMovement(
        ICollection<STOK_HAREKETLERI> movements,
        ICollection<CreateCompanyReceivingLineResultDto> results,
        CreateCompanyReceivingRequest request,
        CreateCompanyReceivingLineRequest line,
        string customerCode,
        string documentSerie,
        int documentOrderNo,
        string documentNo,
        DateTime movementDate,
        DateTime documentDate,
        DateTime now,
        int sourceLineNo,
        ref int rowNo,
        double acceptedQuantity,
        Guid? orderGuid,
        string receivingMode,
        double requestedQuantity,
        double orderLinkedQuantity,
        double orderRemainingBefore,
        double orderRemainingAfter,
        double physicalAcceptedQuantity,
        PartialAcceptanceReturnInfo returnInfo,
        string offlineTraceKey)
    {
        if (acceptedQuantity <= QuantityTolerance)
        {
            return;
        }

        var movement = CreateMovement(
            request,
            line,
            customerCode,
            documentSerie,
            documentOrderNo,
            documentNo,
            movementDate,
            documentDate,
            now,
            rowNo,
            acceptedQuantity,
            orderGuid,
            offlineTraceKey);

        movements.Add(movement);
        results.Add(new CreateCompanyReceivingLineResultDto(
            movement.sth_Guid,
            sourceLineNo,
            rowNo,
            line.StockCode.Trim(),
            orderGuid,
            orderGuid.HasValue,
            receivingMode,
            requestedQuantity,
            acceptedQuantity,
            orderLinkedQuantity,
            orderGuid.HasValue ? 0d : acceptedQuantity,
            orderRemainingBefore,
            orderRemainingAfter,
            acceptedQuantity,
            physicalAcceptedQuantity,
            returnInfo.Quantity,
            returnInfo.Status,
            returnInfo.MovementGuid,
            returnInfo.DocumentSerie,
            returnInfo.DocumentOrderNo,
            returnInfo.EDespatchStatus));

        rowNo++;
    }

    private static STOK_HAREKETLERI CreateMovement(
        CreateCompanyReceivingRequest request,
        CreateCompanyReceivingLineRequest line,
        string customerCode,
        string documentSerie,
        int documentOrderNo,
        string documentNo,
        DateTime movementDate,
        DateTime documentDate,
        DateTime now,
        int rowNo,
        double acceptedQuantity,
        Guid? orderGuid,
        string offlineTraceKey)
    {
        var unitPrice = line.UnitPrice;
        var amount = acceptedQuantity * unitPrice;

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
            sth_tip = IncomingMovementType,
            sth_cins = MovementGenre,
            sth_normal_iade = NormalMovement,
            sth_evraktip = ReceivingReceiptDocumentType,
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
            sth_cari_kodu = customerCode,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = acceptedQuantity,
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
            sth_aciklama = Truncate(NormalizeText(line.Description ?? request.Description), 50),
            sth_sip_uid = orderGuid ?? Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = request.WarehouseNo,
            sth_cikis_depo_no = request.WarehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = Truncate(NormalizeText(line.CustomerResponsibilityCenter), 25),
            sth_stok_srm_merkezi = Truncate(NormalizeText(line.ProductResponsibilityCenter), 25),
            sth_fis_tarihi = MikroEmptyDate,
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = 1,
            sth_parti_kodu = Truncate(NormalizeText(line.PartyCode), 25),
            sth_lot_no = line.LotNo,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = Truncate(NormalizeText(line.ProjectCode), 25),
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
            sth_HareketGrupKodu1 = FormatLastConsumingDate(line.LastConsumingDate),
            sth_HareketGrupKodu2 = Truncate(NormalizeText(request.Deliverer), 25),
            sth_HareketGrupKodu3 = Truncate(NormalizeText(request.Receiver), 25),
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
            sth_eticaret_kanal_kodu = offlineTraceKey,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private static STOK_HAREKETLERI CreatePurchaseReturnMovement(
        CreateCompanyReceivingRequest request,
        CreateCompanyReceivingLineRequest line,
        string customerCode,
        int customerAddressNo,
        string documentSerie,
        int documentOrderNo,
        int rowNo,
        double returnQuantity,
        DateTime movementDate,
        DateTime documentDate,
        DateTime now,
        string receivingDocumentSerie,
        int receivingDocumentOrderNo,
        int sourceLineNo,
        string offlineTraceKey)
    {
        var unitPrice = line.UnitPrice;
        var amount = returnQuantity * unitPrice;

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
            sth_tip = OutgoingMovementType,
            sth_cins = MovementGenre,
            sth_normal_iade = ReturnMovement,
            sth_evraktip = CompanyDispatchDocumentType,
            sth_evrakno_seri = documentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = string.Empty,
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
            sth_cari_kodu = customerCode,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = string.Empty,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = returnQuantity,
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
            sth_aciklama = BuildAutoReturnDescription(
                receivingDocumentSerie,
                receivingDocumentOrderNo,
                sourceLineNo),
            sth_sip_uid = Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = 0,
            sth_cikis_depo_no = request.WarehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = Truncate(NormalizeText(line.CustomerResponsibilityCenter), 25),
            sth_stok_srm_merkezi = Truncate(NormalizeText(line.ProductResponsibilityCenter), 25),
            sth_fis_tarihi = MikroEmptyDate,
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = customerAddressNo,
            sth_parti_kodu = Truncate(NormalizeText(line.PartyCode), 25),
            sth_lot_no = line.LotNo,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = Truncate(NormalizeText(line.ProjectCode), 25),
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
            sth_fiyat_liste_no = 1,
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
            sth_teslim_tarihi = movementDate,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = offlineTraceKey,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private static void ApplyOrderDelivery(SIPARISLER order, double quantity, DateTime now)
    {
        order.sip_teslim_miktar = (order.sip_teslim_miktar ?? 0d) + quantity;
        order.sip_lastup_user = MikroUserNo;
        order.sip_lastup_date = now;
        order.sip_degisti = true;
    }

    private static double CalculateRemainingQuantity(SIPARISLER order) =>
        (order.sip_miktar ?? 0d) - (order.sip_teslim_miktar ?? 0d);

    private static double ResolveDispatchQuantity(CreateCompanyReceivingLineRequest line) =>
        line.DispatchQuantity ?? line.Quantity;

    private static double ResolvePhysicalAcceptedQuantity(CreateCompanyReceivingLineRequest line) =>
        line.AcceptedQuantity ?? line.Quantity;

    private static double CalculateReturnQuantity(CreateCompanyReceivingLineRequest line)
    {
        var returnQuantity = ResolveDispatchQuantity(line) - ResolvePhysicalAcceptedQuantity(line);
        return returnQuantity > QuantityTolerance ? returnQuantity : 0d;
    }

    private static string BuildReturnDocumentSerie(int warehouseNo) => $"F{warehouseNo}";

    private static string BuildDocumentNo(string documentSerie, int documentOrderNo) =>
        string.Concat(
            documentSerie,
            documentOrderNo.ToString(
                new string('0', DerivedDocumentOrderNoLength),
                CultureInfo.InvariantCulture));

    private static string BuildGeneratedDocumentSerie(
        string requestedDocumentNo,
        CARI_HESAPLAR customer,
        string customerCode,
        int warehouseNo)
    {
        var requestedSerie = NormalizeDocumentSerieToken(requestedDocumentNo);
        var source = requestedSerie.Any(character => character is >= 'A' and <= 'Z')
            ? requestedSerie
            : NormalizeText($"{customer.cari_unvan1} {customer.cari_unvan2}");

        if (string.IsNullOrWhiteSpace(source))
        {
            source = customerCode;
        }

        var serie = NormalizeDocumentSerieToken(source);
        if (string.IsNullOrWhiteSpace(serie))
        {
            serie = $"FMK{warehouseNo}";
        }

        return Truncate(serie, MaxDocumentSerieLength);
    }

    private static string NormalizeDocumentSerieToken(string value)
    {
        var prepared = value
            .Replace('ç', 'c')
            .Replace('Ç', 'C')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'G')
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Replace('ö', 'o')
            .Replace('Ö', 'O')
            .Replace('ş', 's')
            .Replace('Ş', 'S')
            .Replace('ü', 'u')
            .Replace('Ü', 'U')
            .ToUpperInvariant()
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(prepared.Length);

        foreach (var character in prepared)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static int ResolveCustomerAddressNo(CARI_HESAPLAR customer)
    {
        var addressNo = customer.cari_sevk_adres_no ?? customer.cari_fatura_adres_no ?? 1;
        return addressNo > 0 ? addressNo : 1;
    }

    private static string BuildAutoReturnDescription(
        string receivingDocumentSerie,
        int receivingDocumentOrderNo,
        int sourceLineNo) =>
        Truncate($"AUTO IADE {receivingDocumentSerie}/{receivingDocumentOrderNo} S{sourceLineNo}", 50);

    private static void Validate(CreateCompanyReceivingRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.RequestedByUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Current user id was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            throw new ArgumentException("Customer code is required.", nameof(request.CustomerCode));
        }

        if (request.DocumentDate.HasValue &&
            request.MovementDate.HasValue &&
            request.DocumentDate.Value.Date < request.MovementDate.Value.Date)
        {
            throw new ArgumentException("Document date can not be earlier than movement date.", nameof(request.DocumentDate));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one receiving line is required.", nameof(request.Lines));
        }

        var duplicateOrderGuid = request.Lines
            .Select(line => NormalizeOrderGuid(line.OrderGuid))
            .Where(orderGuid => orderGuid.HasValue)
            .Select(orderGuid => orderGuid!.Value)
            .GroupBy(orderGuid => orderGuid)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateOrderGuid.HasValue)
        {
            throw new ArgumentException(
                $"Receiving lines can contain an order guid only once: {duplicateOrderGuid.Value}",
                nameof(request.Lines));
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.StockCode))
            {
                throw new ArgumentException("Stock code is required.", nameof(request.Lines));
            }

            var dispatchQuantity = ResolveDispatchQuantity(line);
            var physicalAcceptedQuantity = ResolvePhysicalAcceptedQuantity(line);

            if (dispatchQuantity <= QuantityTolerance)
            {
                throw new ArgumentException("Line dispatch quantity must be greater than zero.", nameof(request.Lines));
            }

            if (physicalAcceptedQuantity < -QuantityTolerance)
            {
                throw new ArgumentException("Line accepted quantity can not be negative.", nameof(request.Lines));
            }

            if (physicalAcceptedQuantity > dispatchQuantity + QuantityTolerance)
            {
                throw new ArgumentException(
                    "Line accepted quantity can not be greater than dispatch quantity.",
                    nameof(request.Lines));
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

    private static Guid? NormalizeOrderGuid(Guid? orderGuid)
    {
        if (!orderGuid.HasValue || orderGuid.Value == Guid.Empty)
        {
            return null;
        }

        return orderGuid.Value;
    }

    private static string FormatLastConsumingDate(DateTime? value) =>
        value.HasValue
            ? value.Value.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : string.Empty;

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private async Task<CreateCompanyReceivingResponse?> TryRecoverOfflineResponseAsync(
        int warehouseNo,
        Guid clientRequestId,
        string? requestPayload,
        CancellationToken cancellationToken)
    {
        var traceKey = MobileOfflineSyncService.ToTraceKey(clientRequestId);
        var movements = await mikroWriteDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement =>
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_giris_depo_no == warehouseNo &&
                movement.sth_eticaret_kanal_kodu == traceKey)
            .OrderBy(movement => movement.sth_satirno)
            .ThenBy(movement => movement.sth_Guid)
            .Select(movement => new RecoveryMovementRow(
                movement.sth_Guid,
                movement.sth_tarih,
                movement.sth_belge_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cari_kodu,
                movement.sth_giris_depo_no,
                movement.sth_satirno,
                movement.sth_stok_kod,
                movement.sth_miktar,
                movement.sth_tutar,
                movement.sth_sip_uid))
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            return null;
        }

        var headerCount = movements
            .Select(movement => new
            {
                movement.DocumentSerie,
                movement.DocumentOrderNo,
                movement.DocumentNo,
                movement.CustomerCode,
                movement.WarehouseNo
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                "More than one company receiving document matched the same clientRequestId trace.");
        }

        var request = DeserializeStoredRequest(requestPayload);
        var requestLines = request?.Lines?.ToArray() ?? Array.Empty<CreateCompanyReceivingLineRequest>();
        var orderIndexes = requestLines
            .Select((line, index) => new { line.OrderGuid, index })
            .Where(item => item.OrderGuid.HasValue && item.OrderGuid.Value != Guid.Empty)
            .ToDictionary(item => item.OrderGuid!.Value, item => item.index);
        var orderlessIndexes = requestLines
            .Select((line, index) => new { line.StockCode, line.OrderGuid, index })
            .Where(item => !item.OrderGuid.HasValue || item.OrderGuid.Value == Guid.Empty)
            .GroupBy(item => NormalizeText(item.StockCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Queue<int>(group.Select(item => item.index)),
                StringComparer.OrdinalIgnoreCase);
        var overflowIndexes = new Dictionary<string, Queue<int>>(StringComparer.OrdinalIgnoreCase);

        var orderGuids = movements
            .Select(movement => NormalizeOrderGuid(movement.OrderGuid))
            .Where(orderGuid => orderGuid.HasValue)
            .Select(orderGuid => orderGuid!.Value)
            .Distinct()
            .ToArray();
        var currentOrders = orderGuids.Length == 0
            ? new Dictionary<Guid, SIPARISLER>()
            : await mikroWriteDbContext.SIPARISLERs
                .AsNoTracking()
                .Where(order => orderGuids.Contains(order.sip_Guid))
                .ToDictionaryAsync(order => order.sip_Guid, cancellationToken);

        var lineResults = new List<CreateCompanyReceivingLineResultDto>(movements.Count);

        foreach (var movement in movements)
        {
            var normalizedOrderGuid = NormalizeOrderGuid(movement.OrderGuid);
            var stockCode = movement.StockCode ?? string.Empty;
            var acceptedQuantity = movement.Quantity ?? 0d;
            var sourceLineNo = movement.RowNo ?? lineResults.Count;
            var requestedQuantity = acceptedQuantity;
            var receivingMode = normalizedOrderGuid.HasValue ? "order-linked" : "orderless";
            var orderLinkedQuantity = normalizedOrderGuid.HasValue ? acceptedQuantity : 0d;
            var orderlessQuantity = normalizedOrderGuid.HasValue ? 0d : acceptedQuantity;
            var orderRemainingAfter = 0d;
            var orderRemainingBefore = 0d;

            if (normalizedOrderGuid.HasValue && orderIndexes.TryGetValue(normalizedOrderGuid.Value, out var matchedIndex))
            {
                sourceLineNo = matchedIndex;
                requestedQuantity = requestLines[matchedIndex].Quantity;

                if (requestLines[matchedIndex].Quantity > acceptedQuantity + QuantityTolerance)
                {
                    if (!overflowIndexes.TryGetValue(stockCode, out var queue))
                    {
                        queue = new Queue<int>();
                        overflowIndexes[stockCode] = queue;
                    }

                    queue.Enqueue(matchedIndex);
                }
            }
            else if (normalizedOrderGuid is null)
            {
                if (overflowIndexes.TryGetValue(stockCode, out var overflowQueue) && overflowQueue.Count > 0)
                {
                    sourceLineNo = overflowQueue.Dequeue();
                    requestedQuantity = requestLines[sourceLineNo].Quantity;
                    receivingMode = "order-overflow";
                }
                else if (orderlessIndexes.TryGetValue(stockCode, out var orderlessQueue) && orderlessQueue.Count > 0)
                {
                    sourceLineNo = orderlessQueue.Dequeue();
                    requestedQuantity = requestLines[sourceLineNo].Quantity;
                }
            }

            if (normalizedOrderGuid.HasValue && currentOrders.TryGetValue(normalizedOrderGuid.Value, out var currentOrder))
            {
                orderRemainingAfter = Math.Max(CalculateRemainingQuantity(currentOrder), 0d);
                orderRemainingBefore = orderRemainingAfter + acceptedQuantity;
            }

            lineResults.Add(new CreateCompanyReceivingLineResultDto(
                movement.MovementGuid,
                sourceLineNo,
                movement.RowNo ?? lineResults.Count,
                stockCode,
                normalizedOrderGuid,
                normalizedOrderGuid.HasValue,
                receivingMode,
                requestedQuantity,
                acceptedQuantity,
                orderLinkedQuantity,
                orderlessQuantity,
                orderRemainingBefore,
                orderRemainingAfter));
        }

        var firstMovement = movements[0];
        var resolvedMovementDate = firstMovement.MovementDate?.Date ?? DateTime.Today;
        var resolvedDocumentDate = firstMovement.DocumentDate?.Date ?? resolvedMovementDate;

        return new CreateCompanyReceivingResponse(
            firstMovement.DocumentSerie ?? string.Empty,
            firstMovement.DocumentOrderNo ?? 0,
            resolvedMovementDate,
            resolvedDocumentDate,
            firstMovement.DocumentNo ?? string.Empty,
            firstMovement.WarehouseNo ?? warehouseNo,
            firstMovement.CustomerCode ?? string.Empty,
            movements.Count,
            movements.Sum(movement => movement.Quantity ?? 0d),
            lineResults.Sum(line => line.OrderLinkedQuantity),
            lineResults.Sum(line => line.OrderlessQuantity),
            lineResults
                .Where(line => line.ReceivingMode == "order-overflow")
                .Sum(line => line.AcceptedQuantity),
            movements.Sum(movement => movement.Amount ?? 0d),
            mikroWriteOptions.Value.ConnectionStringName,
            lineResults);
    }

    private static CreateCompanyReceivingRequest? DeserializeStoredRequest(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonSerializer.Deserialize<CreateCompanyReceivingRequest>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private async Task TryMarkFailedAsync(
        Guid requestedByUserId,
        Guid clientRequestId,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await mobileOfflineSyncService.MarkFailedAsync(
                OfflineOperationCode,
                requestedByUserId,
                clientRequestId,
                errorMessage,
                cancellationToken);
        }
        catch
        {
            // Best effort only; preserve the original business exception.
        }
    }

    private sealed record ResolvedDocumentIdentity(string DocumentSerie, int DocumentOrderNo);

    private sealed record PartialAcceptanceReturnInfo(
        double Quantity,
        string Status,
        Guid? MovementGuid,
        string? DocumentSerie,
        int? DocumentOrderNo,
        string EDespatchStatus)
    {
        public static PartialAcceptanceReturnInfo None { get; } = new(
            0d,
            ReturnStatusNone,
            null,
            null,
            null,
            ReturnEDespatchStatusNone);
    }

    private sealed record RecoveryMovementRow(
        Guid MovementGuid,
        DateTime? MovementDate,
        DateTime? DocumentDate,
        string? DocumentNo,
        string? DocumentSerie,
        int? DocumentOrderNo,
        string? CustomerCode,
        int? WarehouseNo,
        int? RowNo,
        string? StockCode,
        double? Quantity,
        double? Amount,
        Guid? OrderGuid);

    private sealed record RecoveredCompanyReceivingCreate(
        string DocumentSerie,
        int DocumentOrderNo,
        DateTime MovementDate,
        DateTime DocumentDate,
        string DocumentNo,
        int WarehouseNo,
        string CustomerCode,
        int LineCount,
        double TotalQuantity,
        double TotalAmount,
        IReadOnlyDictionary<int, Guid> MovementGuidByRowNo)
    {
        public static RecoveredCompanyReceivingCreate Empty(
            string documentSerie,
            int documentOrderNo,
            DateTime movementDate,
            DateTime documentDate,
            string documentNo,
            int warehouseNo,
            string customerCode) =>
            new(
                documentSerie,
                documentOrderNo,
                movementDate,
                documentDate,
                documentNo,
                warehouseNo,
                customerCode,
                0,
                0d,
                0d,
                new Dictionary<int, Guid>());
    }
}
