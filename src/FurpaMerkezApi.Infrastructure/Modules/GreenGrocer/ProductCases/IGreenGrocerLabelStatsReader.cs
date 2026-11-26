namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;

public sealed record GreenGrocerLabelObservation(
    DateTime CreatedAt,
    double NetKg,
    double CaseCount);

public interface IGreenGrocerLabelStatsReader
{
    Task<IReadOnlyList<GreenGrocerLabelObservation>> ReadObservationsAsync(
        string stockCode,
        DateTime orderDate,
        int windowDays,
        CancellationToken cancellationToken);
}
