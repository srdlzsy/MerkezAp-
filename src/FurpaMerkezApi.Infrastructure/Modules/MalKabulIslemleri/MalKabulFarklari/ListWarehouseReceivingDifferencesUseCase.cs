using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabulFarklari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabulFarklari;

public sealed class ListWarehouseReceivingDifferencesUseCase(MikroDbContext mikroDbContext)
    : IListWarehouseReceivingDifferencesUseCase
{
    private const byte InterWarehouseShippingDocumentType = 17;
    private const byte DeliveredToTargetWarehouseState = 1;
    private const byte ReturnMovement = 1;
    private const double QuantityTolerance = 0.000001d;

    public async Task<IReadOnlyCollection<WarehouseReceivingDifferenceDto>> ExecuteAsync(
        WarehouseReceivingDifferenceListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.");
        }

        var endDateExclusive = endDate.AddDays(1);
        var showCreatedDocuments = request.Scope == WarehouseReceivingDifferenceScope.CreatedByWarehouse;

        var rows = await (
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            let quantity = movement.sth_miktar ?? 0d
            let receivedQuantity = movement.sth_FormulMiktar ?? 0d
            where movement.sth_tarih.HasValue &&
                  movement.sth_tarih.Value >= startDate &&
                  movement.sth_tarih.Value < endDateExclusive &&
                  movement.sth_iptal != true &&
                  movement.sth_evraktip == InterWarehouseShippingDocumentType &&
                  movement.sth_nakliyedurumu == DeliveredToTargetWarehouseState &&
                  movement.sth_FormulMiktar.HasValue &&
                  (receivedQuantity > quantity + QuantityTolerance ||
                   receivedQuantity < quantity - QuantityTolerance) &&
                  (showCreatedDocuments
                      ? movement.sth_cikis_depo_no == request.WarehouseNo
                      : movement.sth_giris_depo_no == request.WarehouseNo)
            join sourceWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals sourceWarehouse.dep_no into sourceWarehouseGroup
            from sourceWarehouse in sourceWarehouseGroup.DefaultIfEmpty()
            join targetWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_giris_depo_no equals targetWarehouse.dep_no into targetWarehouseGroup
            from targetWarehouse in targetWarehouseGroup.DefaultIfEmpty()
            join stock in mikroDbContext.STOKLARs.AsNoTracking()
                on movement.sth_stok_kod equals stock.sto_kod into stockGroup
            from stock in stockGroup.DefaultIfEmpty()
            orderby movement.sth_tarih descending, movement.sth_evrakno_seri, movement.sth_evrakno_sira, movement.sth_satirno
            select new
            {
                movement.sth_belge_tarih,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_satirno,
                movement.sth_Guid,
                movement.sth_normal_iade,
                movement.sth_cikis_depo_no,
                SourceWarehouseName = sourceWarehouse.dep_adi,
                movement.sth_giris_depo_no,
                TargetWarehouseName = targetWarehouse.dep_adi,
                movement.sth_stok_kod,
                StockName = stock.sto_isim,
                stock.sto_birim1_ad,
                stock.sto_birim2_ad,
                stock.sto_birim3_ad,
                stock.sto_birim4_ad,
                movement.sth_birim_pntr,
                Quantity = quantity,
                ReceivedQuantity = receivedQuantity,
                movement.sth_aciklama
            }).ToListAsync(cancellationToken);

        return rows
            .Select(row =>
            {
                var unitPointer = NormalizeUnitPointer(row.sth_birim_pntr);
                var differenceQuantity = row.ReceivedQuantity - row.Quantity;

                return new WarehouseReceivingDifferenceDto(
                    row.sth_belge_tarih,
                    row.sth_tarih,
                    row.sth_belge_no ?? string.Empty,
                    row.sth_evrakno_seri ?? string.Empty,
                    row.sth_evrakno_sira ?? 0,
                    row.sth_satirno ?? 0,
                    row.sth_Guid,
                    row.sth_normal_iade == ReturnMovement,
                    row.sth_cikis_depo_no ?? 0,
                    row.SourceWarehouseName ?? string.Empty,
                    row.sth_giris_depo_no ?? 0,
                    row.TargetWarehouseName ?? string.Empty,
                    row.sth_stok_kod ?? string.Empty,
                    row.StockName ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
                    row.Quantity,
                    row.ReceivedQuantity,
                    differenceQuantity,
                    ResolveDifferenceType(differenceQuantity),
                    row.sth_aciklama ?? string.Empty);
            })
            .ToArray();
    }

    private static byte NormalizeUnitPointer(byte? unitPointer) =>
        unitPointer is >= 1 and <= 4 ? unitPointer.Value : (byte)1;

    private static string ResolveUnitName(
        byte unitPointer,
        string? unit1Name,
        string? unit2Name,
        string? unit3Name,
        string? unit4Name) =>
        unitPointer switch
        {
            2 => unit2Name ?? unit1Name ?? string.Empty,
            3 => unit3Name ?? unit1Name ?? string.Empty,
            4 => unit4Name ?? unit1Name ?? string.Empty,
            _ => unit1Name ?? string.Empty
        };

    private static string ResolveDifferenceType(double differenceQuantity) =>
        differenceQuantity > QuantityTolerance
            ? "excess"
            : differenceQuantity < -QuantityTolerance
                ? "missing"
                : "none";
}
