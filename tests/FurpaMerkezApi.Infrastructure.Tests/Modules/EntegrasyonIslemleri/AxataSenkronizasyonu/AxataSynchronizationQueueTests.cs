using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationQueueTests
{
    [Fact]
    public void Catalog_ContainsEveryLegacyWorkerFlow()
    {
        var taskCodes = AxataSynchronizationCatalog.Definitions
            .Select(definition => definition.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Subset(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "firm-master-sync",
                "product-master-sync",
                "issued-warehouse-order-sync",
                "received-company-order-sync",
                "warehouse-inbound-order-sync",
                "company-receiving-sync",
                "inventory-count-sync",
                "c01-outbound-delivery-import",
                "c02-outbound-delivery-import",
                "c03-outbound-delivery-import",
                "c04-outbound-delivery-import",
                "g01-inbound-atf-import",
                "g02-inbound-delivery-import"
            },
            taskCodes);
    }

    [Fact]
    public async Task BacklogSnapshot_TracksQueuedAndRunningJobs()
    {
        var createdAtUtc = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        var queue = new AxataSynchronizationQueue(new FixedClock(createdAtUtc));
        var definition = AxataSynchronizationCatalog.GetRequired("product-master-sync");
        var job = queue.Enqueue(
            definition,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Manual,
            null,
            Guid.NewGuid());

        var queued = queue.GetBacklogSnapshot();
        Assert.Equal(1, queued.QueuedCount);
        Assert.Equal(0, queued.RunningCount);
        Assert.Equal(createdAtUtc, queued.OldestQueuedAtUtc);

        var workItem = await queue.DequeueAsync(CancellationToken.None);
        queue.MarkRunning(workItem.JobId);
        var running = queue.GetBacklogSnapshot();
        Assert.Equal(0, running.QueuedCount);
        Assert.Equal(1, running.RunningCount);
        Assert.Null(running.OldestQueuedAtUtc);

        queue.MarkSucceeded(
            job.JobId,
            new AxataSynchronizationTaskExecutionResult(1, "Completed.", []));
        var completed = queue.GetBacklogSnapshot();
        Assert.Equal(0, completed.QueuedCount);
        Assert.Equal(0, completed.RunningCount);
    }

    [Fact]
    public void Enqueue_RejectsDuplicateActiveTask()
    {
        var queue = new AxataSynchronizationQueue(new FixedClock(DateTime.UtcNow));
        var definition = AxataSynchronizationCatalog.GetRequired("c01-outbound-delivery-import");

        queue.Enqueue(
            definition,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() => queue.Enqueue(
            definition,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Manual,
            null,
            Guid.NewGuid()));

        Assert.Contains("already has an active job", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
