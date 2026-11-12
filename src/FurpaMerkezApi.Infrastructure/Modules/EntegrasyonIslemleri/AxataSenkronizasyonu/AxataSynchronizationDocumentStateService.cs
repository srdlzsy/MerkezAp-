using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataSynchronizationDocumentStateService(
    AuthDbContext authDbContext,
    IClock clock,
    IOptionsMonitor<AxataSynchronizationOptions> optionsMonitor,
    ILogger<AxataSynchronizationDocumentStateService> logger)
{
    public async Task<IReadOnlySet<string>> GetDeferredDocumentKeysAsync(
        string taskCode,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedTaskCode = Normalize(taskCode);
        var now = clock.UtcNow;
        var manualReviewAfterFailureCount = ResolveManualReviewAfterFailureCount();
        var keys = await authDbContext.AxataSynchronizationDocumentStates
            .AsNoTracking()
            .Where(item =>
                item.TaskCode == normalizedTaskCode &&
                (item.NextAttemptAtUtc > now ||
                 item.FailureCount >= manualReviewAfterFailureCount))
            .Select(item => item.DocumentKey)
            .ToArrayAsync(cancellationToken);

        return keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task RecordOutcomeAsync(
        string taskCode,
        IEnumerable<string> succeededDocumentKeys,
        IEnumerable<AxataSynchronizationDocumentFailure> businessFailures,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return;
        }

        var normalizedTaskCode = Normalize(taskCode);
        var succeededKeys = succeededDocumentKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var failures = businessFailures
            .Where(item => !string.IsNullOrWhiteSpace(item.DocumentKey))
            .GroupBy(item => Normalize(item.DocumentKey), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().ErrorMessage,
                StringComparer.OrdinalIgnoreCase);
        var relevantKeys = succeededKeys
            .Concat(failures.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingStates = relevantKeys.Length == 0
            ? Array.Empty<AxataSynchronizationDocumentState>()
            : await authDbContext.AxataSynchronizationDocumentStates
                .Where(item =>
                    item.TaskCode == normalizedTaskCode &&
                    relevantKeys.Contains(item.DocumentKey))
                .ToArrayAsync(cancellationToken);

        foreach (var state in existingStates.Where(item => succeededKeys.Contains(item.DocumentKey)))
        {
            authDbContext.AxataSynchronizationDocumentStates.Remove(state);
        }

        var statesByKey = existingStates
            .Where(item => !succeededKeys.Contains(item.DocumentKey))
            .ToDictionary(item => item.DocumentKey, StringComparer.OrdinalIgnoreCase);
        var now = clock.UtcNow;

        foreach (var failure in failures)
        {
            if (statesByKey.TryGetValue(failure.Key, out var state))
            {
                if (RequiresManualReview(state))
                {
                    continue;
                }

                state.RegisterFailure(failure.Value, now);
                LogOutcome(normalizedTaskCode, state);
                continue;
            }

            var newState = new AxataSynchronizationDocumentState(
                Guid.NewGuid(),
                normalizedTaskCode,
                failure.Key,
                failure.Value,
                now);
            authDbContext.AxataSynchronizationDocumentStates.Add(newState);
            LogOutcome(normalizedTaskCode, newState);
        }

        var staleBoundary = now.AddDays(-30);
        var manualReviewAfterFailureCount = ResolveManualReviewAfterFailureCount();
        var staleStates = await authDbContext.AxataSynchronizationDocumentStates
            .Where(item =>
                item.LastFailedAtUtc < staleBoundary &&
                item.FailureCount < manualReviewAfterFailureCount)
            .ToArrayAsync(cancellationToken);
        authDbContext.AxataSynchronizationDocumentStates.RemoveRange(staleStates);

        if (authDbContext.ChangeTracker.HasChanges())
        {
            await authDbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private bool IsEnabled() =>
        optionsMonitor.CurrentValue.DocumentRetryState.Enabled;

    private int ResolveManualReviewAfterFailureCount() =>
        Math.Clamp(
            optionsMonitor.CurrentValue.DocumentRetryState.ManualReviewAfterFailureCount,
            3,
            100);

    private bool RequiresManualReview(AxataSynchronizationDocumentState state) =>
        state.FailureCount >= ResolveManualReviewAfterFailureCount();

    private void LogOutcome(string taskCode, AxataSynchronizationDocumentState state)
    {
        if (RequiresManualReview(state))
        {
            logger.LogError(
                "AXATA belge veri sorunu otomatik deneme sinirina ulasti; manuel inceleme gerekiyor. TaskCode={TaskCode}, DocumentKey={DocumentKey}, FailureCount={FailureCount}, Reason={Reason}",
                taskCode,
                state.DocumentKey,
                state.FailureCount,
                state.LastError);
            return;
        }

        logger.LogWarning(
            "AXATA belge veri sorunu nedeniyle kontrollu yeniden denemeye ertelendi. TaskCode={TaskCode}, DocumentKey={DocumentKey}, FailureCount={FailureCount}, NextAttemptAtUtc={NextAttemptAtUtc}, Reason={Reason}",
            taskCode,
            state.DocumentKey,
            state.FailureCount,
            state.NextAttemptAtUtc,
            state.LastError);
    }
}

internal sealed record AxataSynchronizationDocumentFailure(
    string DocumentKey,
    string ErrorMessage);
