using System.Data;
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
        var movements = CreateFilteredMovementQuery(request.WarehouseNo, startDate, endDateExclusive, kind);

        var documentSummaries =
            from movement in movements
            group movement
            by new
            {
                movement.sth_belge_tarih,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cari_kodu,
                AuthoritativeWarehouseNo = kind == CompanyMovementKind.IncomingShipment
                    ? movement.sth_giris_depo_no
                    : movement.sth_cikis_depo_no,
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_normal_iade
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
                grouped.Key.sth_cari_kodu,
                sth_giris_depo_no = kind == CompanyMovementKind.IncomingShipment
                    ? grouped.Key.AuthoritativeWarehouseNo
                    : (int?)null,
                sth_cikis_depo_no = kind == CompanyMovementKind.IncomingShipment
                    ? (int?)null
                    : grouped.Key.AuthoritativeWarehouseNo,
                grouped.Key.sth_evraktip,
                grouped.Key.sth_tip,
                grouped.Key.sth_normal_iade,
                LineCount = grouped.Count(),
                TotalQuantity = grouped.Sum(item => item.sth_miktar ?? 0d),
                TotalAmount = grouped.Sum(item => item.sth_tutar ?? 0d)
            };

        var query =
            from document in documentSummaries
            join customer in mikroDbContext.CARI_HESAPLARs.AsNoTracking()
                on document.sth_cari_kodu equals customer.cari_kod into customerGroup
            from customer in customerGroup.DefaultIfEmpty()
            join inputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on document.sth_giris_depo_no equals inputWarehouse.dep_no into inputWarehouseGroup
            from inputWarehouse in inputWarehouseGroup.DefaultIfEmpty()
            join outputWarehouse in mikroDbContext.DEPOLARs.AsNoTracking()
                on document.sth_cikis_depo_no equals outputWarehouse.dep_no into outputWarehouseGroup
            from outputWarehouse in outputWarehouseGroup.DefaultIfEmpty()
            orderby document.sth_belge_tarih,
                document.MovementCreateDate,
                document.sth_evrakno_seri,
                document.sth_evrakno_sira
            select new
            {
                document.sth_belge_tarih,
                document.MovementCreateDate,
                document.sth_tarih,
                document.sth_belge_no,
                document.sth_evrakno_seri,
                document.sth_evrakno_sira,
                document.sth_cari_kodu,
                CustomerName = customer.cari_unvan1,
                CustomerTitle = customer.cari_unvan2,
                document.sth_giris_depo_no,
                InputWarehouseName = inputWarehouse.dep_adi,
                document.sth_cikis_depo_no,
                OutputWarehouseName = outputWarehouse.dep_adi,
                document.sth_evraktip,
                document.sth_tip,
                document.sth_normal_iade,
                document.LineCount,
                document.TotalQuantity,
                document.TotalAmount
            };

        var documents = await ExecuteReadOnlyListAsync(query, cancellationToken);

        // The opposite warehouse is display metadata. Mikro Desktop can append a line with a
        // different value, but the document header must continue to reflect the first movement.
        // Resolve it in one narrow, linear query instead of a correlated subquery per document.
        var movementWarehouseRows = await ExecuteReadOnlyListAsync(
            movements.Select(movement => new
            {
                movement.sth_belge_tarih,
                movement.sth_tarih,
                movement.sth_belge_no,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_cari_kodu,
                AuthoritativeWarehouseNo = kind == CompanyMovementKind.IncomingShipment
                    ? movement.sth_giris_depo_no
                    : movement.sth_cikis_depo_no,
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_normal_iade,
                movement.sth_satirno,
                movement.sth_create_date,
                movement.sth_Guid,
                movement.sth_giris_depo_no,
                movement.sth_cikis_depo_no
            }),
            cancellationToken);
        var firstWarehouseByDocument = movementWarehouseRows
            .GroupBy(item => new DocumentIdentity(
                item.sth_belge_tarih,
                item.sth_tarih,
                item.sth_belge_no,
                item.sth_evrakno_seri,
                item.sth_evrakno_sira,
                item.sth_cari_kodu,
                item.AuthoritativeWarehouseNo,
                item.sth_evraktip,
                item.sth_tip,
                item.sth_normal_iade))
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.sth_satirno)
                    .ThenBy(item => item.sth_create_date)
                    .ThenBy(item => item.sth_Guid)
                    .Select(item => new WarehousePair(item.sth_giris_depo_no, item.sth_cikis_depo_no))
                    .First());
        var warehouseNames = (await ExecuteReadOnlyListAsync(
                mikroDbContext.DEPOLARs.AsNoTracking().Select(warehouse => new
                {
                    warehouse.dep_no,
                    warehouse.dep_adi
                }),
                cancellationToken))
            .Where(warehouse => warehouse.dep_no.HasValue)
            .GroupBy(warehouse => warehouse.dep_no)
            .ToDictionary(
                group => group.Key!.Value,
                group => group.Select(warehouse => warehouse.dep_adi).FirstOrDefault() ?? string.Empty);

        return documents
            .Select(document =>
            {
                var identity = new DocumentIdentity(
                    document.sth_belge_tarih,
                    document.sth_tarih,
                    document.sth_belge_no,
                    document.sth_evrakno_seri,
                    document.sth_evrakno_sira,
                    document.sth_cari_kodu,
                    kind == CompanyMovementKind.IncomingShipment
                        ? document.sth_giris_depo_no
                        : document.sth_cikis_depo_no,
                    document.sth_evraktip,
                    document.sth_tip,
                    document.sth_normal_iade);
                firstWarehouseByDocument.TryGetValue(identity, out var firstWarehouses);
                var inputWarehouseNo = firstWarehouses?.InputWarehouseNo ?? document.sth_giris_depo_no;
                var outputWarehouseNo = firstWarehouses?.OutputWarehouseNo ?? document.sth_cikis_depo_no;
                var inputWarehouseName = WarehouseName(warehouseNames, inputWarehouseNo, document.InputWarehouseName);
                var outputWarehouseName = WarehouseName(warehouseNames, outputWarehouseNo, document.OutputWarehouseName);
                var warehouseNo = kind == CompanyMovementKind.IncomingShipment
                    ? inputWarehouseNo ?? request.WarehouseNo ?? 0
                    : outputWarehouseNo ?? request.WarehouseNo ?? 0;
                var warehouseName = kind == CompanyMovementKind.IncomingShipment
                    ? inputWarehouseName
                    : outputWarehouseName;

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
                    inputWarehouseNo ?? 0,
                    inputWarehouseName,
                    outputWarehouseNo ?? 0,
                    outputWarehouseName,
                    document.sth_evraktip ?? 0,
                    document.sth_tip ?? 0,
                    document.sth_normal_iade ?? 0,
                    string.Empty,
                    document.LineCount,
                    document.TotalQuantity,
                    document.TotalAmount);
            })
            .ToArray();
    }

    private static string WarehouseName(
        IReadOnlyDictionary<int, string> warehouseNames,
        int? warehouseNo,
        string? fallback) =>
        warehouseNo.HasValue && warehouseNames.TryGetValue(warehouseNo.Value, out var name)
            ? name
            : fallback ?? string.Empty;

    private IQueryable<STOK_HAREKETLERI> CreateFilteredMovementQuery(
        int? warehouseNo,
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
                (!warehouseNo.HasValue || movement.sth_cikis_depo_no == warehouseNo.Value)),

            CompanyMovementKind.IncomingShipment => movements.Where(movement =>
                movement.sth_tarih.HasValue &&
                movement.sth_tarih.Value >= startDate &&
                movement.sth_tarih.Value < endDateExclusive &&
                movement.sth_evraktip == ReceivingReceiptDocumentType &&
                movement.sth_tip == IncomingMovementType &&
                movement.sth_normal_iade == NormalMovement &&
                (!warehouseNo.HasValue || movement.sth_giris_depo_no == warehouseNo.Value)),

            CompanyMovementKind.PurchaseReturn => movements.Where(movement =>
                movement.sth_belge_tarih.HasValue &&
                movement.sth_belge_tarih.Value >= startDate &&
                movement.sth_belge_tarih.Value < endDateExclusive &&
                movement.sth_evraktip == CompanyDispatchDocumentType &&
                movement.sth_tip == OutgoingMovementType &&
                movement.sth_normal_iade == ReturnMovement &&
                (!warehouseNo.HasValue || movement.sth_cikis_depo_no == warehouseNo.Value)),

            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown company movement kind.")
        };
    }

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

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

    private sealed record DocumentIdentity(
        DateTime? DocumentDate,
        DateTime? MovementDate,
        string? DocumentNo,
        string? DocumentSerie,
        int? DocumentOrderNo,
        string? CustomerCode,
        int? AuthoritativeWarehouseNo,
        byte? DocumentType,
        byte? MovementType,
        byte? ReturnType);

    private sealed record WarehousePair(int? InputWarehouseNo, int? OutputWarehouseNo);
}
