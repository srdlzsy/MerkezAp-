using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar;

public sealed class VirmanListQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte VirmanDocumentType = 6;
    private const byte VirmanMovementGenre = 3;
    private const byte NormalMovement = 0;

    internal async Task<IReadOnlyCollection<VirmanListItemDto>> ExecuteAsync(
        VirmanListRequest request,
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

        var rows = await (
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            where movement.sth_belge_tarih.HasValue &&
                  movement.sth_belge_tarih.Value >= startDate &&
                  movement.sth_belge_tarih.Value < endDateExclusive &&
                  movement.sth_evraktip == VirmanDocumentType &&
                  movement.sth_normal_iade == NormalMovement &&
                  movement.sth_cins == VirmanMovementGenre &&
                  (!request.WarehouseNo.HasValue || movement.sth_cikis_depo_no == request.WarehouseNo.Value)
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals warehouse.dep_no into warehouseGroup
            from warehouse in warehouseGroup.DefaultIfEmpty()
            select new
            {
                movement.sth_belge_tarih,
                movement.sth_create_date,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                WarehouseName = warehouse.dep_adi,
                movement.sth_evraktip,
                movement.sth_cins,
                movement.sth_tip,
                movement.sth_aciklama,
                movement.sth_miktar,
                movement.sth_tutar
            }).ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => new
            {
                DocumentSerie = row.sth_evrakno_seri ?? string.Empty,
                DocumentOrderNo = row.sth_evrakno_sira ?? 0,
                WarehouseNo = row.sth_cikis_depo_no ?? request.WarehouseNo ?? 0,
                WarehouseName = row.WarehouseName ?? string.Empty,
                DocumentType = row.sth_evraktip ?? 0,
                MovementGenre = row.sth_cins ?? 0
            })
            .OrderBy(group => group.Min(row => row.sth_belge_tarih))
            .ThenBy(group => group.Min(row => row.sth_create_date))
            .ThenBy(group => group.Key.DocumentSerie)
            .ThenBy(group => group.Key.DocumentOrderNo)
            .Select(group => new VirmanListItemDto(
                group.Min(row => row.sth_belge_tarih),
                group.Min(row => row.sth_create_date),
                group.Min(row => row.sth_tarih),
                group.Select(row => row.sth_belge_no ?? string.Empty).FirstOrDefault() ?? string.Empty,
                group.Key.DocumentSerie,
                group.Key.DocumentOrderNo,
                group.Key.WarehouseNo,
                group.Key.WarehouseName,
                group.Key.DocumentType,
                group.Key.MovementGenre,
                group.Select(row => row.sth_tip ?? 0).Distinct().OrderBy(movementType => movementType).ToArray(),
                group.Select(row => row.sth_aciklama ?? string.Empty).FirstOrDefault() ?? string.Empty,
                group.Count(),
                group.Sum(row => row.sth_miktar ?? 0d),
                group.Sum(row => row.sth_tutar ?? 0d)))
            .ToArray();
    }
}
