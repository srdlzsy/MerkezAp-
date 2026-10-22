using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal abstract class AxataOutboundDeliveryImportTaskHandler(
    IAxataOutboundDeliveryImportService importService,
    AxataSynchronizationOutboxWriter outboxWriter,
    AxataSynchronizationDocumentStateService documentStateService,
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
        var minimumAxataDate = AxataSynchronizationImportWindow.GetMinimumDate(options.CurrentValue, Code);
        var preview = await PreviewImportAsync(
            new AxataOutboundDeliveryImportPreviewRequest(take, minimumAxataDate),
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
        var minimumAxataDate = AxataSynchronizationImportWindow.GetMinimumDate(options.CurrentValue, Code);
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Live)
        {
            var preview = await PreviewImportAsync(
                new AxataOutboundDeliveryImportPreviewRequest(batchSize, minimumAxataDate),
                cancellationToken);
            var artifacts = context.ExecutionMode == AxataSynchronizationJobExecutionMode.Outbox
                ? new[] { await outboxWriter.WritePayloadAsync(context, preview, cancellationToken) }
                : Array.Empty<AxataSynchronizationJobArtifactDto>();

            return new AxataSynchronizationTaskExecutionResult(
                preview.ReturnedDocumentCount,
                $"{preview.ReturnedDocumentCount} bekleyen AXATA {MovementType} belgesi hazirlandi.",
                artifacts);
        }

        var deferredDocumentKeys = await documentStateService.GetDeferredDocumentKeysAsync(
            Code,
            cancellationToken);
        var excludedSequenceNos = deferredDocumentKeys
            .Select(key => long.TryParse(key, out var value) ? (long?)value : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        var result = await ExecuteImportAsync(
            new AxataOutboundDeliveryImportExecuteRequest(
                batchSize,
                ContinueOnError: true,
                Acknowledge: true,
                MinimumAxataDate: minimumAxataDate,
                ExcludedAxataSequenceNos: excludedSequenceNos),
            context.RequestedByUserId,
            cancellationToken);

        var infrastructureFailures = result.Failures
            .Where(failure => failure.IsInfrastructureFailure)
            .ToArray();
        var businessFailures = result.Failures
            .Where(failure => !failure.IsInfrastructureFailure && failure.AxataSequenceNo.HasValue)
            .Select(failure => new AxataSynchronizationDocumentFailure(
                failure.AxataSequenceNo!.Value.ToString(),
                failure.ErrorMessage))
            .ToArray();
        await documentStateService.RecordOutcomeAsync(
            Code,
            result.Results.Select(item => item.AxataSequenceNo.ToString()),
            businessFailures,
            cancellationToken);

        if (infrastructureFailures.Length > 0)
        {
            throw new InvalidOperationException(
                $"AXATA {MovementType} aktariminda {infrastructureFailures.Length} altyapi hatasi olustu. Basarili={result.SucceededDocumentCount}. Nedenler: {FormatFailures(infrastructureFailures)}");
        }

        return new AxataSynchronizationTaskExecutionResult(
            result.SucceededDocumentCount,
            $"{result.SucceededDocumentCount} AXATA {MovementType} belgesi tamamlandi. Satir={result.CreatedMovementLineCount}, Veri bekleyen={businessFailures.Length}, Ertelenen={deferredDocumentKeys.Count}.",
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
    AxataSynchronizationDocumentStateService documentStateService,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, documentStateService, options)
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
    AxataSynchronizationDocumentStateService documentStateService,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, documentStateService, options)
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
    AxataSynchronizationDocumentStateService documentStateService,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, documentStateService, options)
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
    AxataSynchronizationDocumentStateService documentStateService,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, documentStateService, options)
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
    AxataSynchronizationDocumentStateService documentStateService,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : AxataOutboundDeliveryImportTaskHandler(importService, outboxWriter, documentStateService, options)
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
    AxataSynchronizationDocumentStateService documentStateService,
    IOptionsMonitor<AxataSynchronizationOptions> options)
    : IAxataSynchronizationTaskHandler
{
    public string Code => "g01-inbound-atf-import";

    public async Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken)
    {
        var minimumAxataDate = AxataSynchronizationImportWindow.GetMinimumDate(options.CurrentValue, Code);
        var preview = await importService.PreviewAsync(
            new AxataG01InboundAtfPreviewRequest(take, minimumAxataDate),
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
        var minimumAxataDate = AxataSynchronizationImportWindow.GetMinimumDate(options.CurrentValue, Code);
        if (context.ExecutionMode != AxataSynchronizationJobExecutionMode.Live)
        {
            var preview = await importService.PreviewAsync(
                new AxataG01InboundAtfPreviewRequest(batchSize, minimumAxataDate),
                cancellationToken);
            var artifacts = context.ExecutionMode == AxataSynchronizationJobExecutionMode.Outbox
                ? new[] { await outboxWriter.WritePayloadAsync(context, preview, cancellationToken) }
                : Array.Empty<AxataSynchronizationJobArtifactDto>();

            return new AxataSynchronizationTaskExecutionResult(
                preview.ReturnedDocumentCount,
                $"{preview.ReturnedDocumentCount} bekleyen AXATA G01 belgesi hazirlandi.",
                artifacts);
        }

        var deferredDocumentKeys = await documentStateService.GetDeferredDocumentKeysAsync(
            Code,
            cancellationToken);
        var result = await importService.ExecuteAsync(
            new AxataG01InboundAtfExecuteRequest(
                batchSize,
                ContinueOnError: true,
                Acknowledge: true,
                MinimumAxataDate: minimumAxataDate,
                ExcludedOrderDocumentNos: deferredDocumentKeys.ToArray()),
            context.RequestedByUserId,
            cancellationToken);

        var infrastructureFailures = result.Failures
            .Where(failure => failure.IsInfrastructureFailure)
            .ToArray();
        var businessFailures = result.Failures
            .Where(failure =>
                !failure.IsInfrastructureFailure &&
                !string.IsNullOrWhiteSpace(failure.OrderDocumentNo))
            .Select(failure => new AxataSynchronizationDocumentFailure(
                failure.OrderDocumentNo!,
                failure.ErrorMessage))
            .ToArray();
        await documentStateService.RecordOutcomeAsync(
            Code,
            result.Results.Select(item => item.OrderDocumentNo),
            businessFailures,
            cancellationToken);

        if (infrastructureFailures.Length > 0)
        {
            throw new InvalidOperationException(
                $"AXATA G01 aktariminda {infrastructureFailures.Length} altyapi hatasi olustu. Basarili={result.SucceededDocumentCount}. Nedenler: {FormatFailures(infrastructureFailures)}");
        }

        return new AxataSynchronizationTaskExecutionResult(
            result.SucceededDocumentCount,
            $"{result.SucceededDocumentCount} AXATA G01 belgesi tamamlandi. Satir={result.CreatedMovementLineCount}, Veri bekleyen={businessFailures.Length}, Ertelenen={deferredDocumentKeys.Count}.",
            Array.Empty<AxataSynchronizationJobArtifactDto>());
    }

    private static string FormatFailures(IReadOnlyCollection<AxataG01InboundAtfFailureDto> failures) =>
        string.Join(" | ", failures
            .Take(10)
            .Select(failure => $"{failure.OrderDocumentNo ?? "bilinmeyen-belge"}: {failure.ErrorMessage}"));

}

internal static class AxataSynchronizationImportWindow
{
    public static DateTime? GetMinimumDate(
        AxataSynchronizationOptions currentOptions,
        string taskCode,
        DateTime? today = null)
    {
        if (!currentOptions.Tasks.TryGetValue(taskCode, out var taskOptions) ||
            taskOptions.ImportLookbackDays <= 0)
        {
            return null;
        }

        return (today ?? DateTime.Today).Date.AddDays(-(taskOptions.ImportLookbackDays - 1));
    }
}
