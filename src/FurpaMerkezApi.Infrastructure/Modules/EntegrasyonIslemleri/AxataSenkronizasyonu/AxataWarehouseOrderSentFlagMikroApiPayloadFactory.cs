namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal static class AxataWarehouseOrderSentFlagMikroApiPayloadFactory
{
    internal static AxataWarehouseOrderSentFlagMikroApiPayload Create(
        IReadOnlyCollection<AxataWarehouseOrderSentFlagSource> lines,
        string sentFlag) =>
        new(
            [
                new AxataWarehouseOrderSentFlagMikroApiDocument(
                    lines
                        .Select(line => new AxataWarehouseOrderSentFlagMikroApiLine(
                            line.LineGuid.ToString("D").ToUpperInvariant(),
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

internal sealed record AxataWarehouseOrderSentFlagSource(
    Guid LineGuid,
    DateTime? LastUpdateDate);
