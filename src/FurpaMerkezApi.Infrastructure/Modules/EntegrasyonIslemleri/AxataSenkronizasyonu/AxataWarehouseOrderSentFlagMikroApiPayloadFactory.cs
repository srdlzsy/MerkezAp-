namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal static class AxataWarehouseOrderSentFlagMikroApiPayloadFactory
{
    internal static AxataWarehouseOrderSentFlagMikroApiPayload Create(
        IReadOnlyCollection<Guid> lineGuids,
        string sentFlag) =>
        new(
            [
                new AxataWarehouseOrderSentFlagMikroApiDocument(
                    lineGuids
                        .Select(lineGuid => new AxataWarehouseOrderSentFlagMikroApiLine(
                            lineGuid.ToString("D").ToUpperInvariant(),
                            sentFlag))
                        .ToArray())
            ]);
}

internal sealed record AxataWarehouseOrderSentFlagMikroApiPayload(
    IReadOnlyCollection<AxataWarehouseOrderSentFlagMikroApiDocument> evraklar);

internal sealed record AxataWarehouseOrderSentFlagMikroApiDocument(
    IReadOnlyCollection<AxataWarehouseOrderSentFlagMikroApiLine> satirlar);

internal sealed record AxataWarehouseOrderSentFlagMikroApiLine(
    string ssip_Guid,
    string ssip_special1);
