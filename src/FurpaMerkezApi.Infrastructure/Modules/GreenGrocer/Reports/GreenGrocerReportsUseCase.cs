using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Reports;

public sealed class GreenGrocerReportsUseCase(MikroDbContext mikroDbContext)
    : IGreenGrocerReportsUseCase
{
    private const string GreensTypeCode = "12";
    private const double DeleteWindowHours = 24d;
    private const int DefaultTake = 1000;
    private const int MaxTake = 5000;
    private const int DashboardTopProductTake = 10;

    private static readonly string[] GreenGrocerTypeCodes = ["10", "11", "12"];

    private static readonly IReadOnlyCollection<GreenGrocerTypeOptionDto> TypeOptions =
    [
        new("10", "Manav Tip 10", false),
        new("11", "Manav Tip 11", false),
        new("12", "Yesillik", true)
    ];

    public IReadOnlyCollection<GreenGrocerTypeOptionDto> GetTypeOptions() => TypeOptions;

    public async Task<GreenGrocerDashboardDto> GetDashboardAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var items = await ListBranchItemsAsync(normalized, cancellationToken);
        var lazyBranches = normalized.IncludeLazyBranches
            ? await ListLazyBranchesAsync(items, normalized, cancellationToken)
            : [];
        var topProducts = BuildProductSummary(items)
            .OrderByDescending(item => item.Quantity)
            .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
            .Take(DashboardTopProductTake)
            .ToArray();
        var typeSummaries = items
            .GroupBy(item => new
            {
                item.TypeCode,
                item.TypeName
            })
            .OrderBy(group => group.Key.TypeCode)
            .Select(group => new GreenGrocerTypeSummaryDto(
                group.Key.TypeCode,
                group.Key.TypeName,
                group.Select(item => item.BranchNo).Distinct().Count(),
                CountDocuments(group),
                group.Select(item => item.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Round(group.Sum(item => item.Quantity))))
            .ToArray();
        var branchSummaries = items
            .GroupBy(item => new
            {
                item.BranchNo,
                item.BranchName
            })
            .OrderBy(group => group.Key.BranchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.BranchNo)
            .Select(group => new GreenGrocerBranchSummaryDto(
                group.Key.BranchNo,
                group.Key.BranchName,
                CountDocuments(group),
                group.Select(item => item.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Round(group.Sum(item => item.Quantity))))
            .Take(normalized.Take)
            .ToArray();

        return new GreenGrocerDashboardDto(
            normalized.Date,
            normalized.WarehouseNo,
            items.Select(item => item.BranchNo).Distinct().Count(),
            lazyBranches.Count,
            CountDocuments(items),
            items.Select(item => item.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Round(items.Sum(item => item.Quantity)),
            typeSummaries,
            branchSummaries,
            topProducts,
            lazyBranches);
    }

    public async Task<GreenGrocerBranchReportDto> GetByBranchAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var items = await ListBranchItemsAsync(normalized, cancellationToken);
        var lazyBranches = normalized.IncludeLazyBranches
            ? await ListLazyBranchesAsync(items, normalized, cancellationToken)
            : [];

        return new GreenGrocerBranchReportDto(
            items
                .OrderBy(item => item.BranchName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.BranchNo)
                .ThenBy(item => item.TypeCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .Take(normalized.Take)
                .ToArray(),
            lazyBranches);
    }

    public async Task<IReadOnlyCollection<GreenGrocerGreenReportItemDto>> GetGreensAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request with { TypeCode = GreensTypeCode });
        var now = DateTime.Now;

        var query =
            from order in mikroDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking()
            join product in mikroDbContext.STOKLARs.AsNoTracking()
                on order.ssip_stok_kod equals product.sto_kod
            join branch in mikroDbContext.DEPOLARs.AsNoTracking()
                on order.ssip_girdepo equals branch.dep_no
            let productName = product.sto_kisa_ismi ?? product.sto_isim
            where order.ssip_iptal != true &&
                  order.ssip_tarih.HasValue &&
                  order.ssip_tarih.Value >= normalized.StartDate &&
                  order.ssip_tarih.Value < normalized.EndDateExclusive &&
                  product.sto_model_kodu == GreensTypeCode &&
                  (normalized.WarehouseNo == null || branch.dep_no == normalized.WarehouseNo.Value)
            select new
            {
                OrderDate = order.ssip_tarih,
                BranchNo = branch.dep_no,
                BranchName = branch.dep_adi,
                DocumentSerie = order.ssip_evrakno_seri,
                DocumentOrderNo = order.ssip_evrakno_sira,
                RowNo = order.ssip_satirno,
                TypeCode = product.sto_model_kodu,
                ProductCode = order.ssip_stok_kod,
                ProductName = productName,
                Quantity = order.ssip_miktar,
                LatestCreateDate = order.ssip_create_date
            };

        if (normalized.Search is not null)
        {
            query = query.Where(row =>
                (row.ProductCode != null && row.ProductCode.Contains(normalized.Search)) ||
                (row.ProductName != null && row.ProductName.Contains(normalized.Search)) ||
                (row.BranchName != null && row.BranchName.Contains(normalized.Search)) ||
                (row.DocumentSerie != null && row.DocumentSerie.Contains(normalized.Search)));
        }

        var rows = await query
            .OrderBy(row => row.BranchName)
            .ThenBy(row => row.ProductName)
            .ThenBy(row => row.RowNo)
            .Take(normalized.Take)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new GreenGrocerGreenReportItemDto(
                row.OrderDate ?? normalized.Date,
                row.BranchNo ?? 0,
                row.BranchName ?? string.Empty,
                row.DocumentSerie ?? string.Empty,
                row.DocumentOrderNo ?? 0,
                row.RowNo ?? 0,
                row.TypeCode ?? string.Empty,
                GetTypeName(row.TypeCode),
                row.ProductCode ?? string.Empty,
                row.ProductName ?? string.Empty,
                Round(row.Quantity ?? 0d),
                row.LatestCreateDate,
                CanDelete(row.LatestCreateDate, now)))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<GreenGrocerProductReportItemDto>> GetSummaryAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var branchItems = await ListBranchItemsAsync(normalized, cancellationToken);

        return BuildProductSummary(branchItems)
            .OrderBy(item => item.TypeCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
            .Take(normalized.Take)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<GreenGrocerProductReportGroupDto>> GetByProductAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var branchItems = await ListBranchItemsAsync(normalized, cancellationToken);

        return branchItems
            .GroupBy(item => new
            {
                item.TypeCode,
                item.TypeName,
                item.ProductCode,
                item.ProductName
            })
            .OrderBy(group => group.Key.ProductName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.TypeCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new GreenGrocerProductReportGroupDto(
                group.Key.TypeCode,
                group.Key.TypeName,
                group.Key.ProductCode,
                group.Key.ProductName,
                Round(group.Sum(item => item.Quantity)),
                group
                    .OrderBy(item => item.BranchName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.DocumentOrderNo)
                    .Select(item => new GreenGrocerProductBranchItemDto(
                        item.BranchNo,
                        item.BranchName,
                        item.DocumentSerie,
                        item.DocumentOrderNo,
                        item.Quantity,
                        item.LatestCreateDate,
                        item.CanDelete))
                    .ToArray()))
            .Take(normalized.Take)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<GreenGrocerBranchReportItemDto>> ListBranchItemsAsync(
        NormalizedGreenGrocerReportRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var query =
            from order in mikroDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking()
            join product in mikroDbContext.STOKLARs.AsNoTracking()
                on order.ssip_stok_kod equals product.sto_kod
            join branch in mikroDbContext.DEPOLARs.AsNoTracking()
                on order.ssip_girdepo equals branch.dep_no
            let productName = product.sto_kisa_ismi ?? product.sto_isim
            where order.ssip_iptal != true &&
                  order.ssip_tarih.HasValue &&
                  order.ssip_tarih.Value >= request.StartDate &&
                  order.ssip_tarih.Value < request.EndDateExclusive &&
                  product.sto_model_kodu != null &&
                  GreenGrocerTypeCodes.Contains(product.sto_model_kodu) &&
                  (request.WarehouseNo == null || branch.dep_no == request.WarehouseNo.Value) &&
                  (request.TypeCode == null || product.sto_model_kodu == request.TypeCode)
            select new
            {
                OrderDate = order.ssip_tarih,
                BranchNo = branch.dep_no,
                BranchName = branch.dep_adi,
                DocumentSerie = order.ssip_evrakno_seri,
                DocumentOrderNo = order.ssip_evrakno_sira,
                TypeCode = product.sto_model_kodu,
                ProductCode = order.ssip_stok_kod,
                ProductName = productName,
                Quantity = order.ssip_miktar,
                LatestCreateDate = order.ssip_create_date
            };

        if (request.Search is not null)
        {
            query = query.Where(row =>
                (row.ProductCode != null && row.ProductCode.Contains(request.Search)) ||
                (row.ProductName != null && row.ProductName.Contains(request.Search)) ||
                (row.BranchName != null && row.BranchName.Contains(request.Search)) ||
                (row.DocumentSerie != null && row.DocumentSerie.Contains(request.Search)));
        }

        var rows = await query
            .GroupBy(row => new
            {
                row.OrderDate,
                row.BranchNo,
                row.BranchName,
                row.DocumentSerie,
                row.DocumentOrderNo,
                row.TypeCode,
                row.ProductCode,
                row.ProductName
            })
            .OrderBy(group => group.Key.BranchName)
            .ThenBy(group => group.Key.TypeCode)
            .ThenBy(group => group.Key.ProductName)
            .Select(group => new
            {
                group.Key.OrderDate,
                group.Key.BranchNo,
                group.Key.BranchName,
                group.Key.DocumentSerie,
                group.Key.DocumentOrderNo,
                group.Key.TypeCode,
                group.Key.ProductCode,
                group.Key.ProductName,
                Quantity = group.Sum(item => item.Quantity ?? 0d),
                LatestCreateDate = group.Max(item => item.LatestCreateDate)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new GreenGrocerBranchReportItemDto(
                row.OrderDate ?? request.Date,
                row.BranchNo ?? 0,
                row.BranchName ?? string.Empty,
                row.DocumentSerie ?? string.Empty,
                row.DocumentOrderNo ?? 0,
                row.TypeCode ?? string.Empty,
                GetTypeName(row.TypeCode),
                row.ProductCode ?? string.Empty,
                row.ProductName ?? string.Empty,
                Round(row.Quantity),
                row.LatestCreateDate,
                CanDelete(row.LatestCreateDate, now)))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<GreenGrocerLazyBranchDto>> ListLazyBranchesAsync(
        IReadOnlyCollection<GreenGrocerBranchReportItemDto> reportItems,
        NormalizedGreenGrocerReportRequest request,
        CancellationToken cancellationToken)
    {
        var reportedBranches = reportItems
            .Where(item => item.BranchNo > 0)
            .Select(item => item.BranchNo)
            .ToHashSet();

        var query = mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(branch =>
                branch.dep_iptal != true &&
                branch.dep_tipi == 1 &&
                branch.dep_no.HasValue);

        if (request.WarehouseNo.HasValue)
        {
            query = query.Where(branch => branch.dep_no == request.WarehouseNo.Value);
        }

        var branches = await query
            .OrderBy(branch => branch.dep_adi)
            .Select(branch => new
            {
                BranchNo = branch.dep_no!.Value,
                BranchName = branch.dep_adi,
                RegionCode = branch.dep_bolge_kodu
            })
            .ToListAsync(cancellationToken);

        return branches
            .Where(branch => !reportedBranches.Contains(branch.BranchNo))
            .Select(branch => new GreenGrocerLazyBranchDto(
                branch.BranchNo,
                branch.BranchName ?? string.Empty,
                branch.RegionCode ?? string.Empty))
            .ToArray();
    }

    private static IReadOnlyCollection<GreenGrocerProductReportItemDto> BuildProductSummary(
        IEnumerable<GreenGrocerBranchReportItemDto> branchItems) =>
        branchItems
            .GroupBy(item => new
            {
                item.TypeCode,
                item.TypeName,
                item.ProductCode,
                item.ProductName
            })
            .Select(group => new GreenGrocerProductReportItemDto(
                group.Key.TypeCode,
                group.Key.TypeName,
                group.Key.ProductCode,
                group.Key.ProductName,
                Round(group.Sum(item => item.Quantity))))
            .ToArray();

    private static NormalizedGreenGrocerReportRequest Normalize(GreenGrocerReportDateRequest request)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var date = request.Date == default
            ? DateTime.Today
            : request.Date.Date;

        return new NormalizedGreenGrocerReportRequest(
            date,
            date,
            date.AddDays(1),
            request.WarehouseNo,
            NormalizeTypeCode(request.TypeCode),
            NormalizeOrNull(request.Search),
            request.IncludeLazyBranches,
            NormalizeTake(request.Take));
    }

    private static string? NormalizeTypeCode(string? value)
    {
        var normalized = NormalizeOrNull(value)?.ToLowerInvariant();

        return normalized switch
        {
            null or "all" or "tum" => null,
            "10" => "10",
            "11" => "11",
            "12" or "green" or "greens" or "yesillik" => GreensTypeCode,
            _ => throw new ArgumentException("Unsupported green grocer type code.")
        };
    }

    private static string GetTypeName(string? typeCode) =>
        TypeOptions.FirstOrDefault(item => item.TypeCode == typeCode)?.TypeName
        ?? typeCode
        ?? string.Empty;

    private static int CountDocuments(IEnumerable<GreenGrocerBranchReportItemDto> items) =>
        items
            .Select(item => new GreenGrocerDocumentKey(
                item.BranchNo,
                item.DocumentSerie,
                item.DocumentOrderNo))
            .Distinct()
            .Count();

    private static bool CanDelete(DateTime latestCreateDate, DateTime now) =>
        (now - latestCreateDate).TotalHours < DeleteWindowHours;

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record NormalizedGreenGrocerReportRequest(
        DateTime Date,
        DateTime StartDate,
        DateTime EndDateExclusive,
        int? WarehouseNo,
        string? TypeCode,
        string? Search,
        bool IncludeLazyBranches,
        int Take);

    private sealed record GreenGrocerDocumentKey(
        int BranchNo,
        string DocumentSerie,
        int DocumentOrderNo);
}
