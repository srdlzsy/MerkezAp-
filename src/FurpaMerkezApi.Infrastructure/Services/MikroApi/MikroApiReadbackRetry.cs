namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal static class MikroApiReadbackRetry
{
    internal static readonly TimeSpan UncertainWriteTimeout = TimeSpan.FromSeconds(180);

    private static readonly TimeSpan QuickReadbackTimeout = TimeSpan.FromSeconds(10);

    private static readonly IReadOnlyList<TimeSpan> QuickDelays =
    [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(750),
        TimeSpan.FromMilliseconds(1000)
    ];

    private static readonly IReadOnlyList<TimeSpan> UncertainWriteDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(15)
    ];

    internal static IReadOnlyList<TimeSpan> GetDelays(bool uncertainWriteOutcome) =>
        uncertainWriteOutcome ? UncertainWriteDelays : QuickDelays;

    internal static async Task<T?> ExecuteAsync<T>(
        bool uncertainWriteOutcome,
        Func<CancellationToken, Task<T?>> readAsync)
    {
        var delays = GetDelays(uncertainWriteOutcome);
        using var timeout = new CancellationTokenSource(
            uncertainWriteOutcome ? UncertainWriteTimeout : QuickReadbackTimeout);

        try
        {
            for (var attempt = 0; attempt <= delays.Count; attempt++)
            {
                var result = await readAsync(timeout.Token);
                if (result is not null)
                {
                    return result;
                }

                if (attempt < delays.Count)
                {
                    await Task.Delay(delays[attempt], timeout.Token);
                }
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            // An uncertain write stays retryable. The caller persists the result as
            // unconfirmed and the same clientRequestId can continue reconciliation.
        }

        return default;
    }
}
