namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal static class AxataCompanyOrderSentFlagMikroApiPayloadFactory
{
    internal static AxataCompanyOrderSentFlagMikroApiPayload Create(
        IReadOnlyCollection<Guid> lineGuids,
        string sentFlag) =>
        new(
            [
                new AxataCompanyOrderSentFlagMikroApiDocument(
                    lineGuids
                        .Select(lineGuid => new AxataCompanyOrderSentFlagMikroApiLine(
                            lineGuid.ToString("D").ToUpperInvariant(),
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
