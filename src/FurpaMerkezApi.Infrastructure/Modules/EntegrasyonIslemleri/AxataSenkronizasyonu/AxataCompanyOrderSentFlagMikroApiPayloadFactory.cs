namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

using System.Globalization;

internal static class AxataCompanyOrderSentFlagMikroApiPayloadFactory
{
    internal static AxataCompanyOrderSentFlagMikroApiPayload Create(
        IReadOnlyCollection<AxataCompanyOrderSentFlagSource> lines,
        string sentFlag) =>
        new(
            [
                new AxataCompanyOrderSentFlagMikroApiDocument(
                    lines
                        .Select(line => new AxataCompanyOrderSentFlagMikroApiLine(
                            line.LineGuid.ToString("D").ToUpperInvariant(),
                            line.LastUpdateDate?.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture),
                            sentFlag))
                        .ToArray())
            ]);
}

internal sealed record AxataCompanyOrderSentFlagMikroApiPayload(
    IReadOnlyCollection<AxataCompanyOrderSentFlagMikroApiDocument> evraklar);

internal sealed record AxataCompanyOrderSentFlagMikroApiDocument(
    IReadOnlyCollection<AxataCompanyOrderSentFlagMikroApiLine> satirlar);

internal sealed record AxataCompanyOrderSentFlagMikroApiLine(
    string sip_Guid,
    string? sip_lastup_date,
    string sip_special1);

internal sealed record AxataCompanyOrderSentFlagSource(
    Guid LineGuid,
    DateTime? LastUpdateDate);
