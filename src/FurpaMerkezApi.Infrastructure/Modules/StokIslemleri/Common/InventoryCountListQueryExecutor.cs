using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;

public sealed class InventoryCountListQueryExecutor(MikroDbContext mikroDbContext)
{
    internal async Task<IReadOnlyCollection<InventoryCountListItemDto>> ExecuteAsync(
        InventoryCountListRequest request,
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

        var query =
            from result in mikroDbContext.SAYIM_SONUCLARIs.AsNoTracking()
            where result.sym_tarihi.HasValue &&
                  result.sym_tarihi.Value >= startDate &&
                  result.sym_tarihi.Value < endDateExclusive &&
                  (!request.WarehouseNo.HasValue || result.sym_depono == request.WarehouseNo.Value)
            join warehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on result.sym_depono equals warehouse.dep_no into warehouseGroup
            from warehouse in warehouseGroup.DefaultIfEmpty()
            group result
            by new
            {
                result.sym_tarihi,
                result.sym_evrakno,
                result.sym_depono,
                WarehouseName = warehouse.dep_adi
            }
            into grouped
            orderby grouped.Key.sym_tarihi, grouped.Key.sym_evrakno
            select new InventoryCountListItemDto(
                grouped.Key.sym_tarihi,
                grouped.Min(item => item.sym_create_date),
                grouped.Key.sym_evrakno ?? 0,
                grouped.Key.sym_depono ?? request.WarehouseNo ?? 0,
                grouped.Key.WarehouseName ?? string.Empty,
                grouped
                    .Select(item => item.sym_parti_kodu ?? string.Empty)
                    .Where(name => name != string.Empty)
                    .OrderBy(name => name)
                    .FirstOrDefault() ?? string.Empty,
                grouped.Count(),
                grouped.Sum(item => item.sym_miktar1 ?? 0d));

        return await query.ToListAsync(cancellationToken);
    }
}
