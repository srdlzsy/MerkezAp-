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
        var take = Math.Clamp(request.Take, 1, 500);
        var query = authDbContext.DocumentFlows.AsNoTracking();

        if (request.WarehouseNo.HasValue)
        {
            var warehouseNo = request.WarehouseNo.Value;
            query = query.Where(flow =>
                flow.SourceWarehouseNo == warehouseNo ||
                flow.TargetWarehouseNo == warehouseNo);
        }

        if (request.StartDate.HasValue)
        {
            var startDate = ToUtc(request.StartDate.Value.Date);
            query = query.Where(flow => flow.UpdatedAtUtc >= startDate);
        }

        if (request.EndDate.HasValue)
        {
            var endDateExclusive = ToUtc(request.EndDate.Value.Date.AddDays(1));
            query = query.Where(flow => flow.UpdatedAtUtc < endDateExclusive);
        }

        if (request.DocumentType.HasValue)
        {
            query = query.Where(flow => flow.DocumentType == request.DocumentType.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(flow => flow.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(flow =>
                flow.DocumentSerie.Contains(search) ||
                (flow.DocumentNo != null && flow.DocumentNo.Contains(search)) ||
                (flow.ExternalDocumentNo != null && flow.ExternalDocumentNo.Contains(search)) ||
                (flow.ExternalUuid != null && flow.ExternalUuid.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var flows = await query
            .OrderByDescending(flow => flow.UpdatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new DocumentFlowListResponse(
            trackingOptions.CurrentValue.Enabled,
            totalCount,
            flows.Select(ToListItem).ToArray());
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

    private async Task RecordCoreAsync(
        RecordDocumentFlowRequest request,
        CancellationToken cancellationToken)
    {
        var flow = await authDbContext.DocumentFlows
            .Include(item => item.Events)
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

        flow.Record(
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

        await authDbContext.SaveChangesAsync(cancellationToken);
    }

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

    private static DateTime ToUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
