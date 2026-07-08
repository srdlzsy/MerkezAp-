using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;

public sealed class CompanyMovementListQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte CompanyDispatchDocumentType = 1;
    private const byte ReceivingReceiptDocumentType = 13;
    private const byte IncomingMovementType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;

    internal async Task<IReadOnlyCollection<CompanyMovementListItemDto>> ExecuteAsync(
        CompanyMovementListRequest request,
        CompanyMovementKind kind,
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
        var movements = CreateFilteredMovementQuery(request.WarehouseNo, startDate, endDateExclusive, kind);

        var query =
            from movement in movements
            join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking()
                on movement.sth_cari_kodu equals customer.cari_kod into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join inputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_giris_depo_no equals inputWarehouse.dep_no into inputWarehouseGroup
            from inputWarehouse in inputWarehouseGroup.DefaultIfEmpty()
            join outputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals outputWarehouse.dep_no into outputWarehouseGroup
            from outputWarehouse in outputWarehouseGroup.DefaultIfEmpty()
            group movement
            by new
            {
                movement.sth_belge_tarih,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cari_kodu,
                CustomerName = customer.cari_unvan1,
                CustomerTitle = customer.cari_unvan2,
                movement.sth_giris_depo_no,
                InputWarehouseName = inputWarehouse.dep_adi,
                movement.sth_cikis_depo_no,
                OutputWarehouseName = outputWarehouse.dep_adi,
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_normal_iade,
                movement.sth_aciklama
            }
            into grouped
            orderby grouped.Key.sth_belge_tarih,
                grouped.Min(item => item.sth_create_date),
                grouped.Key.sth_evrakno_seri,
                grouped.Key.sth_evrakno_sira
            select new
            {
                grouped.Key.sth_belge_tarih,
                MovementCreateDate = grouped.Min(item => item.sth_create_date),
                grouped.Key.sth_tarih,
                grouped.Key.sth_belge_no,
                grouped.Key.sth_evrakno_seri,
                grouped.Key.sth_evrakno_sira,
                grouped.Key.sth_cari_kodu,
                grouped.Key.CustomerName,
                grouped.Key.CustomerTitle,
                grouped.Key.sth_giris_depo_no,
                grouped.Key.InputWarehouseName,
                grouped.Key.sth_cikis_depo_no,
                grouped.Key.OutputWarehouseName,
                grouped.Key.sth_evraktip,
                grouped.Key.sth_tip,
                grouped.Key.sth_normal_iade,
                grouped.Key.sth_aciklama,
                LineCount = grouped.Count(),
                TotalQuantity = grouped.Sum(item => item.sth_miktar ?? 0d),
                TotalAmount = grouped.Sum(item => item.sth_tutar ?? 0d)
            };

        var documents = await query.ToListAsync(cancellationToken);

        return documents
            .Select(document =>
            {
                var warehouseNo = kind == CompanyMovementKind.IncomingShipment
                    ? document.sth_giris_depo_no ?? request.WarehouseNo
                    : document.sth_cikis_depo_no ?? request.WarehouseNo;
                var warehouseName = kind == CompanyMovementKind.IncomingShipment
                    ? document.InputWarehouseName ?? string.Empty
                    : document.OutputWarehouseName ?? string.Empty;

                return new CompanyMovementListItemDto(
                    document.sth_belge_tarih,
                    document.MovementCreateDate,
                    document.sth_tarih,
                    document.sth_belge_no ?? string.Empty,
                    document.sth_evrakno_seri ?? string.Empty,
                    document.sth_evrakno_sira ?? 0,
                    document.sth_cari_kodu ?? string.Empty,
                    document.CustomerName ?? string.Empty,
                    document.CustomerTitle ?? string.Empty,
                    JoinNonEmpty(document.CustomerName, document.CustomerTitle),
                    warehouseNo,
                    warehouseName,
                    document.sth_giris_depo_no ?? 0,
                    document.InputWarehouseName ?? string.Empty,
                    document.sth_cikis_depo_no ?? 0,
                    document.OutputWarehouseName ?? string.Empty,
                    document.sth_evraktip ?? 0,
                    document.sth_tip ?? 0,
                    document.sth_normal_iade ?? 0,
                    document.sth_aciklama ?? string.Empty,
                    document.LineCount,
                    document.TotalQuantity,
                    document.TotalAmount);
            })
            .ToArray();
    }

    private IQueryable<STOK_HAREKETLERI> CreateFilteredMovementQuery(
        int warehouseNo,
        DateTime startDate,
        DateTime endDateExclusive,
        CompanyMovementKind kind)
    {
        var movements = mikroDbContext.STOK_HAREKETLERIs.AsNoTracking();

        return kind switch
        {
            CompanyMovementKind.OutgoingShipment => movements.Where(movement =>
                movement.sth_belge_tarih.HasValue &&
                movement.sth_belge_tarih.Value >= startDate &&
                movement.sth_belge_tarih.Value < endDateExclusive &&
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            CompanyMovementKind.IncomingShipment => movements.Where(movement =>
                movement.sth_create_date >= startDate &&
                movement.sth_create_date < endDateExclusive &&
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                movement.sth_giris_depo_no == warehouseNo),

            CompanyMovementKind.PurchaseReturn => movements.Where(movement =>
                movement.sth_belge_tarih.HasValue &&
                movement.sth_belge_tarih.Value >= startDate &&
                movement.sth_belge_tarih.Value < endDateExclusive &&
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                movement.sth_cikis_depo_no == warehouseNo),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown company movement kind.")
        };
    }

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
}
