using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.OperasyonIslemleri.Operations;

public sealed class OperationsJobQueueTests
{
    [Fact]
    public void Enqueue_RejectsNewWorkWhenBoundedChannelIsFull()
    {
        var queue = Queue(capacity: 1);
        var first = queue.Enqueue(OperationFileKind.ScalesFile, 110, Guid.NewGuid());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            queue.Enqueue(OperationFileKind.CashierFile, 110, Guid.NewGuid()));

        Assert.Contains("queue is full", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.JobId, queue.Get(first.JobId).JobId);
    }

    [Fact]
    public async Task Enqueue_RemovesCompletedJobsAfterRetentionPeriod()
    {
        var clock = new MutableClock(new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc));
        var queue = Queue(clock: clock, completedRetention: TimeSpan.FromMinutes(5));
        var completed = queue.Enqueue(OperationFileKind.ScalesFile, 110, Guid.NewGuid());
        await queue.DequeueAsync(CancellationToken.None);
        queue.MarkRunning(completed.JobId);
        queue.MarkSucceeded(completed.JobId, "Completed.", []);

        clock.UtcNow = clock.UtcNow.AddMinutes(6);
        queue.Enqueue(OperationFileKind.CashierFile, 110, Guid.NewGuid());

        Assert.Throws<KeyNotFoundException>(() => queue.Get(completed.JobId));
    }

    [Fact]
    public async Task Completion_KeepsOnlyConfiguredNumberOfCompletedJobs()
    {
        var queue = Queue(maximumCompletedJobs: 1);
        var first = queue.Enqueue(OperationFileKind.ScalesFile, 110, Guid.NewGuid());
        await queue.DequeueAsync(CancellationToken.None);
        queue.MarkSucceeded(first.JobId, "First.", []);

        var second = queue.Enqueue(OperationFileKind.CashierFile, 110, Guid.NewGuid());
        await queue.DequeueAsync(CancellationToken.None);
        queue.MarkSucceeded(second.JobId, "Second.", []);

        Assert.Throws<KeyNotFoundException>(() => queue.Get(first.JobId));
        Assert.Equal(second.JobId, queue.Get(second.JobId).JobId);
    }

    private static OperationsJobQueue Queue(
        int capacity = 10,
        MutableClock? clock = null,
        TimeSpan? completedRetention = null,
        int maximumCompletedJobs = 10) =>
        new(
            clock ?? new MutableClock(new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc)),
            capacity,
            completedRetention ?? TimeSpan.FromHours(1),
            maximumCompletedJobs);

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
