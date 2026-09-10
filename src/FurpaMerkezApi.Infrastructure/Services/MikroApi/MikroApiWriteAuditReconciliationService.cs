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

        if (audits.Length == 0)
        {
            return 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning(
            "Reclassified {AuditCount} stale or incorrectly successful Mikro API write audits as Unknown.",
            audits.Length);

        return audits.Length;
    }
}
