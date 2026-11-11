using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.BelgeAkisTakibi;

public sealed class DocumentFlowService(
    AuthDbContext authDbContext,
    IClock clock,
    IOptionsMonitor<DocumentFlowTrackingOptions> trackingOptions,
    ILogger<DocumentFlowService> logger)
    : IDocumentFlowService
{
    public async Task<DocumentFlowListResponse> ListAsync(
        DocumentFlowListRequest request,
        CancellationToken cancellationToken)
    {
        var skip = Math.Max(request.Skip, 0);
        var take = Math.Clamp(request.Take, 1, 500);
        var baseQuery = authDbContext.DocumentFlows.AsNoTracking();
        DateTime? startUtc = request.StartDate.HasValue
            ? ToLocalDayStartUtc(request.StartDate.Value)
            : null;
        DateTime? endExclusiveUtc = request.EndDate.HasValue
            ? ToLocalDayStartUtc(request.EndDate.Value.Date.AddDays(1))
            : null;

        if (request.Category != DocumentFlowCategory.All)
        {
            baseQuery = ApplyCategoryFilter(baseQuery, request.Category);
        }

        if (request.DocumentType.HasValue)
        {
            baseQuery = baseQuery.Where(flow => flow.DocumentType == request.DocumentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            baseQuery = baseQuery.Where(flow =>
                flow.DocumentSerie.Contains(search) ||
                (flow.DocumentNo != null && flow.DocumentNo.Contains(search)) ||
                (flow.ExternalDocumentNo != null && flow.ExternalDocumentNo.Contains(search)) ||
                (flow.ExternalUuid != null && flow.ExternalUuid.Contains(search)));
        }

        var summary = await BuildSummaryAsync(
            baseQuery,
            request.WarehouseNo,
            startUtc,
            endExclusiveUtc,
            cancellationToken);

        // "Tum akislar" ve "gunluk kabul" listeleri son hareket (guncelleme) tarihine,
        // bekleyen/eksik islem listeleri ise belgenin olusturulma tarihine gore filtrelenir.
        // Ozet sayaclari da ayni kurallarla hesaplandigi icin kart sayisi ile liste birebir eslesir.
        var useUpdatedDate = request.Preset is DocumentFlowPreset.All or DocumentFlowPreset.TodayReceivings;
        var query = ApplyDateFilter(baseQuery, startUtc, endExclusiveUtc, useUpdatedDate);

        query = ApplyWarehouseAndDirectionFilter(
            query,
            request.WarehouseNo,
            request.Direction,
            request.Preset);

        if (request.Preset != DocumentFlowPreset.All)
        {
            query = ApplyPresetFilter(query, request.Preset, request.WarehouseNo);
        }

        if (request.CurrentStep.HasValue)
        {
            query = query.Where(flow => flow.CurrentStep == request.CurrentStep.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(flow => flow.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var flows = await query
            .OrderByDescending(flow => flow.UpdatedAtUtc)
            .ThenByDescending(flow => flow.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new DocumentFlowListResponse(
            trackingOptions.CurrentValue.Enabled,
            totalCount,
            flows.Select(ToListItem).ToArray(),
            summary);
    }

    public async Task<DocumentFlowDetailDto> GetAsync(
        Guid id,
        int? allowedWarehouseNo,
        CancellationToken cancellationToken)
    {
        var query = authDbContext.DocumentFlows
            .AsNoTracking()
            .Include(flow => flow.Events)
            .Where(flow => flow.Id == id);

        if (allowedWarehouseNo.HasValue)
        {
            var warehouseNo = allowedWarehouseNo.Value;
            query = query.Where(flow =>
                flow.SourceWarehouseNo == warehouseNo ||
                flow.TargetWarehouseNo == warehouseNo);
        }

        var flow = await query.SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Document flow was not found.");

        return new DocumentFlowDetailDto(
            flow.Id,
            flow.FlowKey,
            flow.DocumentType.ToString(),
            flow.SourceWarehouseNo,
            flow.TargetWarehouseNo,
            flow.DocumentSerie,
            flow.DocumentOrderNo,
            flow.DocumentNo,
            flow.ExternalDocumentNo,
            flow.ExternalUuid,
            flow.Status.ToString(),
            flow.CurrentStep.ToString(),
            flow.LastError,
            flow.LastChangedByUserId,
            flow.CreatedAtUtc,
            flow.UpdatedAtUtc,
            flow.Events
                .OrderBy(flowEvent => flowEvent.OccurredAtUtc)
                .Select(flowEvent => new DocumentFlowEventDto(
                    flowEvent.Id,
                    flowEvent.Step.ToString(),
                    flowEvent.Status.ToString(),
                    flowEvent.Message,
                    flowEvent.Error,
                    flowEvent.ChangedByUserId,
                    flowEvent.OccurredAtUtc))
                .ToArray());
    }

    public async Task RecordAsync(
        RecordDocumentFlowRequest request,
        CancellationToken cancellationToken)
    {
        if (!trackingOptions.CurrentValue.Enabled)
        {
            return;
        }

        try
        {
            await RecordCoreAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Document flow tracking failed. FlowKey={FlowKey}, Step={Step}, Status={Status}",
                request.FlowKey,
                request.Step,
                request.Status);
        }
    }

    public async Task RecordManyAsync(
        IReadOnlyCollection<RecordDocumentFlowRequest> requests,
        CancellationToken cancellationToken)
    {
        if (!trackingOptions.CurrentValue.Enabled || requests.Count == 0)
        {
            return;
        }

        try
        {
            var flowKeys = requests.Select(item => item.FlowKey).Distinct().ToArray();
            var existingFlows = await authDbContext.DocumentFlows
                .Include(item => item.Events)
                .Where(item => flowKeys.Contains(item.FlowKey))
                .ToDictionaryAsync(item => item.FlowKey, cancellationToken);

            foreach (var request in requests)
            {
                if (!existingFlows.TryGetValue(request.FlowKey, out var flow))
                {
                    flow = new DocumentFlow(
                        Guid.NewGuid(),
                        request.FlowKey,
                        request.DocumentType,
                        request.SourceWarehouseNo,
                        request.TargetWarehouseNo,
                        request.DocumentSerie,
                        request.DocumentOrderNo,
                        clock.UtcNow);
                    authDbContext.DocumentFlows.Add(flow);
                    existingFlows.Add(request.FlowKey, flow);
                }
                else if (ShouldSkipSuccessfulEvent(flow, request))
                {
                    continue;
                }

                var flowEvent = flow.Record(
                    request.Step,
                    request.Status,
                    request.Message,
                    request.Error,
                    request.ChangedByUserId,
                    clock.UtcNow,
                    request.DocumentNo,
                    request.ExternalDocumentNo,
                    request.ExternalUuid,
                    request.TargetWarehouseNo);
                authDbContext.DocumentFlowEvents.Add(flowEvent);
            }

            await authDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Batch document flow tracking failed. RequestCount={RequestCount}",
                requests.Count);
        }
    }

    private static IQueryable<DocumentFlow> ApplyWarehouseAndDirectionFilter(
        IQueryable<DocumentFlow> query,
        int? warehouseNo,
        DocumentFlowDirection direction,
        DocumentFlowPreset preset)
    {
        if (!warehouseNo.HasValue)
        {
            return direction switch
            {
                DocumentFlowDirection.Incoming => query.Where(flow => flow.TargetWarehouseNo.HasValue),
                _ => query
            };
        }

        var warehouse = warehouseNo.Value;
        var effectiveDirection = direction;

        if (effectiveDirection == DocumentFlowDirection.All)
        {
            effectiveDirection = preset switch
            {
                DocumentFlowPreset.PendingReceiving or DocumentFlowPreset.TodayReceivings =>
                    DocumentFlowDirection.Incoming,
                DocumentFlowPreset.UnacceptedOutgoing
                    or DocumentFlowPreset.PendingEDespatch
                    or DocumentFlowPreset.FailedEDespatch
                    or DocumentFlowPreset.TodayShipments
                    or DocumentFlowPreset.TodayReturns =>
                    DocumentFlowDirection.Outgoing,
                _ => DocumentFlowDirection.All
            };
        }

        return effectiveDirection switch
        {
            DocumentFlowDirection.Incoming => query.Where(flow => flow.TargetWarehouseNo == warehouse),
            DocumentFlowDirection.Outgoing => query.Where(flow => flow.SourceWarehouseNo == warehouse),
            _ => query.Where(flow =>
                flow.SourceWarehouseNo == warehouse ||
                flow.TargetWarehouseNo == warehouse)
        };
    }

    private static IQueryable<DocumentFlow> ApplyCategoryFilter(
        IQueryable<DocumentFlow> query,
        DocumentFlowCategory category) =>
        category switch
        {
            DocumentFlowCategory.ShipmentsAndReceiving => query.Where(flow =>
                flow.DocumentType == DocumentFlowType.CompanyShipment ||
                flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                flow.DocumentType == DocumentFlowType.CompanyReceiving),
            DocumentFlowCategory.Returns => query.Where(flow =>
                flow.DocumentType == DocumentFlowType.CompanyReturn ||
                flow.DocumentType == DocumentFlowType.WarehouseReturn),
            DocumentFlowCategory.Orders => query.Where(flow =>
                flow.DocumentType == DocumentFlowType.IssuedCompanyOrder ||
                flow.DocumentType == DocumentFlowType.ReceivedCompanyOrder ||
                flow.DocumentType == DocumentFlowType.IssuedWarehouseOrder ||
                flow.DocumentType == DocumentFlowType.ReceivedWarehouseOrder),
            DocumentFlowCategory.CashAndStore => query.Where(flow =>
                flow.DocumentType == DocumentFlowType.CashSummary ||
                flow.DocumentType == DocumentFlowType.OutageReceipt ||
                flow.DocumentType == DocumentFlowType.ExpenseReceipt ||
                flow.DocumentType == DocumentFlowType.StockMovementDocument ||
                flow.DocumentType == DocumentFlowType.CustomerMovementDocument),
            DocumentFlowCategory.StockAndCounting => query.Where(flow =>
                flow.DocumentType == DocumentFlowType.InventoryCount ||
                flow.DocumentType == DocumentFlowType.Virman ||
                flow.DocumentType == DocumentFlowType.StockCard ||
                flow.DocumentType == DocumentFlowType.WarehouseCard ||
                flow.DocumentType == DocumentFlowType.CustomerCard ||
                flow.DocumentType == DocumentFlowType.StockSalesPrice),
            _ => query
        };

    private static IQueryable<DocumentFlow> ApplyPresetFilter(
        IQueryable<DocumentFlow> query,
        DocumentFlowPreset preset,
        int? warehouseNo) =>
        preset switch
        {
            DocumentFlowPreset.PendingReceiving or DocumentFlowPreset.UnacceptedOutgoing =>
                query.Where(flow =>
                    flow.TargetWarehouseNo.HasValue &&
                    (flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                     flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                    flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                    flow.Status == DocumentFlowStatus.Succeeded),

            DocumentFlowPreset.PendingEDespatch =>
                query.Where(flow =>
                    flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                    flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted &&
                    ((flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                      flow.Status == DocumentFlowStatus.Failed) ||
                     ((flow.DocumentType == DocumentFlowType.CompanyShipment ||
                       flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                       flow.DocumentType == DocumentFlowType.CompanyReturn ||
                       flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                      flow.Status == DocumentFlowStatus.Succeeded &&
                      flow.ExternalUuid == null &&
                      (flow.CurrentStep == DocumentFlowStep.DocumentCreated ||
                       flow.CurrentStep == DocumentFlowStep.AxataDocumentImported ||
                       flow.CurrentStep == DocumentFlowStep.DocumentUpdated)))),

            DocumentFlowPreset.FailedEDespatch =>
                query.Where(flow =>
                    flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                    flow.Status == DocumentFlowStatus.Failed),

            DocumentFlowPreset.OpenOrders =>
                query.Where(flow =>
                    (flow.DocumentType == DocumentFlowType.IssuedCompanyOrder ||
                     flow.DocumentType == DocumentFlowType.ReceivedCompanyOrder ||
                     flow.DocumentType == DocumentFlowType.IssuedWarehouseOrder ||
                     flow.DocumentType == DocumentFlowType.ReceivedWarehouseOrder) &&
                    flow.CurrentStep == DocumentFlowStep.OrderCreated &&
                    flow.Status == DocumentFlowStatus.Succeeded),

            DocumentFlowPreset.FailedOnly =>
                query.Where(flow =>
                    flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                    flow.Status == DocumentFlowStatus.Failed),

            DocumentFlowPreset.OtherFailedOperations =>
                query.Where(flow =>
                    flow.CurrentStep != DocumentFlowStep.EDespatchSubmission &&
                    flow.Status == DocumentFlowStatus.Failed),

            DocumentFlowPreset.TodayShipments =>
                query.Where(flow =>
                    (flow.DocumentType == DocumentFlowType.CompanyShipment ||
                     flow.DocumentType == DocumentFlowType.InterWarehouseShipment) &&
                    flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                    !(flow.CurrentStep == DocumentFlowStep.DocumentCreated &&
                      flow.Status == DocumentFlowStatus.Failed)),

            DocumentFlowPreset.TodayReturns =>
                query.Where(flow =>
                    (flow.DocumentType == DocumentFlowType.CompanyReturn ||
                     flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                    flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                    !(flow.CurrentStep == DocumentFlowStep.DocumentCreated &&
                      flow.Status == DocumentFlowStatus.Failed)),

            DocumentFlowPreset.TodayReceivings =>
                query.Where(flow =>
                    flow.TargetWarehouseNo.HasValue &&
                    flow.CurrentStep == DocumentFlowStep.WarehouseReceivingAccepted),

            DocumentFlowPreset.ActionRequired =>
                warehouseNo.HasValue
                    ? query.Where(flow =>
                        flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                        ((flow.TargetWarehouseNo == warehouseNo.Value &&
                          (flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                           flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                          flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                          flow.Status == DocumentFlowStatus.Succeeded) ||
                         (flow.SourceWarehouseNo == warehouseNo.Value &&
                          flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted &&
                          ((flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                            flow.Status == DocumentFlowStatus.Failed) ||
                           ((flow.DocumentType == DocumentFlowType.CompanyShipment ||
                             flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                             flow.DocumentType == DocumentFlowType.CompanyReturn ||
                             flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                            flow.Status == DocumentFlowStatus.Succeeded &&
                            flow.ExternalUuid == null &&
                            (flow.CurrentStep == DocumentFlowStep.DocumentCreated ||
                             flow.CurrentStep == DocumentFlowStep.AxataDocumentImported ||
                              flow.CurrentStep == DocumentFlowStep.DocumentUpdated)))) ||
                         ((flow.SourceWarehouseNo == warehouseNo.Value ||
                           flow.TargetWarehouseNo == warehouseNo.Value) &&
                          (flow.DocumentType == DocumentFlowType.IssuedCompanyOrder ||
                           flow.DocumentType == DocumentFlowType.ReceivedCompanyOrder ||
                           flow.DocumentType == DocumentFlowType.IssuedWarehouseOrder ||
                           flow.DocumentType == DocumentFlowType.ReceivedWarehouseOrder) &&
                          flow.CurrentStep == DocumentFlowStep.OrderCreated &&
                          flow.Status == DocumentFlowStatus.Succeeded) ||
                         flow.Status == DocumentFlowStatus.Failed))
                    : query.Where(flow =>
                        flow.CurrentStep != DocumentFlowStep.DocumentDeleted &&
                        ((flow.TargetWarehouseNo.HasValue &&
                          (flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                           flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                          flow.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
                          flow.Status == DocumentFlowStatus.Succeeded) ||
                         (flow.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted &&
                          (flow.DocumentType == DocumentFlowType.CompanyShipment ||
                           flow.DocumentType == DocumentFlowType.InterWarehouseShipment ||
                           flow.DocumentType == DocumentFlowType.CompanyReturn ||
                           flow.DocumentType == DocumentFlowType.WarehouseReturn) &&
                          flow.Status == DocumentFlowStatus.Succeeded &&
                          flow.ExternalUuid == null &&
                          (flow.CurrentStep == DocumentFlowStep.DocumentCreated ||
                           flow.CurrentStep == DocumentFlowStep.AxataDocumentImported ||
                            flow.CurrentStep == DocumentFlowStep.DocumentUpdated)) ||
                         ((flow.DocumentType == DocumentFlowType.IssuedCompanyOrder ||
                           flow.DocumentType == DocumentFlowType.ReceivedCompanyOrder ||
                           flow.DocumentType == DocumentFlowType.IssuedWarehouseOrder ||
                           flow.DocumentType == DocumentFlowType.ReceivedWarehouseOrder) &&
                          flow.CurrentStep == DocumentFlowStep.OrderCreated &&
                          flow.Status == DocumentFlowStatus.Succeeded) ||
                         flow.Status == DocumentFlowStatus.Failed)),

            _ => query
        };

    private static IQueryable<DocumentFlow> ApplyDateFilter(
        IQueryable<DocumentFlow> query,
        DateTime? startUtc,
        DateTime? endExclusiveUtc,
        bool useUpdatedDate)
    {
        if (useUpdatedDate)
        {
            if (startUtc.HasValue)
            {
                var start = startUtc.Value;
                query = query.Where(flow => flow.UpdatedAtUtc >= start);
            }

            if (endExclusiveUtc.HasValue)
            {
                var end = endExclusiveUtc.Value;
                query = query.Where(flow => flow.UpdatedAtUtc < end);
            }

            return query;
        }

        if (startUtc.HasValue)
        {
            var start = startUtc.Value;
            query = query.Where(flow => flow.CreatedAtUtc >= start);
        }

        if (endExclusiveUtc.HasValue)
        {
            var end = endExclusiveUtc.Value;
            query = query.Where(flow => flow.CreatedAtUtc < end);
        }

        return query;
    }

    private static async Task<DocumentFlowSummaryDto> BuildSummaryAsync(
        IQueryable<DocumentFlow> baseQuery,
        int? warehouseNo,
        DateTime? startUtc,
        DateTime? endExclusiveUtc,
        CancellationToken cancellationToken)
    {
        var scopedQuery = warehouseNo.HasValue
            ? baseQuery.Where(flow =>
                flow.SourceWarehouseNo == warehouseNo.Value ||
                flow.TargetWarehouseNo == warehouseNo.Value)
            : baseQuery;

        // Hem guncelleme hem olusturma tarihine gore aralikta kalan kayitlari tek seferde okur;
        // her sayac kendi listesinin tarih kuralini bellekte uygular.
        if (startUtc.HasValue)
        {
            var start = startUtc.Value;
            scopedQuery = scopedQuery.Where(flow => flow.UpdatedAtUtc >= start || flow.CreatedAtUtc >= start);
        }

        if (endExclusiveUtc.HasValue)
        {
            var end = endExclusiveUtc.Value;
            scopedQuery = scopedQuery.Where(flow => flow.UpdatedAtUtc < end || flow.CreatedAtUtc < end);
        }

        var candidateRows = await scopedQuery
            .Select(flow => new SummaryRow(
                flow.DocumentType,
                flow.SourceWarehouseNo,
                flow.TargetWarehouseNo,
                flow.Status,
                flow.CurrentStep,
                flow.ExternalUuid,
                flow.CreatedAtUtc,
                flow.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        bool InRange(DateTime value) =>
            (!startUtc.HasValue || value >= startUtc.Value) &&
            (!endExclusiveUtc.HasValue || value < endExclusiveUtc.Value);

        var updatedRows = candidateRows.Where(row => InRange(row.UpdatedAtUtc)).ToArray();
        var rows = candidateRows.Where(row => InRange(row.CreatedAtUtc)).ToArray();

        var totalCount = updatedRows.Length;
        var succeededCount = updatedRows.Count(row => row.Status == DocumentFlowStatus.Succeeded);
        var failedCount = rows.Count(row =>
            row.CurrentStep != DocumentFlowStep.DocumentDeleted &&
            row.Status == DocumentFlowStatus.Failed);

        var pendingReceivingCount = rows.Count(row =>
            (!warehouseNo.HasValue || row.TargetWarehouseNo == warehouseNo.Value) &&
            IsPendingReceiving(row));

        var unacceptedOutgoingCount = rows.Count(row =>
            (!warehouseNo.HasValue || row.SourceWarehouseNo == warehouseNo.Value) &&
            IsPendingReceiving(row));

        var pendingEDespatchCount = rows.Count(row =>
            (!warehouseNo.HasValue || row.SourceWarehouseNo == warehouseNo.Value) &&
            IsPendingOrFailedEDespatch(row));

        var failedEDespatchCount = rows.Count(row =>
            (!warehouseNo.HasValue || row.SourceWarehouseNo == warehouseNo.Value) &&
            row.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
            row.Status == DocumentFlowStatus.Failed);

        var openOrderCount = rows.Count(row =>
            IsOrder(row.DocumentType) &&
            row.CurrentStep == DocumentFlowStep.OrderCreated &&
            row.Status == DocumentFlowStatus.Succeeded);

        var actionRequiredCount = rows.Count(row =>
            IsActionRequired(row, warehouseNo));

        return new DocumentFlowSummaryDto(
            totalCount,
            succeededCount,
            failedCount,
            actionRequiredCount,
            pendingReceivingCount,
            unacceptedOutgoingCount,
            pendingEDespatchCount,
            failedEDespatchCount,
            openOrderCount);
    }

    private static bool IsPendingReceiving(SummaryRow row) =>
        row.TargetWarehouseNo.HasValue &&
        (row.DocumentType is DocumentFlowType.InterWarehouseShipment or DocumentFlowType.WarehouseReturn) &&
        row.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
        row.Status == DocumentFlowStatus.Succeeded;

    private static bool IsDespatchDocument(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.CompanyShipment
            or DocumentFlowType.InterWarehouseShipment
            or DocumentFlowType.CompanyReturn
            or DocumentFlowType.WarehouseReturn;

    private static bool IsOrder(DocumentFlowType documentType) =>
        documentType is DocumentFlowType.IssuedCompanyOrder
            or DocumentFlowType.ReceivedCompanyOrder
            or DocumentFlowType.IssuedWarehouseOrder
            or DocumentFlowType.ReceivedWarehouseOrder;

    private static bool IsPendingOrFailedEDespatch(SummaryRow row) =>
        row.CurrentStep != DocumentFlowStep.DocumentDeleted &&
        row.CurrentStep != DocumentFlowStep.WarehouseReceivingAccepted &&
        ((row.CurrentStep == DocumentFlowStep.EDespatchSubmission &&
          row.Status == DocumentFlowStatus.Failed) ||
         (IsDespatchDocument(row.DocumentType) &&
          row.Status == DocumentFlowStatus.Succeeded &&
          row.ExternalUuid is null &&
          row.CurrentStep is DocumentFlowStep.DocumentCreated
              or DocumentFlowStep.AxataDocumentImported
              or DocumentFlowStep.DocumentUpdated));

    private static bool IsActionRequired(SummaryRow row, int? warehouseNo)
    {
        if (row.CurrentStep == DocumentFlowStep.DocumentDeleted)
        {
            return false;
        }

        if (warehouseNo.HasValue)
        {
            var warehouse = warehouseNo.Value;
            return (row.TargetWarehouseNo == warehouse && IsPendingReceiving(row))
                || (row.SourceWarehouseNo == warehouse && IsPendingOrFailedEDespatch(row))
                || ((row.SourceWarehouseNo == warehouse || row.TargetWarehouseNo == warehouse) &&
                    IsOpenOrder(row))
                || row.Status == DocumentFlowStatus.Failed;
        }

        return IsPendingReceiving(row)
            || IsPendingOrFailedEDespatch(row)
            || IsOpenOrder(row)
            || row.Status == DocumentFlowStatus.Failed;
    }

    private static bool IsOpenOrder(SummaryRow row) =>
        IsOrder(row.DocumentType) &&
        row.CurrentStep == DocumentFlowStep.OrderCreated &&
        row.Status == DocumentFlowStatus.Succeeded;

    private async Task RecordCoreAsync(
        RecordDocumentFlowRequest request,
        CancellationToken cancellationToken)
    {
        var flow = await authDbContext.DocumentFlows
            .SingleOrDefaultAsync(item => item.FlowKey == request.FlowKey, cancellationToken);

        if (flow is null)
        {
            flow = new DocumentFlow(
                Guid.NewGuid(),
                request.FlowKey,
                request.DocumentType,
                request.SourceWarehouseNo,
                request.TargetWarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                clock.UtcNow);
            authDbContext.DocumentFlows.Add(flow);
        }
        else if (flow.CurrentStep != DocumentFlowStep.DocumentDeleted
                 && IsIdempotentSuccessfulStep(request)
                 && await authDbContext.DocumentFlowEvents.AnyAsync(
                     item => item.DocumentFlowId == flow.Id
                         && item.Step == request.Step
                         && item.Status == DocumentFlowStatus.Succeeded
                         && (request.Step != DocumentFlowStep.AxataOrderSent
                             || item.Message == request.Message),
                     cancellationToken))
        {
            return;
        }

        var flowEvent = flow.Record(
            request.Step,
            request.Status,
            request.Message,
            request.Error,
            request.ChangedByUserId,
            clock.UtcNow,
            request.DocumentNo,
            request.ExternalDocumentNo,
            request.ExternalUuid,
            request.TargetWarehouseNo);
        authDbContext.DocumentFlowEvents.Add(flowEvent);

        await authDbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool ShouldSkipSuccessfulEvent(
        DocumentFlow flow,
        RecordDocumentFlowRequest request) =>
        flow.CurrentStep != DocumentFlowStep.DocumentDeleted
        && IsIdempotentSuccessfulStep(request)
        && flow.Events.Any(item =>
            item.Step == request.Step
            && item.Status == DocumentFlowStatus.Succeeded
            && (request.Step != DocumentFlowStep.AxataOrderSent
                || item.Message == request.Message));

    private static bool IsIdempotentSuccessfulStep(RecordDocumentFlowRequest request) =>
        request.Status == DocumentFlowStatus.Succeeded
        && request.Step is DocumentFlowStep.DocumentCreated
            or DocumentFlowStep.OrderCreated
            or DocumentFlowStep.WarehouseReceivingAccepted
            or DocumentFlowStep.AxataOrderSent
            or DocumentFlowStep.AxataDocumentImported;

    private static DocumentFlowListItemDto ToListItem(DocumentFlow flow) =>
        new(
            flow.Id,
            flow.DocumentType.ToString(),
            flow.SourceWarehouseNo,
            flow.TargetWarehouseNo,
            flow.DocumentSerie,
            flow.DocumentOrderNo,
            flow.DocumentNo,
            flow.ExternalDocumentNo,
            flow.ExternalUuid,
            flow.Status.ToString(),
            flow.CurrentStep.ToString(),
            flow.LastError,
            flow.CreatedAtUtc,
            flow.UpdatedAtUtc);

    private static DateTime ToLocalDayStartUtc(DateTime value)
    {
        var dateOnly = DateOnly.FromDateTime(value.Date);
        return dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
    }

    private sealed record SummaryRow(
        DocumentFlowType DocumentType,
        int SourceWarehouseNo,
        int? TargetWarehouseNo,
        DocumentFlowStatus Status,
        DocumentFlowStep CurrentStep,
        string? ExternalUuid,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}
