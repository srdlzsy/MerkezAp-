using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.Accept;

internal static class WarehouseReceivingAcceptanceMikroApiPayloadFactory
{
    internal static WarehouseReceivingAcceptanceMikroApiPayload Create(
        int warehouseNo,
        IReadOnlyCollection<STOK_HAREKETLERI> movements,
        IReadOnlyDictionary<Guid, double> receivedQuantitiesByMovementGuid)
    {
        var updateUser = ResolveMikroUserNo(warehouseNo);
        var satirlar = movements
            .OrderBy(movement => movement.sth_satirno ?? 0)
            .ThenBy(movement => movement.sth_stok_kod)
            .Select(movement =>
            {
                var transitWarehouseNo = movement.sth_giris_depo_no ?? 0;

                return new WarehouseReceivingAcceptanceMikroApiLine(
                    movement.sth_Guid,
                    receivedQuantitiesByMovementGuid[movement.sth_Guid],
                    warehouseNo,
                    transitWarehouseNo,
                    1,
                    updateUser,
                    DateTime.Now,
                    true);
            })
            .ToArray();

        return new WarehouseReceivingAcceptanceMikroApiPayload(
            [
                new WarehouseReceivingAcceptanceMikroApiDocument(satirlar)
            ]);
    }

    private static short ResolveMikroUserNo(int warehouseNo) =>
        warehouseNo is > 0 and <= short.MaxValue
            ? Convert.ToInt16(warehouseNo)
            : (short)39;
}

internal sealed record WarehouseReceivingAcceptanceMikroApiPayload(
    IReadOnlyCollection<WarehouseReceivingAcceptanceMikroApiDocument> evraklar);

internal sealed record WarehouseReceivingAcceptanceMikroApiDocument(
    IReadOnlyCollection<WarehouseReceivingAcceptanceMikroApiLine> satirlar);

internal sealed record WarehouseReceivingAcceptanceMikroApiLine(
    Guid sth_Guid,
    double sth_FormulMiktar,
    int sth_giris_depo_no,
    int sth_nakliyedeposu,
    byte sth_nakliyedurumu,
    short sth_lastup_user,
    DateTime sth_lastup_date,
    bool sth_degisti);
