using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal abstract class AxataOutboundDeliveryImportTaskHandler(
    IAxataOutboundDeliveryImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : IAxataSynchronizationTaskHandler
{
    public abstract string Code { get; }

    protected abstract string MovementType { get; }

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var preview = await PreviewImportAsync(
            new AxataOutboundDeliveryImportPreviewRequest(take),
            cancellationToken);

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            null,
            preview.TotalFetchedDocumentCount,
            preview.ReturnedDocumentCount,
            preview.GeneratedAtUtc,
            preview.Documents.Select(document => new AxataSynchronizationPreviewItemDto(
                    $"{document.DocumentSerie}.{document.DocumentOrderNo}",
                    $"{document.AxataLineCount} satir / {document.AxataQuantity:0.##} miktar / {document.Warning ?? "aktarilabilir"}",
                    JsonSerializer.Serialize(document, AxataSynchronizationJson.Options)))
                .ToArray(),
            preview.Notes);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var batchSize = GetBatchSize(options.CurrentValue, Code);
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Live)
        {
            var preview = await PreviewImportAsync(
                new AxataOutboundDeliveryImportPreviewRequest(batchSize),
                cancellationToken);
            var artifacts = context.ExecutionMode == AxataSynchronizationJobExecutionMode.Outbox
                ? new[] { await outboxWriter.WritePayloadAsync(context, preview, cancellationToken) }
                : Array.Empty<AxataSynchronizationJobArtifactDto>();

            return new AxataSynchronizationTaskExecutionResult(
                preview.ReturnedDocumentCount,
                $"{preview.ReturnedDocumentCount} bekleyen AXATA {MovementType} belgesi hazirlandi.",
                artifacts);
        }

        var result = await ExecuteImportAsync(
            new AxataOutboundDeliveryImportExecuteRequest(
                batchSize,
                ContinueOnError: true,
                Acknowledge: true),
            context.RequestedByUserId,
            cancellationToken);

        if (result.FailedDocumentCount > 0)
        {
            throw new InvalidOperationException(
                $"AXATA {MovementType} aktariminda {result.FailedDocumentCount} belge basarisiz oldu. Basarili={result.SucceededDocumentCount}, Atlanan={result.SkippedDocumentCount}. Nedenler: {FormatFailures(result.Failures)}");
        }

        return new AxataSynchronizationTaskExecutionResult(
            result.SucceededDocumentCount,
            $"{result.SucceededDocumentCount} AXATA {MovementType} belgesi Mikro'ya aktarildi. Satir={result.CreatedMovementLineCount}, Atlanan={result.SkippedDocumentCount}.",
            Array.Empty<AxataSynchronizationJobArtifactDto>());
    }

    protected abstract Task<AxataOutboundDeliveryImportPreviewDto> PreviewImportAsync(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken);

    protected abstract Task<AxataOutboundDeliveryImportExecuteDto> ExecuteImportAsync(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken);

    private static int GetBatchSize(AxataSynchronizationOptions currentOptions, string taskCode) =>
        currentOptions.Tasks.TryGetValue(taskCode, out var taskOptions)
            ? Math.Clamp(taskOptions.BatchSize, 1, 500)
            : 100;

    private static string FormatFailures(
        IReadOnlyCollection<AxataOutboundDeliveryImportFailureDto> failures) =>
        string.Join(" | ", failures
            .Take(10)
            .Select(failure =>
                $"{failure.AxataDeliveryNo ?? failure.AxataSequenceNo?.ToString() ?? "bilinmeyen-belge"}: {failure.ErrorMessage}"));

    protected IAxataOutboundDeliveryImportService ImportService { get; } = importService;
}

internal sealed class C01OutboundDeliveryImportTaskHandler(
    IAxataOutboundDeliveryImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, options)
{
    public override string Code => "c01-outbound-delivery-import";

    protected override string MovementType => "C01";

    protected override Task<AxataOutboundDeliveryImportPreviewDto> PreviewImportAsync(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken) => ImportService.PreviewC01Async(request, cancellationToken);

    protected override Task<AxataOutboundDeliveryImportExecuteDto> ExecuteImportAsync(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken) => ImportService.ExecuteC01Async(request, requestedByUserId, cancellationToken);
}

internal sealed class C02OutboundDeliveryImportTaskHandler(
    IAxataOutboundDeliveryImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, options)
{
    public override string Code => "c02-outbound-delivery-import";

    protected override string MovementType => "C02";

    protected override Task<AxataOutboundDeliveryImportPreviewDto> PreviewImportAsync(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken) => ImportService.PreviewC02Async(request, cancellationToken);

    protected override Task<AxataOutboundDeliveryImportExecuteDto> ExecuteImportAsync(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken) => ImportService.ExecuteC02Async(request, requestedByUserId, cancellationToken);
}

internal sealed class C03OutboundDeliveryImportTaskHandler(
    IAxataOutboundDeliveryImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, options)
{
    public override string Code => "c03-outbound-delivery-import";

    protected override string MovementType => "C03";

    protected override Task<AxataOutboundDeliveryImportPreviewDto> PreviewImportAsync(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken) => ImportService.PreviewC03Async(request, cancellationToken);

    protected override Task<AxataOutboundDeliveryImportExecuteDto> ExecuteImportAsync(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken) => ImportService.ExecuteC03Async(request, requestedByUserId, cancellationToken);
}

internal sealed class C04OutboundDeliveryImportTaskHandler(
    IAxataOutboundDeliveryImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, options)
{
    public override string Code => "c04-outbound-delivery-import";

    protected override string MovementType => "C4";

    protected override Task<AxataOutboundDeliveryImportPreviewDto> PreviewImportAsync(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken) => ImportService.PreviewC04Async(request, cancellationToken);

    protected override Task<AxataOutboundDeliveryImportExecuteDto> ExecuteImportAsync(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken) => ImportService.ExecuteC04Async(request, requestedByUserId, cancellationToken);
}

internal sealed class G02InboundDeliveryImportTaskHandler(
    IAxataOutboundDeliveryImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, options)
{
    public override string Code => "g02-inbound-delivery-import";

    protected override string MovementType => "G02";

    protected override Task<AxataOutboundDeliveryImportPreviewDto> PreviewImportAsync(
        AxataOutboundDeliveryImportPreviewRequest request,
        CancellationToken cancellationToken) => ImportService.PreviewG02Async(request, cancellationToken);

    protected override Task<AxataOutboundDeliveryImportExecuteDto> ExecuteImportAsync(
        AxataOutboundDeliveryImportExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken) => ImportService.ExecuteG02Async(request, requestedByUserId, cancellationToken);
}

internal sealed class G01InboundAtfImportTaskHandler(
    IAxataG01InboundAtfImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : IAxataSynchronizationTaskHandler
{
    public string Code => "g01-inbound-atf-import";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var preview = await importService.PreviewAsync(
            new AxataG01InboundAtfPreviewRequest(take),
            cancellationToken);

        return new AxataSynchronizationPreviewDto(
            context.Definition.Code,
            context.Definition.Name,
            null,
            preview.TotalFetchedLineCount,
            preview.ReturnedDocumentCount,
            preview.GeneratedAtUtc,
            preview.Documents.Select(document => new AxataSynchronizationPreviewItemDto(
                    document.OrderDocumentNo,
                    $"{document.AxataLineCount} satir / {document.AxataQuantity:0.##} miktar / {document.Warning ?? "aktarilabilir"}",
                    JsonSerializer.Serialize(document, AxataSynchronizationJson.Options)))
                .ToArray(),
            preview.Notes);
    }

    public async Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var batchSize = options.CurrentValue.Tasks.TryGetValue(Code, out var taskOptions)
            ? Math.Clamp(taskOptions.BatchSize, 1, 500)
            : 100;
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Live)
        {
            var preview = await importService.PreviewAsync(
                new AxataG01InboundAtfPreviewRequest(batchSize),
                cancellationToken);
            var artifacts = context.ExecutionMode == AxataSynchronizationJobExecutionMode.Outbox
                ? new[] { await outboxWriter.WritePayloadAsync(context, preview, cancellationToken) }
                : Array.Empty<AxataSynchronizationJobArtifactDto>();

            return new AxataSynchronizationTaskExecutionResult(
                preview.ReturnedDocumentCount,
                $"{preview.ReturnedDocumentCount} bekleyen AXATA G01 belgesi hazirlandi.",
                artifacts);
        }

        var result = await importService.ExecuteAsync(
            new AxataG01InboundAtfExecuteRequest(
                batchSize,
                ContinueOnError: true,
                Acknowledge: true),
            context.RequestedByUserId,
            cancellationToken);

        if (result.FailedDocumentCount > 0)
        {
            throw new InvalidOperationException(
                $"AXATA G01 aktariminda {result.FailedDocumentCount} belge basarisiz oldu. Basarili={result.SucceededDocumentCount}, Atlanan={result.SkippedDocumentCount}. Nedenler: {FormatFailures(result.Failures)}");
        }

        return new AxataSynchronizationTaskExecutionResult(
            result.SucceededDocumentCount,
            $"{result.SucceededDocumentCount} AXATA G01 belgesi Mikro'ya aktarildi. Satir={result.CreatedMovementLineCount}, Atlanan={result.SkippedDocumentCount}.",
            Array.Empty<AxataSynchronizationJobArtifactDto>());
    }

    private static string FormatFailures(IReadOnlyCollection<AxataG01InboundAtfFailureDto> failures) =>
        string.Join(" | ", failures
            .Take(10)
            .Select(failure => $"{failure.OrderDocumentNo ?? "bilinmeyen-belge"}: {failure.ErrorMessage}"));
}
