using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Reports;

public sealed class GreenGrocerReportsUseCase(MikroDbContext mikroDbContext)
    : IGreenGrocerReportsUseCase
{
    private const string GreensTypeCode = "12";
    private static readonly string[] GreenGrocerTypeCodes = ["10", "11", "12"];

    public async Task<GreenGrocerBranchReportDto> GetByBranchAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var items = await ListBranchItemsAsync(request, cancellationToken);
        var lazyBranches = await ListLazyBranchesAsync(items, cancellationToken);

        return new GreenGrocerBranchReportDto(items, lazyBranches);
    }

    public async Task<IReadOnlyCollection<GreenGrocerGreenReportItemDto>> GetGreensAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = ResolveDateRange(request);

        var rows = await (
            from order in mikroDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking()
            join product in mikroDbContext.STOKLARs.AsNoTracking()
                on order.ssip_stok_kod equals product.sto_kod
            join branch in mikroDbContext.DEPOLARs.AsNoTracking()
                on order.ssip_girdepo equals branch.dep_no
            where order.ssip_tarih.HasValue &&
                  order.ssip_tarih.Value >= startDate &&
                  order.ssip_tarih.Value < endDateExclusive &&
                  product.sto_model_kodu == GreensTypeCode
            orderby branch.dep_adi, product.sto_kisa_ismi ?? product.sto_isim, order.ssip_satirno
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
                ProductName = product.sto_kisa_ismi ?? product.sto_isim,
                Quantity = order.ssip_miktar
            }).ToListAsync(cancellationToken);

        return rows
            .Select(row => new GreenGrocerGreenReportItemDto(
                row.OrderDate ?? startDate,
                row.BranchNo ?? 0,
                row.BranchName ?? string.Empty,
                row.DocumentSerie ?? string.Empty,
                row.DocumentOrderNo ?? 0,
                row.RowNo ?? 0,
                row.TypeCode ?? string.Empty,
                row.ProductCode ?? string.Empty,
                row.ProductName ?? string.Empty,
                row.Quantity ?? 0d))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<GreenGrocerProductReportItemDto>> GetSummaryAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var branchItems = await ListBranchItemsAsync(request, cancellationToken);

        return branchItems
            .GroupBy(item => new
            {
                item.TypeCode,
                item.ProductCode,
                item.ProductName
            })
            .OrderBy(group => group.Key.TypeCode)
            .ThenBy(group => group.Key.ProductName)
            .Select(group => new GreenGrocerProductReportItemDto(
                group.Key.TypeCode,
                group.Key.ProductCode,
                group.Key.ProductName,
                group.Sum(item => item.Quantity)))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<GreenGrocerProductReportGroupDto>> GetByProductAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var branchItems = await ListBranchItemsAsync(request, cancellationToken);

        return branchItems
            .GroupBy(item => new
            {
                item.TypeCode,
                item.ProductCode,
                item.ProductName
            })
            .OrderBy(group => group.Key.ProductName)
            .ThenBy(group => group.Key.TypeCode)
            .Select(group => new GreenGrocerProductReportGroupDto(
                group.Key.TypeCode,
                group.Key.ProductCode,
                group.Key.ProductName,
                group.Sum(item => item.Quantity),
                group
                    .OrderBy(item => item.BranchName)
                    .ThenBy(item => item.DocumentOrderNo)
                    .Select(item => new GreenGrocerProductBranchItemDto(
                        item.BranchNo,
                        item.BranchName,
                        item.DocumentSerie,
                        item.DocumentOrderNo,
                        item.Quantity))
                    .ToArray()))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<GreenGrocerBranchReportItemDto>> ListBranchItemsAsync(
        GreenGrocerReportDateRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = ResolveDateRange(request);

        var rows = await (
            from order in mikroDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking()
            join product in mikroDbContext.STOKLARs.AsNoTracking()
                on order.ssip_stok_kod equals product.sto_kod
            join branch in mikroDbContext.DEPOLARs.AsNoTracking()
                on order.ssip_girdepo equals branch.dep_no
            where order.ssip_tarih.HasValue &&
                  order.ssip_tarih.Value >= startDate &&
                  order.ssip_tarih.Value < endDateExclusive &&
                  GreenGrocerTypeCodes.Contains(product.sto_model_kodu!)
            group new
            {
                order,
                product,
                branch
            }
            by new
            {
                OrderDate = order.ssip_tarih,
                BranchNo = branch.dep_no,
                BranchName = branch.dep_adi,
                DocumentSerie = order.ssip_evrakno_seri,
                DocumentOrderNo = order.ssip_evrakno_sira,
                TypeCode = product.sto_model_kodu,
                ProductCode = order.ssip_stok_kod,
                ProductName = product.sto_isim
            }
            into grouped
            orderby grouped.Key.BranchName, grouped.Key.TypeCode, grouped.Key.ProductName
            select new
            {
                grouped.Key.OrderDate,
                grouped.Key.BranchNo,
                grouped.Key.BranchName,
                grouped.Key.DocumentSerie,
                grouped.Key.DocumentOrderNo,
                grouped.Key.TypeCode,
                grouped.Key.ProductCode,
                grouped.Key.ProductName,
                Quantity = grouped.Sum(item => item.order.ssip_miktar ?? 0d)
            }).ToListAsync(cancellationToken);

        return rows
            .Select(row => new GreenGrocerBranchReportItemDto(
                row.OrderDate ?? startDate,
                row.BranchNo ?? 0,
                row.BranchName ?? string.Empty,
                row.DocumentSerie ?? string.Empty,
                row.DocumentOrderNo ?? 0,
                row.TypeCode ?? string.Empty,
                row.ProductCode ?? string.Empty,
                row.ProductName ?? string.Empty,
                row.Quantity))
            .ToArray();
    }

    private async Task<IReadOnlyCollection<GreenGrocerLazyBranchDto>> ListLazyBranchesAsync(
        IReadOnlyCollection<GreenGrocerBranchReportItemDto> reportItems,
        CancellationToken cancellationToken)
    {
        var reportedBranches = reportItems
            .Where(item => item.BranchNo > 0)
            .Select(item => item.BranchNo)
            .ToHashSet();

        var branches = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(branch =>
                branch.dep_iptal != true &&
                branch.dep_tipi == 1 &&
                branch.dep_no.HasValue)
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

    private static (DateTime StartDate, DateTime EndDateExclusive) ResolveDateRange(
        GreenGrocerReportDateRequest request)
    {
        if (request.Date == default)
        {
            throw new ArgumentException("Date is required.", nameof(request.Date));
        }

        var startDate = request.Date.Date;
        return (startDate, startDate.AddDays(1));
    }
}
