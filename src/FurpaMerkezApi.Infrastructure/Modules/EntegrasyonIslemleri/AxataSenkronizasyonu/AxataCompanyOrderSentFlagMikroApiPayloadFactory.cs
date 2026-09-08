namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

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
    string sip_special1);

internal sealed record AxataCompanyOrderSentFlagSource(
    Guid LineGuid,
    DateTime? LastUpdateDate);
