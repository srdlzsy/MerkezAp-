using System.Data;
using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.FirmaEvrakTakibi;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.FirmaEvrakTakibi;

public sealed class CompanyDocumentTrackingService(
    MikroDbContext mikroDbContext,
    IClock clock) : ICompanyDocumentTrackingService
{
    private const byte CompanyDispatchDocumentType = 1;
    private const byte CompanyReceivingDocumentType = 13;
    private const byte IncomingMovementType = 0;
    private const byte OutgoingMovementType = 1;
    private const byte NormalMovement = 0;
    private const byte ReturnMovement = 1;
    private const string CompanyReceivingKind = "CompanyReceiving";
    private const string CompanyReturnKind = "CompanyReturn";

    public async Task<CompanyDocumentTrackingDto> GetAsync(
        CompanyDocumentTrackingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request));
        }

        var startDate = request.Date.ToDateTime(TimeOnly.MinValue);
        var endDateExclusive = startDate.AddDays(1);

        var movements = mikroDbContext.STOK_HAREKETLERIs
            .AsNoTracking()
            .Where(movement => movement.sth_iptal != true)
            .Where(movement =>
                (movement.sth_evraktip == CompanyReceivingDocumentType &&
                 movement.sth_tip == IncomingMovementType &&
                 movement.sth_normal_iade == NormalMovement &&
                 movement.sth_create_date >= startDate &&
                 movement.sth_create_date < endDateExclusive &&
                 (!request.WarehouseNo.HasValue || movement.sth_giris_depo_no == request.WarehouseNo.Value)) ||
                (movement.sth_evraktip == CompanyDispatchDocumentType &&
                 movement.sth_tip == OutgoingMovementType &&
                 movement.sth_normal_iade == ReturnMovement &&
                 movement.sth_belge_tarih.HasValue &&
                 movement.sth_belge_tarih.Value >= startDate &&
                 movement.sth_belge_tarih.Value < endDateExclusive &&
                 (!request.WarehouseNo.HasValue || movement.sth_cikis_depo_no == request.WarehouseNo.Value)));

        var documentSummaries =
            from movement in movements
            group movement
            by new
            {
                movement.sth_evraktip,
                movement.sth_tip,
                movement.sth_normal_iade,
                movement.sth_evrakno_seri,
                movement.sth_evrakno_sira,
                movement.sth_belge_no,
                movement.sth_belge_tarih,
                movement.sth_cari_kodu,
                movement.sth_giris_depo_no,
                movement.sth_cikis_depo_no,
                movement.sth_HareketGrupKodu2,
                movement.sth_HareketGrupKodu3
            }
            into grouped
            select new
            {
                grouped.Key.sth_evraktip,
                grouped.Key.sth_tip,
                grouped.Key.sth_normal_iade,
                grouped.Key.sth_evrakno_seri,
                grouped.Key.sth_evrakno_sira,
                grouped.Key.sth_belge_no,
                grouped.Key.sth_belge_tarih,
                grouped.Key.sth_cari_kodu,
                grouped.Key.sth_giris_depo_no,
                grouped.Key.sth_cikis_depo_no,
                grouped.Key.sth_HareketGrupKodu2,
                grouped.Key.sth_HareketGrupKodu3,
                MovementCreateDate = grouped.Min(item => item.sth_create_date),
                LineCount = grouped.Count(),
                TotalQuantity = grouped.Sum(item => item.sth_miktar ?? 0d)
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
            select new
            {
                document.sth_evraktip,
                document.sth_tip,
                document.sth_normal_iade,
                document.sth_evrakno_seri,
                document.sth_evrakno_sira,
                document.sth_belge_no,
                document.sth_belge_tarih,
                document.sth_cari_kodu,
                CustomerName = customer.cari_unvan1,
                CustomerTitle = customer.cari_unvan2,
                document.sth_giris_depo_no,
                InputWarehouseName = inputWarehouse.dep_adi,
                document.sth_cikis_depo_no,
                OutputWarehouseName = outputWarehouse.dep_adi,
                document.sth_HareketGrupKodu2,
                document.sth_HareketGrupKodu3,
                document.MovementCreateDate,
                document.LineCount,
                document.TotalQuantity
            };

        var fragments = await ExecuteReadOnlyListAsync(
            query.TagWith("CompanyDocumentTracking.Get"),
            cancellationToken);

        var items = fragments
            .GroupBy(document => new
            {
                document.sth_evraktip,
                document.sth_tip,
                document.sth_normal_iade,
                document.sth_evrakno_seri,
                document.sth_evrakno_sira,
                document.sth_belge_no,
                document.sth_belge_tarih,
                document.sth_cari_kodu,
                document.CustomerName,
                document.CustomerTitle,
                document.sth_giris_depo_no,
                document.InputWarehouseName,
                document.sth_cikis_depo_no,
                document.OutputWarehouseName
            })
            .Select(group =>
            {
                var first = group.First();
                var isCompanyReceiving = first.sth_evraktip == CompanyReceivingDocumentType;
                var warehouseNo = isCompanyReceiving
                    ? first.sth_giris_depo_no ?? request.WarehouseNo ?? 0
                    : first.sth_cikis_depo_no ?? request.WarehouseNo ?? 0;
                var warehouseName = isCompanyReceiving
                    ? first.InputWarehouseName
                    : first.OutputWarehouseName;
                var documentSerie = first.sth_evrakno_seri?.Trim() ?? string.Empty;
                var documentOrderNo = first.sth_evrakno_sira ?? 0;
                var documentNo = string.IsNullOrWhiteSpace(first.sth_belge_no)
                    ? $"{documentSerie}/{documentOrderNo}"
                    : first.sth_belge_no.Trim();

                return new CompanyDocumentTrackingItemDto(
                    isCompanyReceiving ? CompanyReceivingKind : CompanyReturnKind,
                    isCompanyReceiving ? "Firma Mal Kabul" : "Firma Iadesi",
                    documentSerie,
                    documentOrderNo,
                    documentNo,
                    first.sth_cari_kodu?.Trim() ?? string.Empty,
                    first.CustomerName?.Trim() ?? string.Empty,
                    first.CustomerTitle?.Trim() ?? string.Empty,
                    JoinNonEmpty(first.CustomerName, first.CustomerTitle),
                    first.sth_belge_tarih,
                    group.Min(item => item.MovementCreateDate),
                    FirstNonEmpty(group.Select(item => item.sth_HareketGrupKodu2)),
                    FirstNonEmpty(group.Select(item => item.sth_HareketGrupKodu3)),
                    warehouseNo,
                    warehouseName?.Trim() ?? string.Empty,
                    group.Sum(item => item.LineCount),
                    group.Sum(item => item.TotalQuantity));
            })
            .OrderBy(item => item.MovementCreateDate)
            .ThenBy(item => item.DocumentSerie, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DocumentOrderNo)
            .ToArray();

        return new CompanyDocumentTrackingDto(
            request.Date,
            clock.UtcNow,
            request.WarehouseNo,
            items.Length,
            items.Count(item => item.DocumentKind == CompanyReceivingKind),
            items.Count(item => item.DocumentKind == CompanyReturnKind),
            items);
    }

    private static string FirstNonEmpty(IEnumerable<string?> values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

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
            await using var transaction = await mikroDbContext.Database.BeginTransactionAsync(
                IsolationLevel.ReadUncommitted,
                cancellationToken);
            var result = await query.ToListAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private bool IsSqlServerProvider() =>
        mikroDbContext.Database.ProviderName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true;
}
