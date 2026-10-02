using FurpaMerkezApi.Application.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

public sealed class StockReceiptListQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte StockReceiptDocumentType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte NormalMovement = 0;
    private const byte OutageMovementGenre = 4;
    private const byte ExpenseMovementGenre = 5;

    internal async Task<IReadOnlyCollection<StockReceiptListItemDto>> ExecuteAsync(
        StockReceiptListRequest request,
        StockReceiptKind kind,
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
        var movementGenre = ResolveMovementGenre(kind);

        var filteredMovements =
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            where movement.sth_belge_tarih.HasValue &&
                  movement.sth_belge_tarih.Value >= startDate &&
                  movement.sth_belge_tarih.Value < endDateExclusive &&
                  movement.sth_evraktip == StockReceiptDocumentType &&
                  movement.sth_tip == OutgoingMovementType &&
                  movement.sth_normal_iade == NormalMovement &&
                  movement.sth_cins == movementGenre &&
                  (!request.WarehouseNo.HasValue || movement.sth_cikis_depo_no == request.WarehouseNo.Value)
            select movement;

        var documentSummaries =
            from movement in filteredMovements
            group movement
            by new
            {
                movement.sth_belge_tarih,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                movement.sth_HareketGrupKodu1,
                movement.sth_HareketGrupKodu2,
                movement.sth_isemri_gider_kodu,
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_cins
            }
            into grouped
            select new
            {
                grouped.Key.sth_belge_tarih,
                MovementCreateDate = grouped.Min(item => item.sth_create_date),
                grouped.Key.sth_tarih,
                grouped.Key.sth_belge_no,
                grouped.Key.sth_evrakno_seri,
                grouped.Key.sth_evrakno_sira,
                grouped.Key.sth_cikis_depo_no,
                grouped.Key.sth_HareketGrupKodu1,
                grouped.Key.sth_HareketGrupKodu2,
                grouped.Key.sth_isemri_gider_kodu,
                grouped.Key.sth_evraktip,
                grouped.Key.sth_tip,
                grouped.Key.sth_cins,
                LineCount = grouped.Count(),
                TotalQuantity = grouped.Sum(item => item.sth_miktar ?? 0d),
                TotalAmount = grouped.Sum(item => item.sth_tutar ?? 0d)
            };

        var query =
            from document in documentSummaries
            join outputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on document.sth_cikis_depo_no equals outputWarehouse.dep_no into outputWarehouseGroup
            from outputWarehouse in outputWarehouseGroup.DefaultIfEmpty()
            orderby document.sth_belge_tarih,
                document.MovementCreateDate,
                document.sth_evrakno_seri,
                document.sth_evrakno_sira
            select new StockReceiptListItemDto(
                document.sth_belge_tarih,
                document.MovementCreateDate,
                document.sth_tarih,
                document.sth_belge_no ?? string.Empty,
                document.sth_evrakno_seri ?? string.Empty,
                document.sth_evrakno_sira ?? 0,
                document.sth_cikis_depo_no ?? request.WarehouseNo ?? 0,
                outputWarehouse.dep_adi ?? string.Empty,
                document.sth_HareketGrupKodu1 ?? string.Empty,
                document.sth_HareketGrupKodu2 ?? string.Empty,
                document.sth_isemri_gider_kodu ?? string.Empty,
                document.sth_evraktip ?? 0,
                document.sth_tip ?? 0,
                document.sth_cins ?? 0,
                string.Empty,
                document.LineCount,
                document.TotalQuantity,
                document.TotalAmount);

        return await query.ToListAsync(cancellationToken);
    }

    private static byte ResolveMovementGenre(StockReceiptKind kind) =>
        kind switch
        {
            StockReceiptKind.OutageReceipt => OutageMovementGenre,
            StockReceiptKind.ExpenseReceipt => ExpenseMovementGenre,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported stock receipt kind.")
        };
}
