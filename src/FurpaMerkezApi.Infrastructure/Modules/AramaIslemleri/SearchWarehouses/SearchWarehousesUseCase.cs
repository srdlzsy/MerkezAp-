using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchWarehouses;

public sealed class SearchWarehousesUseCase(MikroDbContext mikroDbContext) : ISearchWarehousesUseCase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 200;

    public async Task<IReadOnlyCollection<WarehouseLookupItemDto>> ExecuteAsync(
        WarehouseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var searchText = NormalizeOrNull(request.SearchText);
        var like = searchText is null ? null : $"%{searchText}%";

        var query = mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_iptal != true && warehouse.dep_no.HasValue);

        if (request.WarehouseNo.HasValue)
        {
            query = query.Where(warehouse => warehouse.dep_no == request.WarehouseNo.Value);
        }

        if (like is not null)
        {
            query = query.Where(warehouse =>
                (warehouse.dep_adi != null && EF.Functions.Like(warehouse.dep_adi, like)) ||
                (warehouse.dep_grup_kodu != null && EF.Functions.Like(warehouse.dep_grup_kodu, like)) ||
                (warehouse.dep_Il != null && EF.Functions.Like(warehouse.dep_Il, like)) ||
                (warehouse.dep_Ilce != null && EF.Functions.Like(warehouse.dep_Ilce, like)));
        }

        var warehouses = await query
            .OrderBy(warehouse => warehouse.dep_no)
            .Select(warehouse => new
            {
                warehouse.dep_no,
                warehouse.dep_adi,
                warehouse.dep_firmano,
                warehouse.dep_subeno,
                warehouse.dep_grup_kodu,
                warehouse.dep_tipi,
                warehouse.dep_sor_mer_kodu,
                warehouse.dep_proje_kodu,
                warehouse.dep_cadde,
                warehouse.dep_mahalle,
                warehouse.dep_sokak,
                warehouse.dep_Ilce,
                warehouse.dep_Il,
                warehouse.dep_envanter_harici_fl
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        return warehouses
            .Select(warehouse => new WarehouseLookupItemDto(
                warehouse.dep_no ?? 0,
                warehouse.dep_adi ?? string.Empty,
                warehouse.dep_firmano,
                warehouse.dep_subeno,
                warehouse.dep_grup_kodu ?? string.Empty,
                warehouse.dep_tipi,
                warehouse.dep_sor_mer_kodu ?? string.Empty,
                warehouse.dep_proje_kodu ?? string.Empty,
                JoinNonEmpty(warehouse.dep_cadde, warehouse.dep_mahalle, warehouse.dep_sokak),
                warehouse.dep_Ilce ?? string.Empty,
                warehouse.dep_Il ?? string.Empty,
                warehouse.dep_envanter_harici_fl ?? false))
            .ToArray();
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
}
