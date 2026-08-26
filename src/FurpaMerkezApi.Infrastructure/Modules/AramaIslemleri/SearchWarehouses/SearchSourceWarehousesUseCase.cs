using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchWarehouses;

public sealed class SearchSourceWarehousesUseCase(MikroDbContext mikroDbContext) : ISearchSourceWarehousesUseCase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 200;

    public async Task<IReadOnlyCollection<SourceWarehouseLookupItemDto>> ExecuteAsync(
        SourceWarehouseSearchRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var searchText = NormalizeOrNull(request.SearchText);
        var searchWarehouseNo = TryParseWarehouseNo(searchText);
        var like = searchText is null ? null : $"%{searchText}%";

        var query = mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse =>
                warehouse.dep_iptal != true &&
                warehouse.dep_no.HasValue &&
                warehouse.dep_no > 0 &&
                warehouse.dep_barkod_yazici_yolu != null &&
                warehouse.dep_barkod_yazici_yolu.Trim() != string.Empty);

        if (like is not null)
        {
            query = query.Where(warehouse =>
                (searchWarehouseNo.HasValue && warehouse.dep_no == searchWarehouseNo.Value) ||
                (warehouse.dep_adi != null && EF.Functions.Like(warehouse.dep_adi, like)) ||
                (warehouse.dep_barkod_yazici_yolu != null && EF.Functions.Like(warehouse.dep_barkod_yazici_yolu, like)));
        }

        var warehouses = await query
            .OrderBy(warehouse => warehouse.dep_no)
            .Select(warehouse => new
            {
                warehouse.dep_no,
                warehouse.dep_adi,
                warehouse.dep_barkod_yazici_yolu
            })
            .Take(take)
            .ToListAsync(cancellationToken);

        return warehouses
            .Select(warehouse =>
            {
                var modelCodes = ParseModelCodes(warehouse.dep_barkod_yazici_yolu);
                var modelNames = modelCodes
                    .Select(GetModelName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var sourceWarehouseNo = warehouse.dep_no ?? 0;
                var sourceWarehouseName = warehouse.dep_adi ?? string.Empty;

                return new SourceWarehouseLookupItemDto(
                    sourceWarehouseNo,
                    sourceWarehouseName,
                    modelCodes,
                    modelNames,
                    CreateDisplayName(sourceWarehouseNo, sourceWarehouseName, modelNames));
            })
            .Where(warehouse => warehouse.ModelCodes.Count > 0)
            .ToArray();
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static int? TryParseWarehouseNo(string? value) =>
        int.TryParse(value, out var warehouseNo) && warehouseNo > 0
            ? warehouseNo
            : null;

    private static IReadOnlyCollection<string> ParseModelCodes(string? value) =>
        (value ?? string.Empty)
            .Replace(';', ',')
            .Replace('|', ',')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string CreateDisplayName(
        int sourceWarehouseNo,
        string sourceWarehouseName,
        IReadOnlyCollection<string> modelNames)
    {
        var models = modelNames.Count > 0
            ? $" ({string.Join(", ", modelNames)})"
            : string.Empty;

        return $"{sourceWarehouseNo} - {sourceWarehouseName}{models}";
    }

    private static string GetModelName(string modelCode) =>
        modelCode.Trim() switch
        {
            "01" => "Market",
            "02" => "Market",
            "03" => "Market",
            "04" => "Market",
            "10" => "Meyve",
            "11" => "Sebze",
            "12" => "Yesillik",
            "15" => "Sarkuteri",
            "20" => "Market",
            "21" => "Sarkuteri",
            "22" => "Unlu Mamul",
            "23" => "Manav Sarf",
            "30" => "Unlu Mamul",
            "31" => "Unlu Mamul",
            "32" => "Unlu Mamul",
            "33" => "Unlu Mamul",
            "40" => "Unlu Mamul",
            _ => modelCode
        };
}
