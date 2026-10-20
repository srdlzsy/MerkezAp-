using System.Globalization;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.BakeryReport;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanDepoSiparisleri.BakeryReport;

public sealed class GetBakeryProductionReportUseCase(MikroDbContext mikroDbContext)
    : IGetBakeryProductionReportUseCase
{
    private static readonly IReadOnlyDictionary<string, string> CategoryNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["22"] = "Sarf Pastane",
            ["30"] = "Yas Pasta",
            ["31"] = "Kuru Pasta",
            ["32"] = "Baklava",
            ["33"] = "Mayali Grup"
        };

    private static readonly string[] CategoryCodes = CategoryNames.Keys.ToArray();
    private static readonly StringComparer TurkishComparer =
        StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    public async Task<BakeryProductionReportDto> ExecuteAsync(
        BakeryProductionReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceWarehouseNo <= 0)
        {
            throw new ArgumentException("Source warehouse no must be greater than zero.", nameof(request));
        }

        var startDate = request.ReportDate.ToDateTime(TimeOnly.MinValue);
        var endDateExclusive = startDate.AddDays(1);
        var sourceWarehouseName = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no == request.SourceWarehouseNo)
            .Select(warehouse => warehouse.dep_adi)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var rows = await (
            from order in mikroDbContext.DEPOLAR_ARASI_SIPARISLERs.AsNoTracking()
            join stock in mikroDbContext.STOKLARs.AsNoTracking()
                on order.ssip_stok_kod equals stock.sto_kod
            join branch in mikroDbContext.DEPOLARs.AsNoTracking()
                on order.ssip_girdepo equals branch.dep_no into branchGroup
            from branch in branchGroup.DefaultIfEmpty()
            where order.ssip_iptal != true &&
                  order.ssip_cikdepo == request.SourceWarehouseNo &&
                  order.ssip_tarih.HasValue &&
                  order.ssip_tarih.Value >= startDate &&
                  order.ssip_tarih.Value < endDateExclusive &&
                  CategoryCodes.Contains(stock.sto_model_kodu!)
            group order by new
            {
                BranchNo = order.ssip_girdepo,
                BranchName = branch.dep_adi,
                DocumentSerie = order.ssip_evrakno_seri,
                DocumentOrderNo = order.ssip_evrakno_sira,
                ProductCode = stock.sto_kod,
                ProductName = stock.sto_isim,
                TypeCode = stock.sto_model_kodu
            }
            into grouped
            select new
            {
                grouped.Key.BranchNo,
                grouped.Key.BranchName,
                grouped.Key.DocumentSerie,
                grouped.Key.DocumentOrderNo,
                grouped.Key.ProductCode,
                grouped.Key.ProductName,
                grouped.Key.TypeCode,
                Quantity = grouped.Sum(order => order.ssip_miktar ?? 0d)
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new BakeryProductionReportItemDto(
                row.BranchNo ?? 0,
                row.BranchName ?? string.Empty,
                row.DocumentSerie ?? string.Empty,
                row.DocumentOrderNo ?? 0,
                row.ProductCode,
                row.ProductName ?? string.Empty,
                row.TypeCode ?? string.Empty,
                ResolveCategoryName(row.TypeCode),
                row.Quantity))
            .OrderBy(item => item.BranchName, TurkishComparer)
            .ThenBy(item => item.BranchNo)
            .ThenBy(item => item.DocumentSerie, TurkishComparer)
            .ThenBy(item => item.DocumentOrderNo)
            .ThenBy(item => item.ProductName, TurkishComparer)
            .ThenBy(item => item.ProductCode, TurkishComparer)
            .ToArray();

        return new BakeryProductionReportDto(
            request.ReportDate,
            request.SourceWarehouseNo,
            sourceWarehouseName,
            items);
    }

    internal static string ResolveCategoryName(string? typeCode) =>
        typeCode is not null && CategoryNames.TryGetValue(typeCode, out var name)
            ? name
            : string.Empty;
}
