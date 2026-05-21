using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

public sealed class WarehouseShippingDetailQueryExecutor(MikroDbContext mikroDbContext)
{
    private const byte InterWarehouseShippingDocumentType = 17;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const byte DeliveredToTargetWarehouseState = 1;

    internal async Task<WarehouseShippingDetailDto> ExecuteAsync(
        WarehouseShippingDetailRequest request,
        WarehouseShippingDirection direction,
        bool isReturn,
        CancellationToken cancellationToken) =>
        await ExecuteAsyncCore(
            request,
            direction,
            isReturn ? ReturnMovement : NormalMovement,
            false,
            cancellationToken);

    internal async Task<WarehouseShippingDetailDto> ExecutePendingIncomingAsync(
        WarehouseShippingDetailRequest request,
        CancellationToken cancellationToken) =>
        await ExecuteAsyncCore(
            request,
            WarehouseShippingDirection.Incoming,
            null,
            true,
            cancellationToken);

    private async Task<WarehouseShippingDetailDto> ExecuteAsyncCore(
        WarehouseShippingDetailRequest request,
        WarehouseShippingDirection direction,
        byte? returnType,
        bool requirePending,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        var documentSerie = request.DocumentSerie.Trim();
        var isOutgoing = direction == WarehouseShippingDirection.Outgoing;

        var rows = await (
            from movement in mikroDbContext.STOK_HAREKETLERIs.AsNoTracking()
            where movement.sth_evraktip == InterWarehouseShippingDocumentType &&
                  (!returnType.HasValue || movement.sth_normal_iade == returnType.Value) &&
                  movement.sth_evrakno_seri == documentSerie &&
                  movement.sth_evrakno_sira == request.DocumentOrderNo &&
                  (!requirePending ||
                      movement.sth_nakliyedurumu != DeliveredToTargetWarehouseState &&
                      movement.sth_nakliyedeposu == request.WarehouseNo) &&
                  (isOutgoing
                      ? movement.sth_cikis_depo_no == request.WarehouseNo
                      : movement.sth_nakliyedeposu == request.WarehouseNo || movement.sth_giris_depo_no == request.WarehouseNo)
            join movementExtra in mikroDbContext.STOK_HAREKETLERI_EKs.AsNoTracking()
                on movement.sth_Guid equals movementExtra.sthek_related_uid into movementExtraGroup
            from movementExtra in movementExtraGroup.DefaultIfEmpty()
            join warehouseOrder in mikroDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking()
                on movementExtra.sth_subesip_uid equals warehouseOrder.ssip_Guid into warehouseOrderGroup
            from warehouseOrder in warehouseOrderGroup.DefaultIfEmpty()
            join sourceWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on movement.sth_cikis_depo_no equals sourceWarehouse.dep_no into sourceWarehouseGroup
            from sourceWarehouse in sourceWarehouseGroup.DefaultIfEmpty()
            let resolvedTargetWarehouseNo = movement.sth_nakliyedurumu == DeliveredToTargetWarehouseState
                ? movement.sth_giris_depo_no
                : movement.sth_nakliyedeposu
            join targetWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on resolvedTargetWarehouseNo equals targetWarehouse.dep_no into targetWarehouseGroup
            from targetWarehouse in targetWarehouseGroup.DefaultIfEmpty()
            join stock in mikroDbContext.STOKLARs.AsNoTracking()
                on movement.sth_stok_kod equals stock.sto_kod into stockGroup
            from stock in stockGroup.DefaultIfEmpty()
            orderby movement.sth_satirno, movement.sth_stok_kod
            select new
            {
                movement.sth_belge_no,
                movement.sth_belge_tarih,
                movement.sth_tarih,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cikis_depo_no,
                SourceWarehouseName = sourceWarehouse.dep_adi,
                movement.sth_giris_depo_no,
                TargetWarehouseName = targetWarehouse.dep_adi,
                movement.sth_nakliyedeposu,
                movement.sth_nakliyedurumu,
                movement.sth_normal_iade,
                movement.sth_HareketGrupKodu1,
                movement.sth_HareketGrupKodu3,
                movement.sth_ismerkezi_kodu,
                movement.sth_aciklama,
                movement.sth_Guid,
                WarehouseOrderSerie = warehouseOrder.ssip_evrakno_seri,
                WarehouseOrderNo = warehouseOrder.ssip_evrakno_sira,
                movement.sth_satirno,
                movement.sth_stok_kod,
                StockName = stock.sto_isim,
                stock.sto_birim1_ad,
                stock.sto_birim2_ad,
                stock.sto_birim3_ad,
                stock.sto_birim4_ad,
                movement.sth_birim_pntr,
                movement.sth_miktar,
                movement.sth_tutar,
                movement.sth_parti_kodu,
                movement.sth_lot_no,
                movement.sth_proje_kodu
            }).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException(
                requirePending
                    ? "Pending warehouse receiving detail was not found."
                    : returnType == ReturnMovement
                    ? "Warehouse return detail was not found."
                    : "Inter warehouse shipment detail was not found.");
        }

        var headerCount = rows
            .Select(row => new
            {
                row.sth_belge_no,
                row.sth_belge_tarih,
                row.sth_tarih,
                row.sth_evrakno_seri,
                row.sth_evrakno_sira,
                row.sth_cikis_depo_no,
                row.sth_giris_depo_no,
                row.sth_nakliyedeposu,
                row.sth_nakliyedurumu,
                row.sth_normal_iade
            })
            .Distinct()
            .Count();

        if (headerCount > 1)
        {
            throw new InvalidOperationException(
                returnType == ReturnMovement
                    ? "More than one warehouse return matched the requested serie and order number for the selected warehouse."
                    : "More than one inter warehouse shipment matched the requested serie and order number for the selected warehouse.");
        }

        var firstRow = rows[0];
        var sourceWarehouseNo = firstRow.sth_cikis_depo_no ?? 0;
        var targetWarehouseNo = firstRow.sth_giris_depo_no ?? 0;
        var shippingWarehouseNo = firstRow.sth_nakliyedeposu ?? 0;
        var isReturn = firstRow.sth_normal_iade == ReturnMovement;
        var normalizedDocumentSerie = firstRow.sth_evrakno_seri ?? documentSerie;
        var normalizedDocumentOrderNo = firstRow.sth_evrakno_sira ?? request.DocumentOrderNo;
        var warehouseOrderNos = rows
            .Select(row => BuildWarehouseOrderNo(row.WarehouseOrderSerie, row.WarehouseOrderNo))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var items = rows
            .Select(row =>
            {
                var unitPointer = NormalizeUnitPointer(row.sth_birim_pntr);
                var quantity = row.sth_miktar ?? 0d;
                var lineAmount = row.sth_tutar ?? 0d;

                return new WarehouseShippingLineItemDto(
                    row.sth_Guid,
                    row.sth_satirno ?? 0,
                    row.sth_stok_kod ?? string.Empty,
                    row.StockName ?? string.Empty,
                    ResolveUnitName(unitPointer, row.sto_birim1_ad, row.sto_birim2_ad, row.sto_birim3_ad, row.sto_birim4_ad),
                    unitPointer,
                    quantity,
                    quantity == 0d ? 0d : lineAmount / quantity,
                    lineAmount,
                    row.sth_aciklama ?? string.Empty,
                    row.sth_parti_kodu ?? string.Empty,
                    row.sth_lot_no ?? 0,
                    row.sth_proje_kodu ?? string.Empty,
                    BuildWarehouseOrderNo(row.WarehouseOrderSerie, row.WarehouseOrderNo));
            })
            .ToArray();

        var header = new WarehouseShippingHeaderDto(
            firstRow.sth_belge_tarih,
            firstRow.sth_tarih,
            firstRow.sth_belge_no ?? string.Empty,
            normalizedDocumentSerie,
            normalizedDocumentOrderNo,
            sourceWarehouseNo,
            firstRow.SourceWarehouseName ?? string.Empty,
            targetWarehouseNo,
            firstRow.TargetWarehouseName ?? string.Empty,
            shippingWarehouseNo,
            firstRow.sth_nakliyedurumu ?? 0,
            isReturn,
            firstRow.sth_HareketGrupKodu1 ?? string.Empty,
            firstRow.sth_HareketGrupKodu3 ?? string.Empty,
            firstRow.sth_ismerkezi_kodu ?? string.Empty,
            firstRow.sth_aciklama ?? string.Empty,
            string.Join(", ", warehouseOrderNos),
            warehouseOrderNos,
            items.Length,
            items.Sum(item => item.Quantity),
            items.Sum(item => item.LineAmount));

        return new WarehouseShippingDetailDto(header, items);
    }

    private static string BuildWarehouseOrderNo(string? documentSerie, int? documentOrderNo)
    {
        if (string.IsNullOrWhiteSpace(documentSerie) || documentOrderNo is null or < 0)
        {
            return string.Empty;
        }

        return $"{documentSerie.Trim()}.{documentOrderNo.Value}";
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
}
