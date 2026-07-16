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

        var query =
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            where movement.sth_belge_tarih.HasValue &&
                  movement.sth_belge_tarih.Value >= startDate &&
                  movement.sth_belge_tarih.Value < endDateExclusive &&
                  movement.sth_evraktip == StockReceiptDocumentType &&
                  movement.sth_tip == OutgoingMovementType &&
                  movement.sth_normal_iade == NormalMovement &&
                  movement.sth_cins == movementGenre &&
                  (!request.WarehouseNo.HasValue || movement.sth_cikis_depo_no == request.WarehouseNo.Value)
            join outputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals outputWarehouse.dep_no into outputWarehouseGroup
            from outputWarehouse in outputWarehouseGroup.DefaultIfEmpty()
            group movement
            by new
            {
                movement.sth_belge_tarih,
                movement.sth_create_date,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                OutputWarehouseName = outputWarehouse.dep_adi,
                movement.sth_HareketGrupKodu1,
                movement.sth_HareketGrupKodu2,
                movement.sth_isemri_gider_kodu,
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_cins,
                movement.sth_aciklama
            }
            into grouped
            orderby grouped.Key.sth_belge_tarih, grouped.Key.sth_create_date, grouped.Key.sth_evrakno_seri, grouped.Key.sth_evrakno_sira
            select new StockReceiptListItemDto(
                grouped.Key.sth_belge_tarih,
                grouped.Key.sth_create_date,
                grouped.Key.sth_tarih,
                grouped.Key.sth_belge_no ?? string.Empty,
                grouped.Key.sth_evrakno_seri ?? string.Empty,
                grouped.Key.sth_evrakno_sira ?? 0,
                grouped.Key.sth_cikis_depo_no ?? request.WarehouseNo ?? 0,
                grouped.Key.OutputWarehouseName ?? string.Empty,
                grouped.Key.sth_HareketGrupKodu1 ?? string.Empty,
                grouped.Key.sth_HareketGrupKodu2 ?? string.Empty,
                grouped.Key.sth_isemri_gider_kodu ?? string.Empty,
                grouped.Key.sth_evraktip ?? 0,
                grouped.Key.sth_tip ?? 0,
                grouped.Key.sth_cins ?? 0,
                grouped.Key.sth_aciklama ?? string.Empty,
                grouped.Count(),
                grouped.Sum(item => item.sth_miktar ?? 0d),
                grouped.Sum(item => item.sth_tutar ?? 0d));

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
