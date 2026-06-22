using System.Globalization;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Persistence.Axata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationService(
    AxataSynchronizationQueue queue,
    AxataSynchronizationExecutionCoordinator coordinator,
    AxataSynchronizationManualDocumentService manualDocumentService,
    AxataSynchronizationConnectionProbeService probeService,
    AxataDbContext axataDbContext,
    IConfiguration configuration,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : IAxataSynchronizationService
{
    public Task<AxataSynchronizationOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        var tasks = AxataSynchronizationCatalog.Definitions
            .Select(definition =>
            {
                var taskOptions = ResolveTaskOptions(currentOptions, definition.Code);

                return new AxataSynchronizationTaskDto(
                    definition.Code,
                    definition.Name,
                    definition.Description,
                    definition.Flow,
                    definition.RequiresWarehouseNo,
                    taskOptions.Enabled,
                    taskOptions.ScheduleEnabled,
                    Math.Max(1, taskOptions.IntervalMinutes),
                    taskOptions.DefaultWarehouseNo,
                    definition.SourceSystem,
                    definition.TargetSystem,
                    SupportsManualDocuments(definition.Code),
                    SupportsLiveDispatch(definition.Code),
                    ResolveLiveOperationName(definition.Code, currentOptions));
            })
            .ToArray();

        var response = new AxataSynchronizationOverviewDto(
            currentOptions.Enabled,
            currentOptions.WorkerEnabled,
            currentOptions.SchedulerEnabled,
            configuration["MikroDatabase:Profile"] ?? "Split",
            currentOptions.MainEndpointUrl,
            currentOptions.ExtendedEndpointUrl,
            tasks,
            queue.ListRecent(20));

        return Task.FromResult(response);
    }

    public Task<AxataSynchronizationFetchProfilesOverviewDto> GetFetchProfilesAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var currentOptions = options.CurrentValue;
        var profiles = AxataSynchronizationFetchProfileCatalog.Definitions
            .Select(definition => new AxataSynchronizationFetchProfileDto(
                definition.Code,
                definition.Name,
                definition.SourceSystem,
                definition.TargetSystem,
                ToDisplayName(definition.SourceEndpointKind),
                ResolveEndpointUrl(currentOptions, definition.SourceEndpointKind),
                definition.FetchOperation,
                ToDisplayName(definition.AckEndpointKind),
                ResolveEndpointUrl(currentOptions, definition.AckEndpointKind),
                definition.AckOperation,
                definition.CompanyCode,
                definition.WarehouseCode,
                definition.MovementType,
                definition.PendingStatus,
                definition.CurrentHandling,
                definition.CurrentRoute,
                definition.IsImplemented))
            .ToArray();

        return Task.FromResult(new AxataSynchronizationFetchProfilesOverviewDto(
            DateTime.UtcNow,
            profiles,
            [
                "Bu katalog eski worker akislarindaki AXATA fetch/import profillerini worker-ready sekilde toplar.",
                "IsImplemented = false olan profiller planli parity alanidir; route veya body-based fallback varsa CurrentRoute alaninda gosterilir.",
                "Bugunku modulde canli fetch adapter'i yerine belirli akislarda manual import ve manual accept endpoint'leri kullanilir."
            ]));
    }

    public Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationPreviewRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var take = NormalizeTake(request.Take, options.CurrentValue.PreviewDefaultTake);

        return coordinator.PreviewAsync(definition.Code, warehouseNo, take, cancellationToken);
    }

    public Task<AxataSynchronizationJobDto> QueueAsync(
        AxataSynchronizationExecuteRequest request,
        Guid requestedByUserId,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var currentOptions = options.CurrentValue;

        if (!currentOptions.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled in configuration.");
        }

        if (!currentOptions.WorkerEnabled)
        {
            throw new InvalidOperationException("AXATA synchronization worker is disabled in configuration.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Current user id was not found.");
        }

        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var executionMode = AxataSynchronizationJobMappings.ParseExecutionMode(request.ExecutionMode);

        return Task.FromResult(queue.Enqueue(
            definition,
            executionMode,
            AxataSynchronizationJobTriggerSource.Manual,
            warehouseNo,
            requestedByUserId));
    }

    public Task<AxataSynchronizationJobDetailDto> GetJobAsync(Guid jobId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.FromResult(queue.Get(jobId));
    }

    public Task<AxataSynchronizationConnectionTestDto> TestConnectionsAsync(CancellationToken cancellationToken) =>
        probeService.ProbeAsync(cancellationToken);

    public Task<AxataSynchronizationManualDocumentCandidatesDto> ListDocumentCandidatesAsync(
        AxataSynchronizationManualDocumentCandidatesRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var skip = NormalizeSkip(request.Skip);
        var take = NormalizeTake(request.Take, options.CurrentValue.PreviewDefaultTake);
        var dateRange = ResolveDateRange(
            request.StartDate,
            request.EndDate,
            options.CurrentValue.DefaultLookbackDays);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            AxataSynchronizationJobExecutionMode.DryRun,
            warehouseNo,
            Guid.Empty,
            DateTime.UtcNow);

        return manualDocumentService.ListCandidatesAsync(
            context,
            new AxataSynchronizationManualDocumentCandidateCriteria(
                dateRange.StartDate,
                dateRange.EndDate,
                skip,
                take),
            cancellationToken);
    }

    public Task<AxataSynchronizationManualDocumentDto> PreviewDocumentAsync(
        AxataSynchronizationManualDocumentRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            AxataSynchronizationJobExecutionMode.DryRun,
            warehouseNo,
            Guid.Empty,
            DateTime.UtcNow);

        return manualDocumentService.PreviewAsync(
            context,
            new AxataSynchronizationManualDocumentInput(
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentNo,
                request.DocumentDate),
            cancellationToken);
    }

    public Task<AxataSynchronizationManualDocumentDto> ExecuteDocumentAsync(
        AxataSynchronizationManualDocumentExecuteRequest request,
        Guid requestedByUserId,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;

        if (!currentOptions.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled in configuration.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Current user id was not found.");
        }

        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var executionMode = AxataSynchronizationJobMappings.ParseExecutionMode(request.ExecutionMode);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            executionMode,
            warehouseNo,
            requestedByUserId,
            DateTime.UtcNow);

        return manualDocumentService.ExecuteAsync(
            context,
            new AxataSynchronizationManualDocumentInput(
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentNo,
                request.DocumentDate),
            cancellationToken);
    }

    public Task<AxataSynchronizationManualDocumentBatchDto> PreviewDocumentsAsync(
        AxataSynchronizationManualDocumentBatchRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            AxataSynchronizationJobExecutionMode.DryRun,
            warehouseNo,
            Guid.Empty,
            DateTime.UtcNow);

        return manualDocumentService.PreviewBatchAsync(
            context,
            MapBatchInputs(request.Documents),
            request.ContinueOnError,
            cancellationToken);
    }

    public Task<AxataSynchronizationManualDocumentBatchDto> ExecuteDocumentsAsync(
        AxataSynchronizationManualDocumentBatchExecuteRequest request,
        Guid requestedByUserId,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;

        if (!currentOptions.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled in configuration.");
        }

        if (requestedByUserId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Current user id was not found.");
        }

        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var executionMode = AxataSynchronizationJobMappings.ParseExecutionMode(request.ExecutionMode);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            executionMode,
            warehouseNo,
            requestedByUserId,
            DateTime.UtcNow);

        return manualDocumentService.ExecuteBatchAsync(
            context,
            MapBatchInputs(request.Documents),
            request.ContinueOnError,
            cancellationToken);
    }

    public Task<AxataSynchronizationManualDispatchDto> DispatchDocumentLiveAsync(
        AxataSynchronizationManualDocumentRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled in configuration.");
        }

        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            AxataSynchronizationJobExecutionMode.DryRun,
            warehouseNo,
            Guid.Empty,
            DateTime.UtcNow);

        return manualDocumentService.DispatchLiveAsync(
            context,
            new AxataSynchronizationManualDocumentInput(
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentNo,
                request.DocumentDate),
            cancellationToken);
    }

    public Task<AxataSynchronizationManualDispatchBatchDto> DispatchDocumentsLiveAsync(
        AxataSynchronizationManualDocumentBatchRequest request,
        int defaultWarehouseNo,
        CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled in configuration.");
        }

        var definition = GetEnabledDefinition(request.TaskCode);
        var warehouseNo = ResolveWarehouseNo(definition, request.WarehouseNo, defaultWarehouseNo);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            AxataSynchronizationJobExecutionMode.DryRun,
            warehouseNo,
            Guid.Empty,
            DateTime.UtcNow);

        return manualDocumentService.DispatchBatchLiveAsync(
            context,
            MapBatchInputs(request.Documents),
            request.ContinueOnError,
            cancellationToken);
    }

    private AxataSynchronizationTaskDefinition GetEnabledDefinition(string taskCode)
    {
        var definition = AxataSynchronizationCatalog.GetRequired(taskCode);
        var taskOptions = ResolveTaskOptions(options.CurrentValue, definition.Code);

        if (!taskOptions.Enabled)
        {
            throw new InvalidOperationException($"AXATA synchronization task '{definition.Code}' is disabled.");
        }

        return definition;
    }

    private static int? ResolveWarehouseNo(
        AxataSynchronizationTaskDefinition definition,
        int? requestedWarehouseNo,
        int defaultWarehouseNo)
    {
        if (!definition.RequiresWarehouseNo)
        {
            return null;
        }

        var warehouseNo = requestedWarehouseNo ?? defaultWarehouseNo;

        if (warehouseNo <= 0)
        {
            throw new ArgumentException(
                $"Task '{definition.Code}' requires a warehouse number greater than zero.",
                nameof(requestedWarehouseNo));
        }

        return warehouseNo;
    }

    private static int NormalizeTake(int? requestedTake, int configuredDefaultTake)
    {
        var take = requestedTake.GetValueOrDefault(configuredDefaultTake);
        return take <= 0 ? Math.Max(1, configuredDefaultTake) : Math.Min(take, 100);
    }

    private static int NormalizeSkip(int? requestedSkip) =>
        requestedSkip is > 0 ? requestedSkip.Value : 0;

    private static IReadOnlyCollection<AxataSynchronizationManualDocumentInput> MapBatchInputs(
        IReadOnlyCollection<AxataSynchronizationManualDocumentRequestItem> documents)
    {
        if (documents.Count == 0)
        {
            throw new ArgumentException("At least one document must be supplied for batch manual synchronization.");
        }

        return documents
            .Select(document => new AxataSynchronizationManualDocumentInput(
                document.DocumentSerie,
                document.DocumentOrderNo,
                document.DocumentNo,
                document.DocumentDate))
            .ToArray();
    }

    private static AxataSynchronizationDateRange ResolveDateRange(
        DateTime? requestedStartDate,
        DateTime? requestedEndDate,
        int configuredLookbackDays)
    {
        if (requestedStartDate.HasValue && requestedEndDate.HasValue)
        {
            var startDate = requestedStartDate.Value.Date;
            var endDate = requestedEndDate.Value.Date;

            if (endDate < startDate)
            {
                throw new ArgumentException("End date can not be earlier than start date.");
            }

            return new AxataSynchronizationDateRange(startDate, endDate);
        }

        if (requestedStartDate.HasValue)
        {
            var startDate = requestedStartDate.Value.Date;
            return new AxataSynchronizationDateRange(startDate, startDate);
        }

        if (requestedEndDate.HasValue)
        {
            var endDate = requestedEndDate.Value.Date;
            var normalizedLookbackDays = Math.Max(1, configuredLookbackDays);
            return new AxataSynchronizationDateRange(
                endDate.AddDays(-(normalizedLookbackDays - 1)),
                endDate);
        }

        var today = DateTime.Today;
        var lookbackDays = Math.Max(1, configuredLookbackDays);
        return new AxataSynchronizationDateRange(
            today.AddDays(-(lookbackDays - 1)),
            today);
    }

    private static AxataSynchronizationTaskOptions ResolveTaskOptions(
        AxataSynchronizationOptions options,
        string taskCode) =>
        options.Tasks.TryGetValue(taskCode, out var taskOptions)
            ? taskOptions
            : new AxataSynchronizationTaskOptions();

    private static bool SupportsManualDocuments(string taskCode) =>
        taskCode is "issued-warehouse-order-sync" or "company-receiving-sync" or "inventory-count-sync";

    private static bool SupportsLiveDispatch(string taskCode) =>
        taskCode is "product-master-sync" or "issued-warehouse-order-sync" or "company-receiving-sync";

    private static string? ResolveLiveOperationName(
        string taskCode,
        AxataSynchronizationOptions options)
    {
        if (!SupportsLiveDispatch(taskCode))
        {
            return null;
        }

        if (options.Tasks.TryGetValue(taskCode, out var taskOptions) &&
            !string.IsNullOrWhiteSpace(taskOptions.LiveOperationName))
        {
            return taskOptions.LiveOperationName.Trim();
        }

        return taskCode switch
        {
            "product-master-sync" => "addSKUMaster",
            "issued-warehouse-order-sync" => "addOutboundOrder",
            "company-receiving-sync" => "addInboundOrder",
            _ => null
        };
    }

    private static string ResolveEndpointUrl(
        AxataSynchronizationOptions options,
        AxataEndpointKind endpointKind) =>
        endpointKind switch
        {
            AxataEndpointKind.Main => options.MainEndpointUrl,
            AxataEndpointKind.Extended => options.ExtendedEndpointUrl,
            _ => string.Empty
        };

    private static string ToDisplayName(AxataEndpointKind endpointKind) =>
        endpointKind switch
        {
            AxataEndpointKind.Main => "AXATA Main Endpoint",
            AxataEndpointKind.Extended => "AXATA EXT Endpoint",
            _ => endpointKind.ToString()
        };

    public async Task<AxataOutboundDeliveriesByDateDto> GetOutboundDeliveriesByDateAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var normalizedDate = date.Date;
        var axataDateNumber = ToAxataDateNumber(normalizedDate);
        var headers = await axataDbContext.ENT006s
            .AsNoTracking()
            .Where(delivery => delivery.S06ITAR == axataDateNumber)
            .OrderBy(delivery => delivery.S06SIRA)
            .Select(delivery => new AxataOutboundDeliveryHeaderRow(
                delivery.S06SIRA,
                delivery.S06TESL ?? string.Empty,
                delivery.S06STAT,
                delivery.S06HTIP,
                delivery.S06FIRM,
                delivery.S06TFIR,
                delivery.S06ITAR,
                delivery.S06TMTR,
                delivery.S06PLKA,
                delivery.S06SURUCU))
            .ToListAsync(cancellationToken);

        List<AxataOutboundDeliveryLineSummary> lineSummaries;

        if (headers.Count == 0)
        {
            lineSummaries = [];
        }
        else
        {
            var deliveryNoQuery = axataDbContext.ENT006s
                .AsNoTracking()
                .Where(delivery =>
                    delivery.S06ITAR == axataDateNumber &&
                    delivery.S06TESL != null &&
                    delivery.S06TESL != string.Empty)
                .Select(delivery => delivery.S06TESL!)
                .Distinct();

            lineSummaries = await axataDbContext.ENT007s
                .AsNoTracking()
                .Join(
                    deliveryNoQuery,
                    line => line.S07TESL,
                    deliveryNo => deliveryNo,
                    (line, _) => line)
                .GroupBy(line => line.S07TESL!)
                .Select(group => new AxataOutboundDeliveryLineSummary(
                    group.Key,
                    group.Count(),
                    group.Sum(line => line.S07MIKT ?? 0m)))
                .ToListAsync(cancellationToken);
        }

        var lineSummariesByDeliveryNo = lineSummaries
            .GroupBy(summary => NormalizeText(summary.AxataDeliveryNo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new AxataOutboundDeliveryLineSummary(
                    group.Key,
                    group.Sum(summary => summary.LineCount),
                    group.Sum(summary => summary.Quantity)),
                StringComparer.OrdinalIgnoreCase);

        var items = headers
            .Select(header =>
            {
                var axataDeliveryNo = NormalizeText(header.AxataDeliveryNo);
                var (documentSerie, documentOrderNo) = ParseAxataDeliveryNo(axataDeliveryNo);
                var lineSummary = lineSummariesByDeliveryNo.GetValueOrDefault(axataDeliveryNo);

                return new AxataOutboundDeliveryByDateItemDto(
                    header.AxataSequenceNo,
                    axataDeliveryNo,
                    documentSerie,
                    documentOrderNo,
                    FormatDecimal(header.Status),
                    NormalizeNullableText(header.MovementType),
                    NormalizeNullableText(header.SourceWarehouseCode),
                    NormalizeNullableText(header.TargetWarehouseCode),
                    ParseAxataDateNumber(header.AxataDateNumber),
                    ParseAxataDateNumber(header.TransferDateNumber),
                    lineSummary?.LineCount ?? 0,
                    lineSummary is null ? 0d : (double)lineSummary.Quantity,
                    NormalizeNullableText(header.VehiclePlate),
                    NormalizeNullableText(header.DriverName));
            })
            .ToArray();

        return new AxataOutboundDeliveriesByDateDto(
            normalizedDate,
            axataDateNumber,
            DateTime.UtcNow,
            items.Length,
            items.Sum(item => item.LineCount),
            items.Sum(item => item.Quantity),
            items);
    }

    private static decimal ToAxataDateNumber(DateTime value) =>
        decimal.Parse(value.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static DateTime? ParseAxataDateNumber(decimal? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var normalized = value.Value.ToString("0", CultureInfo.InvariantCulture);
        return DateTime.TryParseExact(
            normalized,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static (string Serie, int? OrderNo) ParseAxataDeliveryNo(string deliveryNo)
    {
        var separatorIndex = deliveryNo.LastIndexOf('.');

        if (separatorIndex <= 0 || separatorIndex >= deliveryNo.Length - 1)
        {
            return (deliveryNo, null);
        }

        var serie = deliveryNo[..separatorIndex].Trim();
        var orderNoText = deliveryNo[(separatorIndex + 1)..].Trim();

        return int.TryParse(orderNoText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNo)
            ? (serie, orderNo)
            : (serie, null);
    }

    private static string FormatDecimal(decimal? value) =>
        value?.ToString("0.#############################", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string NormalizeText(string? value) =>
        value?.Trim() ?? string.Empty;

    private static string? NormalizeNullableText(string? value)
    {
        var normalized = NormalizeText(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}

internal sealed record AxataSynchronizationDateRange(DateTime StartDate, DateTime EndDate);

internal sealed record AxataOutboundDeliveryHeaderRow(
    long AxataSequenceNo,
    string AxataDeliveryNo,
    decimal? Status,
    string? MovementType,
    string? SourceWarehouseCode,
    string? TargetWarehouseCode,
    decimal? AxataDateNumber,
    decimal? TransferDateNumber,
    string? VehiclePlate,
    string? DriverName);

internal sealed record AxataOutboundDeliveryLineSummary(
    string AxataDeliveryNo,
    int LineCount,
    decimal Quantity);
