using System.Collections.Concurrent;
using System.Threading.Channels;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoPriceStockSnapshotCoordinator
{
    private readonly Channel<long> refreshQueue = Channel.CreateUnbounded<long>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<long, byte> pendingStores = new();
    private readonly ConcurrentDictionary<long, SnapshotState> states = new();

    public SnapshotState GetState(long storeId) =>
        states.GetOrAdd(storeId, static _ => SnapshotState.Empty);

    public bool RequestRefresh(long storeId)
    {
        if (!pendingStores.TryAdd(storeId, 0))
        {
            return false;
        }

        states.AddOrUpdate(
            storeId,
            static _ => SnapshotState.Empty with { RefreshState = SnapshotRefreshState.Queued },
            static (_, current) => current with
            {
                RefreshState = SnapshotRefreshState.Queued,
                RefreshError = null
            });

        if (refreshQueue.Writer.TryWrite(storeId))
        {
            return true;
        }

        pendingStores.TryRemove(storeId, out _);
        MarkFailed(storeId, "Snapshot refresh could not be queued.");
        return false;
    }

    public bool TryDequeue(out long storeId) => refreshQueue.Reader.TryRead(out storeId);

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) =>
        refreshQueue.Reader.WaitToReadAsync(cancellationToken);

    public void MarkRefreshing(long storeId)
    {
        var now = DateTime.UtcNow;
        states.AddOrUpdate(
            storeId,
            static (_, timestamp) => SnapshotState.Empty with
            {
                RefreshState = SnapshotRefreshState.Refreshing,
                RefreshStartedAtUtc = timestamp
            },
            static (_, current, timestamp) => current with
            {
                RefreshState = SnapshotRefreshState.Refreshing,
                RefreshStartedAtUtc = timestamp,
                RefreshError = null
            },
            now);
    }

    public void Publish(TrendyolGoPriceStockSnapshot snapshot)
    {
        var completedAtUtc = DateTime.UtcNow;
        states[snapshot.StoreId] = new SnapshotState(
            snapshot,
            SnapshotRefreshState.Ready,
            states.TryGetValue(snapshot.StoreId, out var current) ? current.RefreshStartedAtUtc : null,
            completedAtUtc,
            null);
    }

    public void MarkFailed(long storeId, string error)
    {
        var completedAtUtc = DateTime.UtcNow;
        states.AddOrUpdate(
            storeId,
            static (_, values) => SnapshotState.Empty with
            {
                RefreshState = SnapshotRefreshState.Failed,
                RefreshCompletedAtUtc = values.CompletedAtUtc,
                RefreshError = values.Error
            },
            static (_, current, values) => current with
            {
                RefreshState = SnapshotRefreshState.Failed,
                RefreshCompletedAtUtc = values.CompletedAtUtc,
                RefreshError = values.Error
            },
            (CompletedAtUtc: completedAtUtc, Error: error));
    }

    public void Complete(long storeId) => pendingStores.TryRemove(storeId, out _);

    public void Invalidate(long storeId)
    {
        states.TryRemove(storeId, out _);
        RequestRefresh(storeId);
    }

    public long[] GetTrackedStoreIds() => states.Keys.ToArray();

    internal sealed record SnapshotState(
        TrendyolGoPriceStockSnapshot? Snapshot,
        SnapshotRefreshState RefreshState,
        DateTime? RefreshStartedAtUtc,
        DateTime? RefreshCompletedAtUtc,
        string? RefreshError)
    {
        public static SnapshotState Empty { get; } = new(
            null, SnapshotRefreshState.None, null, null, null);
    }
}

internal enum SnapshotRefreshState
{
    None,
    Queued,
    Refreshing,
    Ready,
    Failed
}

internal sealed record TrendyolGoPriceStockSnapshot(
    long StoreId, int WarehouseNo, string StoreName,
    int TotalPages, long TotalElements, string PreviewHash,
    DateTime GeneratedAtUtc,
    IReadOnlyCollection<TrendyolGoPriceStockWorkbench.TrendyolProduct> Products,
    IReadOnlyCollection<TrendyolGoPriceStockRow> Rows)
{
    public int ReadyCount => Rows.Count(item => item.Status == "Ready");
    public int UnchangedCount => Rows.Count(item => item.Status == "Unchanged");
    public int SkippedCount => Rows.Count(item => item.Status == "Skipped");
}
