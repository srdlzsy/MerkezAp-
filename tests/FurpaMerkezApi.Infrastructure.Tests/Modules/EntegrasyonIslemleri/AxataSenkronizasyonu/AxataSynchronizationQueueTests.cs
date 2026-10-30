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
    public void Catalog_AssignsOperationalDocumentsHigherSchedulingPriorityThanMasters()
    {
        Assert.True(
            AxataSynchronizationCatalog.GetSchedulingPriority("received-company-order-sync") <
            AxataSynchronizationCatalog.GetSchedulingPriority("product-master-sync"));
    }

    [Fact]
    public void Catalog_AssignsOrderDispatchHigherPriorityThanHeavyImports()
    {
        Assert.True(
            AxataSynchronizationCatalog.GetSchedulingPriority("received-company-order-sync") <
            AxataSynchronizationCatalog.GetSchedulingPriority("c02-outbound-delivery-import"));
        Assert.True(AxataSynchronizationCatalog.IsOrderDispatchTask("received-company-order-sync"));
        Assert.False(AxataSynchronizationCatalog.IsOrderDispatchTask("c02-outbound-delivery-import"));
    }

    [Fact]
    public void Catalog_ReportsAllIndependentOrderDispatchFlows()
    {
        Assert.Equal(4, AxataSynchronizationCatalog.OrderDispatchTaskCount);
    }

    [Fact]
    public void Catalog_PreservesCentralWarehouseForReceivedCompanyOrderTask()
    {
        var definition = AxataSynchronizationCatalog.GetRequired("received-company-order-sync");

        var warehouseNo = AxataSynchronizationCatalog.ResolveScheduledWarehouseNo(definition, 50);

        Assert.Equal(50, warehouseNo);
    }

    [Fact]
    public void Catalog_PreservesConfiguredWarehouseForWarehouseScopedTask()
    {
        var definition = AxataSynchronizationCatalog.GetRequired("issued-warehouse-order-sync");

        var warehouseNo = AxataSynchronizationCatalog.ResolveScheduledWarehouseNo(definition, 50);

        Assert.Equal(50, warehouseNo);
    }

    [Fact]
    public async Task OrderDispatchLane_AllowsIndependentFlowsToBeConsumedWithoutSerialBlocking()
    {
        var queue = new AxataSynchronizationQueue(new FixedClock(DateTime.UtcNow));
        var expectedTaskCodes = new[]
        {
            "issued-warehouse-order-sync",
            "received-company-order-sync",
            "warehouse-inbound-order-sync",
            "company-receiving-sync"
        };

        foreach (var taskCode in expectedTaskCodes)
        {
            queue.Enqueue(
                AxataSynchronizationCatalog.GetRequired(taskCode),
                AxataSynchronizationJobExecutionMode.Live,
                AxataSynchronizationJobTriggerSource.Scheduled,
                null,
                Guid.Empty);
        }

        var consumed = new List<string>();
        for (var index = 0; index < expectedTaskCodes.Length; index++)
        {
            var workItem = await queue.DequeueOrderDispatchAsync(CancellationToken.None);
            consumed.Add(workItem.TaskCode);
        }

        Assert.Equal(expectedTaskCodes, consumed);
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

        var workItem = await queue.DequeueGeneralAsync(CancellationToken.None);
        queue.MarkRunning(workItem.JobId);
        var running = queue.GetBacklogSnapshot();
        Assert.Equal(0, running.QueuedCount);
        Assert.Equal(1, running.RunningCount);
        Assert.Null(running.OldestQueuedAtUtc);
        Assert.Equal(job.JobId, running.CurrentJob?.JobId);
        Assert.Equal("product-master-sync", running.CurrentJob?.TaskCode);
        Assert.Equal("Running", running.CurrentJob?.Status);
        Assert.Null(running.LastJob);

        queue.MarkSucceeded(
            job.JobId,
            new AxataSynchronizationTaskExecutionResult(1, "Completed.", []));
        var completed = queue.GetBacklogSnapshot();
        Assert.Equal(0, completed.QueuedCount);
        Assert.Equal(0, completed.RunningCount);
        Assert.Null(completed.CurrentJob);
        Assert.Equal(job.JobId, completed.LastJob?.JobId);
        Assert.Equal("Succeeded", completed.LastJob?.Status);
        Assert.Equal(1, completed.LastJob?.AffectedRecordCount);
        Assert.Equal("Completed.", completed.LastJob?.Message);
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

    [Fact]
    public async Task OrderDispatchLane_DoesNotWaitForBackgroundMaster()
    {
        var queue = new AxataSynchronizationQueue(new FixedClock(DateTime.UtcNow));
        var master = AxataSynchronizationCatalog.GetRequired("product-master-sync");
        var document = AxataSynchronizationCatalog.GetRequired("received-company-order-sync");

        queue.Enqueue(
            master,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);
        queue.Enqueue(
            document,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);

        var first = await queue.DequeueOrderDispatchAsync(CancellationToken.None);
        var general = await queue.DequeueGeneralAsync(CancellationToken.None);

        Assert.Equal("received-company-order-sync", first.TaskCode);
        Assert.Equal("product-master-sync", general.TaskCode);
    }

    [Fact]
    public async Task GeneralLane_PrioritizesOperationalImportOverBackgroundMaster()
    {
        var queue = new AxataSynchronizationQueue(new FixedClock(DateTime.UtcNow));
        var master = AxataSynchronizationCatalog.GetRequired("product-master-sync");
        var import = AxataSynchronizationCatalog.GetRequired("c02-outbound-delivery-import");

        queue.Enqueue(
            master,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);
        queue.Enqueue(
            import,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);

        var first = await queue.DequeueGeneralAsync(CancellationToken.None);

        Assert.Equal("c02-outbound-delivery-import", first.TaskCode);
    }

    [Fact]
    public async Task Dequeue_PrioritizesManualJobOverScheduledBackgroundMaster()
    {
        var queue = new AxataSynchronizationQueue(new FixedClock(DateTime.UtcNow));
        var scheduledMaster = AxataSynchronizationCatalog.GetRequired("product-master-sync");
        var manualMaster = AxataSynchronizationCatalog.GetRequired("firm-master-sync");

        queue.Enqueue(
            scheduledMaster,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);
        queue.Enqueue(
            manualMaster,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Manual,
            null,
            Guid.NewGuid());

        var first = await queue.DequeueGeneralAsync(CancellationToken.None);

        Assert.Equal("firm-master-sync", first.TaskCode);
    }

    [Fact]
    public async Task BacklogSnapshot_DifferentTaskSuccess_DoesNotHideUnresolvedFailure()
    {
        var queue = new AxataSynchronizationQueue(new FixedClock(DateTime.UtcNow));
        var failedDefinition = AxataSynchronizationCatalog.GetRequired("g02-inbound-delivery-import");
        var successfulDefinition = AxataSynchronizationCatalog.GetRequired("product-master-sync");
        var failedJob = queue.Enqueue(
            failedDefinition,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);
        var failedWorkItem = await queue.DequeueGeneralAsync(CancellationToken.None);
        queue.MarkRunning(failedWorkItem.JobId);
        queue.MarkFailed(failedWorkItem.JobId, "G02 belge eslesmedi.");

        queue.Enqueue(
            successfulDefinition,
            AxataSynchronizationJobExecutionMode.Live,
            AxataSynchronizationJobTriggerSource.Scheduled,
            null,
            Guid.Empty);
        var successfulWorkItem = await queue.DequeueGeneralAsync(CancellationToken.None);
        queue.MarkRunning(successfulWorkItem.JobId);
        queue.MarkSucceeded(
            successfulWorkItem.JobId,
            new AxataSynchronizationTaskExecutionResult(5, "5 urun aktarildi.", []));

        var snapshot = queue.GetBacklogSnapshot();

        Assert.Equal(1, snapshot.UnresolvedFailureCount);
        Assert.Equal(failedJob.JobId, snapshot.LastFailedJob?.JobId);
        Assert.Equal("G02 belge eslesmedi.", snapshot.LastFailedJob?.ErrorMessage);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
