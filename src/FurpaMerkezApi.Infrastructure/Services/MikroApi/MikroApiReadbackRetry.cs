namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal static class MikroApiReadbackRetry
{
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
        TimeSpan.FromSeconds(15)
    ];

    internal static IReadOnlyList<TimeSpan> GetDelays(bool uncertainWriteOutcome) =>
        uncertainWriteOutcome ? UncertainWriteDelays : QuickDelays;
}
