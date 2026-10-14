using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataSynchronizationQueueTests
{
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

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
