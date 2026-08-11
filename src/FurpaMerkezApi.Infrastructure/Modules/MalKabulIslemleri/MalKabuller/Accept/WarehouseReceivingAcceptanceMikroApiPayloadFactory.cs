using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.Accept;

internal static class WarehouseReceivingAcceptanceMikroApiPayloadFactory
{
    internal static WarehouseReceivingAcceptanceMikroApiPayload Create(
        int warehouseNo,
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        IReadOnlyDictionary<Guid, double> receivedQuantitiesByMovementGuid)
    {
        var satirlar = movements
            .OrderBy(movement => movement.sth_satirno ?? 0)
            .ThenBy(movement => movement.sth_stok_kod)
            .Select(movement =>
            {
                var transitWarehouseNo = movement.sth_giris_depo_no ?? 0;

                return new WarehouseReceivingAcceptanceMikroApiLine(
                    movement.sth_Guid.ToString("D").ToUpperInvariant(),
                    receivedQuantitiesByMovementGuid[movement.sth_Guid],
                    warehouseNo,
                    transitWarehouseNo,
                    1);
            })
            .ToArray();

        return new WarehouseReceivingAcceptanceMikroApiPayload(
            [
                new WarehouseReceivingAcceptanceMikroApiDocument(satirlar)
            ]);
    }
}

internal sealed record WarehouseReceivingAcceptanceMikroApiPayload(
    IReadOnlyCollection<WarehouseReceivingAcceptanceMikroApiDocument> evraklar);

internal sealed record WarehouseReceivingAcceptanceMikroApiDocument(
    IReadOnlyCollection<WarehouseReceivingAcceptanceMikroApiLine> satirlar);

internal sealed record WarehouseReceivingAcceptanceMikroApiLine(
    string sth_Guid,
    double sth_FormulMiktar,
    int sth_giris_depo_no,
    int sth_nakliyedeposu,
    byte sth_nakliyedurumu);
