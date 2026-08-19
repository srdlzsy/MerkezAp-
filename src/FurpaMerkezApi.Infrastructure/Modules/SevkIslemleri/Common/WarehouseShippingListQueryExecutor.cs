using System.Data;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

public sealed class WarehouseShippingListQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte InterWarehouseShippingDocumentType = 17;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const byte DeliveredToTargetWarehouseState = 1;

    internal async Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsync(
        WarehouseShippingListRequest request,
        WarehouseShippingDirection direction,
        bool isReturn,
        CancellationToken cancellationToken) =>
        await ExecuteAsyncCore(
            request,
            direction,
            isReturn ? ReturnMovement : NormalMovement,
            false,
            cancellationToken);

    internal async Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecutePendingIncomingAsync(
        WarehouseShippingListRequest request,
        CancellationToken cancellationToken) =>
        await ExecuteAsyncCore(
            request,
            WarehouseShippingDirection.Incoming,
            null,
            true,
            cancellationToken);

    private async Task<IReadOnlyCollection<WarehouseShippingListItemDto>> ExecuteAsyncCore(
        WarehouseShippingListRequest request,
        WarehouseShippingDirection direction,
        byte? returnType,
        bool onlyPending,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo is <= 0)
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
        var isOutgoing = direction == WarehouseShippingDirection.Outgoing;

        var query =
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            where movement.sth_tarih.HasValue &&
                  movement.sth_tarih.Value >= startDate &&
                  movement.sth_tarih.Value < endDateExclusive &&
                  movement.sth_evraktip == InterWarehouseShippingDocumentType &&
                  (!returnType.HasValue || movement.sth_normal_iade == returnType.Value) &&
                  (!onlyPending ||
                      movement.sth_nakliyedurumu != DeliveredToTargetWarehouseState &&
                      (!request.WarehouseNo.HasValue || movement.sth_nakliyedeposu == request.WarehouseNo.Value)) &&
                  (!request.WarehouseNo.HasValue ||
                      (isOutgoing
                          ? movement.sth_cikis_depo_no == request.WarehouseNo.Value
                          : movement.sth_nakliyedeposu == request.WarehouseNo.Value ||
                            movement.sth_giris_depo_no == request.WarehouseNo.Value))
            join sourceWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals sourceWarehouse.dep_no into sourceWarehouseGroup
            from sourceWarehouse in sourceWarehouseGroup.DefaultIfEmpty()
            let resolvedTargetWarehouseNo = movement.sth_nakliyedurumu == DeliveredToTargetWarehouseState
                ? movement.sth_giris_depo_no
                : movement.sth_nakliyedeposu
            let resolvedShippingWarehouseNo = movement.sth_nakliyedurumu == DeliveredToTargetWarehouseState
                ? movement.sth_nakliyedeposu
                : movement.sth_giris_depo_no
            join targetWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on resolvedTargetWarehouseNo equals targetWarehouse.dep_no into targetWarehouseGroup
            from targetWarehouse in targetWarehouseGroup.DefaultIfEmpty()
            group movement
            by new
            {
                movement.sth_belge_no,
                movement.sth_belge_tarih,
                movement.sth_tarih,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                SourceWarehouseName = sourceWarehouse.dep_adi,
                movement.sth_giris_depo_no,
                ResolvedTargetWarehouseNo = resolvedTargetWarehouseNo,
                TargetWarehouseName = targetWarehouse.dep_adi,
                movement.sth_nakliyedeposu,
                ResolvedShippingWarehouseNo = resolvedShippingWarehouseNo,
                movement.sth_nakliyedurumu,
                movement.sth_normal_iade,
                movement.sth_HareketGrupKodu1,
                movement.sth_HareketGrupKodu3,
                movement.sth_ismerkezi_kodu,
                movement.sth_aciklama
            }
            into grouped
            orderby grouped.Key.sth_tarih, grouped.Key.sth_evrakno_seri, grouped.Key.sth_evrakno_sira
            select new
            {
                grouped.Key.sth_belge_no,
                grouped.Key.sth_belge_tarih,
                grouped.Key.sth_tarih,
                grouped.Key.sth_evrakno_seri,
                grouped.Key.sth_evrakno_sira,
                grouped.Key.sth_cikis_depo_no,
                grouped.Key.SourceWarehouseName,
                grouped.Key.sth_giris_depo_no,
                grouped.Key.ResolvedTargetWarehouseNo,
                grouped.Key.TargetWarehouseName,
                grouped.Key.sth_nakliyedeposu,
                grouped.Key.ResolvedShippingWarehouseNo,
                grouped.Key.sth_nakliyedurumu,
                grouped.Key.sth_normal_iade,
                grouped.Key.sth_HareketGrupKodu1,
                grouped.Key.sth_HareketGrupKodu3,
                grouped.Key.sth_ismerkezi_kodu,
                grouped.Key.sth_aciklama,
                LineCount = grouped.Count(),
                TotalQuantity = grouped.Sum(movement => movement.sth_miktar ?? 0d)
            };

        var shipments = await ExecuteReadOnlyListAsync(query, cancellationToken);

        return shipments
            .Select(shipment => new WarehouseShippingListItemDto(
                shipment.sth_belge_tarih,
                shipment.sth_tarih,
                shipment.sth_belge_no ?? string.Empty,
                shipment.sth_evrakno_seri ?? string.Empty,
                shipment.sth_evrakno_sira ?? 0,
                shipment.sth_cikis_depo_no ?? request.WarehouseNo ?? 0,
                shipment.SourceWarehouseName ?? string.Empty,
                shipment.ResolvedTargetWarehouseNo ?? 0,
                shipment.TargetWarehouseName ?? string.Empty,
                shipment.ResolvedShippingWarehouseNo ?? 0,
                shipment.sth_nakliyedurumu ?? 0,
                shipment.sth_normal_iade == ReturnMovement,
                shipment.sth_HareketGrupKodu1 ?? string.Empty,
                shipment.sth_HareketGrupKodu3 ?? string.Empty,
                shipment.sth_ismerkezi_kodu ?? string.Empty,
                shipment.sth_aciklama ?? string.Empty,
                string.Empty,
                shipment.LineCount,
                shipment.TotalQuantity))
            .ToArray();
    }

    private async Task<List<T>> ExecuteReadOnlyListAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken)
    {
        if (!IsSqlServerProvider())
        {
            return await query.ToListAsync(cancellationToken);
        }

        var executionStrategy = mikroDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var readTransaction = await mikroDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadUncommitted,
                cancellationToken);
            var result = await query.ToListAsync(cancellationToken);
            await readTransaction.CommitAsync(cancellationToken);

            return result;
        });
    }

    private bool IsSqlServerProvider() =>
        mikroDbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
}
