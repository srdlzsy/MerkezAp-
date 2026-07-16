using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;

public sealed class WarehouseOrderListQueryExecutor(MikroDbContext mikroDbContext)
{
    internal async Task<IReadOnlyCollection<WarehouseOrderListItemDto>> ExecuteAsync(
        WarehouseOrderListRequest request,
        WarehouseOrderListDirection direction,
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
        var isIssued = direction == WarehouseOrderListDirection.Issued;

        var baseQuery = mikroDbContext.DEPOLAR_ARASI_SIPARISLERs
            .AsNoTracking()
            .Where(order =>
                order.ssip_tarih.HasValue &&
                order.ssip_tarih.Value >= startDate &&
                order.ssip_tarih.Value < endDateExclusive);

        if (request.WarehouseNo.HasValue)
        {
            baseQuery = isIssued
                ? baseQuery.Where(order => order.ssip_girdepo == request.WarehouseNo.Value)
                : baseQuery.Where(order => order.ssip_cikdepo == request.WarehouseNo.Value);
        }

        var query =
            from order in baseQuery
            join inWarehouse in mikroDbContext.DEPOLARs.AsNoTracking() on order.ssip_girdepo equals inWarehouse.dep_no into inWarehouseGroup
            from inWarehouse in inWarehouseGroup.DefaultIfEmpty()
            join outWarehouse in mikroDbContext.DEPOLARs.AsNoTracking() on order.ssip_cikdepo equals outWarehouse.dep_no into outWarehouseGroup
            from outWarehouse in outWarehouseGroup.DefaultIfEmpty()
            group new { order, inWarehouse, outWarehouse }
            by new
            {
                DocumentDate = order.ssip_tarih,
                DocumentSerie = order.ssip_evrakno_seri,
                DocumentOrderNo = order.ssip_evrakno_sira,
                DocumentNumber = order.ssip_belgeno,
                InWarehouseNo = order.ssip_girdepo,
                InWarehouseName = inWarehouse.dep_adi,
                OutWarehouseNo = order.ssip_cikdepo,
                OutWarehouseName = outWarehouse.dep_adi
            }
            into grouped
            orderby grouped.Key.DocumentOrderNo, grouped.Key.DocumentDate, grouped.Key.DocumentSerie
            select new
            {
                grouped.Key.DocumentDate,
                grouped.Key.DocumentSerie,
                grouped.Key.DocumentOrderNo,
                grouped.Key.DocumentNumber,
                grouped.Key.InWarehouseNo,
                grouped.Key.InWarehouseName,
                grouped.Key.OutWarehouseNo,
                grouped.Key.OutWarehouseName,
                LineCount = grouped.Count(),
                TotalQuantity = grouped.Sum(item => item.order.ssip_miktar ?? 0d),
                TotalAmount = grouped.Sum(item => item.order.ssip_tutar ?? 0d),
                DeliveryDate = grouped.Max(item => item.order.ssip_teslim_tarih)
            };

        var documents = await query.ToListAsync(cancellationToken);

        return documents
            .Select(document =>
            {
                var warehouseNo = isIssued ? (document.InWarehouseNo ?? 0) : (document.OutWarehouseNo ?? 0);
                var warehouseName = isIssued ? (document.InWarehouseName ?? string.Empty) : (document.OutWarehouseName ?? string.Empty);
                var relatedWarehouseNo = isIssued ? (document.OutWarehouseNo ?? 0) : (document.InWarehouseNo ?? 0);
                var relatedWarehouseName = isIssued ? (document.OutWarehouseName ?? string.Empty) : (document.InWarehouseName ?? string.Empty);
                var documentSerie = document.DocumentSerie ?? string.Empty;
                var documentOrderNo = document.DocumentOrderNo ?? 0;

                return new WarehouseOrderListItemDto(
                    WarehouseOrderDocumentKey.CreateOrNull(warehouseNo, documentSerie, documentOrderNo),
                    document.DocumentDate ?? DateTime.MinValue,
                    documentSerie,
                    documentOrderNo,
                    document.DocumentNumber ?? string.Empty,
                    warehouseNo,
                    warehouseName,
                    relatedWarehouseNo,
                    relatedWarehouseName,
                    document.InWarehouseNo ?? 0,
                    document.InWarehouseName ?? string.Empty,
                    document.OutWarehouseNo ?? 0,
                    document.OutWarehouseName ?? string.Empty,
                    document.LineCount,
                    document.TotalQuantity,
                    document.TotalAmount,
                    document.DeliveryDate);
            })
            .ToArray();
    }
}
