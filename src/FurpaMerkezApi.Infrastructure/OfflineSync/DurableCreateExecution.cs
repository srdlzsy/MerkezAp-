namespace FurpaMerkezApi.Infrastructure.OfflineSync;

internal static class DurableCreateExecution
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    internal static CancellationTokenSource CreateTimeout() => new(Timeout);
}
