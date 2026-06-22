using System.Text.Json;
using System.Text.Json.Serialization;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal interface IAxataSynchronizationTaskHandler
{
    string Code { get; }

    Task<AxataSynchronizationPreviewDto> PreviewAsync(
        AxataSynchronizationTaskExecutionContext context,
        int take,
        CancellationToken cancellationToken);

    Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken);
}

internal sealed class AxataSynchronizationExecutionCoordinator(IEnumerable<IAxataSynchronizationTaskHandler> handlers)
{
    private readonly IReadOnlyDictionary<string, IAxataSynchronizationTaskHandler> handlerMap = handlers
        .ToDictionary(handler => handler.Code, StringComparer.OrdinalIgnoreCase);

    public Task<AxataSynchronizationPreviewDto> PreviewAsync(
        string taskCode,
        int? warehouseNo,
        int take,
        CancellationToken cancellationToken)
    {
        var definition = AxataSynchronizationCatalog.GetRequired(taskCode);
        var handler = GetRequiredHandler(taskCode);
        var context = new AxataSynchronizationTaskExecutionContext(
            Guid.Empty,
            definition,
            AxataSynchronizationJobExecutionMode.DryRun,
            warehouseNo,
            Guid.Empty,
            DateTime.UtcNow);

        return handler.PreviewAsync(context, take, cancellationToken);
    }

    public Task<AxataSynchronizationTaskExecutionResult> ExecuteAsync(
        AxataSynchronizationTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (context.ExecutionMode == AxataSynchronizationJobExecutionMode.Live &&
            !context.Definition.Code.Equals("product-master-sync", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"Live job execution is not supported for task '{context.Definition.Code}'.");
        }

        return GetRequiredHandler(context.Definition.Code).ExecuteAsync(context, cancellationToken);
    }

    private IAxataSynchronizationTaskHandler GetRequiredHandler(string taskCode) =>
        handlerMap.TryGetValue(taskCode, out var handler)
            ? handler
            : throw new InvalidOperationException(
                $"AXATA synchronization handler is not registered for task '{taskCode}'.");
}

internal sealed record AxataSynchronizationTaskExecutionContext(
    Guid JobId,
    AxataSynchronizationTaskDefinition Definition,
    AxataSynchronizationJobExecutionMode ExecutionMode,
    int? WarehouseNo,
    Guid RequestedByUserId,
    DateTime RequestedAtUtc);

internal sealed record AxataSynchronizationTaskExecutionResult(
    int AffectedRecordCount,
    string Message,
    IReadOnlyCollection<AxataSynchronizationJobArtifactDto> Artifacts);

internal static class AxataSynchronizationJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
