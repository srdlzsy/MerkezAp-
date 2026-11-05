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

        var movementTypeRows = await (
            from movement in mikroDbContext.STOK_HAREKETLERIs
                .AsNoTracking()
                .TagWith("Furpa:List:Virman")
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
            group movement by new
            {
                DocumentSerie = movement.sth_evrakno_seri ?? string.Empty,
                DocumentOrderNo = movement.sth_evrakno_sira ?? 0,
                WarehouseNo = movement.sth_cikis_depo_no ?? request.WarehouseNo ?? 0,
                WarehouseName = warehouse.dep_adi,
                DocumentType = movement.sth_evraktip ?? 0,
                MovementGenre = movement.sth_cins ?? 0,
                MovementType = movement.sth_tip ?? 0
            }
            into movementTypeGroup
            select new
            {
                movementTypeGroup.Key.DocumentSerie,
                movementTypeGroup.Key.DocumentOrderNo,
                movementTypeGroup.Key.WarehouseNo,
                WarehouseName = movementTypeGroup.Key.WarehouseName ?? string.Empty,
                movementTypeGroup.Key.DocumentType,
                movementTypeGroup.Key.MovementGenre,
                movementTypeGroup.Key.MovementType,
                DocumentDate = movementTypeGroup.Min(movement => movement.sth_belge_tarih),
                MovementCreateDate = movementTypeGroup.Min(movement => movement.sth_create_date),
                MovementDate = movementTypeGroup.Min(movement => movement.sth_tarih),
                DocumentNo = movementTypeGroup.Min(movement => movement.sth_belge_no) ?? string.Empty,
                Description = movementTypeGroup.Min(movement => movement.sth_aciklama) ?? string.Empty,
                LineCount = movementTypeGroup.Count(),
                TotalQuantity = movementTypeGroup.Sum(movement => movement.sth_miktar ?? 0d),
                TotalAmount = movementTypeGroup.Sum(movement => movement.sth_tutar ?? 0d)
            }).ToListAsync(cancellationToken);

        return movementTypeRows
            .GroupBy(row => new
            {
                row.DocumentSerie,
                row.DocumentOrderNo,
                row.WarehouseNo,
                row.WarehouseName,
                row.DocumentType,
                row.MovementGenre
            })
            .OrderBy(group => group.Min(row => row.DocumentDate))
            .ThenBy(group => group.Min(row => row.MovementCreateDate))
            .ThenBy(group => group.Key.DocumentSerie)
            .ThenBy(group => group.Key.DocumentOrderNo)
            .Select(group => new VirmanListItemDto(
                group.Min(row => row.DocumentDate),
                group.Min(row => row.MovementCreateDate),
                group.Min(row => row.MovementDate),
                group.OrderBy(row => row.MovementType).Select(row => row.DocumentNo).FirstOrDefault() ?? string.Empty,
                group.Key.DocumentSerie,
                group.Key.DocumentOrderNo,
                group.Key.WarehouseNo,
                group.Key.WarehouseName,
                group.Key.DocumentType,
                group.Key.MovementGenre,
                group.Select(row => row.MovementType).OrderBy(movementType => movementType).ToArray(),
                group.OrderBy(row => row.MovementType).Select(row => row.Description).FirstOrDefault() ?? string.Empty,
                group.Sum(row => row.LineCount),
                group.Where(row => row.MovementType == 0).Sum(row => row.LineCount),
                group.Where(row => row.MovementType == 1).Sum(row => row.LineCount),
                group.Where(row => row.MovementType == 0).Sum(row => row.TotalQuantity),
                group.Where(row => row.MovementType == 1).Sum(row => row.TotalQuantity),
                group.Sum(row => row.TotalQuantity),
                group.Sum(row => row.TotalAmount)))
            .ToArray();
    }
}
