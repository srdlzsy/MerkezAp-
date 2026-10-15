using System.Text.Json;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal sealed class MikroApiWriteAuditReconciliationService(
    AuthDbContext dbContext,
    IClock clock,
    IOptionsMonitor<MikroApiWriteAuditOptions> options,
    ILogger<MikroApiWriteAuditReconciliationService> logger)
{
    private const string TimeoutMarker = "MikroAPI - TimeOut";

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.Enabled || !currentOptions.ReconciliationEnabled)
        {
            return 0;
        }

        var now = clock.UtcNow;
        var stalePendingCutoff = now.AddSeconds(
            -Math.Clamp(currentOptions.StalePendingAfterSeconds, 600, 86400));
        var batchSize = Math.Clamp(currentOptions.ReconciliationBatchSize, 1, 1000);

        var audits = await dbContext.MikroApiWriteAudits
            .Where(audit =>
                (audit.Status == MikroApiWriteAuditStatus.Pending &&
                 audit.CreatedAtUtc <= stalePendingCutoff) ||
                (audit.Status == MikroApiWriteAuditStatus.Succeeded &&
                 audit.Response != null &&
                 audit.Response.Contains(TimeoutMarker)))
            .OrderBy(audit => audit.CreatedAtUtc)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        foreach (var audit in audits)
        {
            var reason = audit.Status == MikroApiWriteAuditStatus.Pending
                ? "Mikro API audit remained pending beyond the configured reconciliation window; write outcome is unknown."
                : "Historical Mikro API timeout response was previously classified as succeeded; write outcome is unknown.";

            audit.MarkOutcomeUnknown(reason, now);
        }

        if (audits.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var reconciledReadbacks = await ReconcileCompletedSafeWritesAsync(
            now,
            batchSize,
            cancellationToken);

        if (audits.Length == 0 && reconciledReadbacks == 0)
        {
            return 0;
        }

        if (reconciledReadbacks > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        if (audits.Length > 0)
        {
            logger.LogWarning(
                "Classified {AuditCount} stale or incorrectly successful Mikro API write audits as Unknown. No business write was retried.",
                audits.Length);
        }

        if (reconciledReadbacks > 0)
        {
            logger.LogInformation(
                "Reconciled {AuditCount} Mikro API write audits from trace-matched safe writes that had already completed verified readback.",
                reconciledReadbacks);
        }

        var reconciledAfterClassification = audits.Count(audit =>
            audit.Status == MikroApiWriteAuditStatus.RecoveredAfterUnknown);
        return audits.Length + reconciledReadbacks - reconciledAfterClassification;
    }

    private async Task<int> ReconcileCompletedSafeWritesAsync(
        DateTime now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var unknownAudits = await dbContext.MikroApiWriteAudits
            .Where(audit =>
                audit.Status == MikroApiWriteAuditStatus.Unknown &&
                audit.TraceKey != null)
            .OrderBy(audit => audit.CreatedAtUtc)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        var auditIdsByClientRequestId = unknownAudits
            .Select(audit => new
            {
                Audit = audit,
                ClientRequestId = TryDecodeTraceKey(audit.TraceKey)
            })
            .Where(item => item.ClientRequestId.HasValue)
            .ToArray();

        if (auditIdsByClientRequestId.Length == 0)
        {
            return 0;
        }

        var clientRequestIds = auditIdsByClientRequestId
            .Select(item => item.ClientRequestId!.Value.ToString("D").ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var safeWrites = await dbContext.MobileOfflineSyncRequests
            .AsNoTracking()
            .Where(request =>
                request.Status == MobileOfflineSyncRequestStatus.Completed &&
                request.ResponsePayload != null &&
                clientRequestIds.Contains(request.ClientRequestId))
            .ToArrayAsync(cancellationToken);

        var safeWritesByClientRequestId = safeWrites
            .GroupBy(request => request.ClientRequestId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CompletedAtUtc).First(), StringComparer.Ordinal);

        var reconciled = 0;
        foreach (var item in auditIdsByClientRequestId)
        {
            var clientRequestId = item.ClientRequestId!.Value.ToString("D").ToLowerInvariant();
            if (!safeWritesByClientRequestId.TryGetValue(clientRequestId, out var safeWrite) ||
                !MatchesAuditContext(item.Audit, safeWrite))
            {
                continue;
            }

            var recovered = ExtractRecoveredDocument(safeWrite.ResponsePayload);
            if (recovered.DocumentNo is null && recovered.Guid is null)
            {
                continue;
            }

            item.Audit.MarkRecovered(recovered.DocumentNo, recovered.Guid, null, now);
            reconciled++;
        }

        return reconciled;
    }

    private static bool MatchesAuditContext(
        MikroApiWriteAudit audit,
        MobileOfflineSyncRequest safeWrite)
    {
        if (audit.WarehouseNo.HasValue && audit.WarehouseNo.Value != safeWrite.WarehouseNo)
        {
            return false;
        }

        var requestContext = MikroApiWriteAuditContextExtractor.Extract(safeWrite.RequestPayload);
        return !audit.LineCount.HasValue ||
               !requestContext.LineCount.HasValue ||
               audit.LineCount.Value == requestContext.LineCount.Value;
    }

    private static Guid? TryDecodeTraceKey(string? traceKey)
    {
        if (string.IsNullOrWhiteSpace(traceKey) ||
            !traceKey.StartsWith("FR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var encoded = traceKey[2..]
                .Replace('-', '+')
                .Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            var bytes = Convert.FromBase64String(encoded);
            return bytes.Length == 16 ? new Guid(bytes) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static (string? DocumentNo, Guid? Guid) ExtractRecoveredDocument(string? responsePayload)
    {
        if (string.IsNullOrWhiteSpace(responsePayload)) return (null, null);

        try
        {
            using var document = JsonDocument.Parse(responsePayload);
            var root = document.RootElement;
            var serie = FindString(root, "documentSerie");
            var orderNo = FindInt32(root, "documentOrderNo") ?? FindInt32(root, "documentNo");
            var documentNo = orderNo.HasValue
                ? string.IsNullOrWhiteSpace(serie) ? orderNo.Value.ToString() : $"{serie}/{orderNo.Value}"
                : null;
            var recoveredGuid = FindGuid(root, "documentGuid") ?? FindFirstGuid(root, "movementGuids");
            return (documentNo, recoveredGuid);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? FindString(JsonElement root, string propertyName) =>
        TryFindProperty(root, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? FindInt32(JsonElement root, string propertyName)
    {
        if (!TryFindProperty(root, propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)
            ? number
            : null;
    }

    private static Guid? FindGuid(JsonElement root, string propertyName) =>
        TryFindProperty(root, propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        Guid.TryParse(value.GetString(), out var guid)
            ? guid
            : null;

    private static Guid? FindFirstGuid(JsonElement root, string propertyName)
    {
        if (!TryFindProperty(root, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var guid))
            {
                return guid;
            }
        }

        return null;
    }

    private static bool TryFindProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
