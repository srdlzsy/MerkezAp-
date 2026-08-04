using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;

internal static class GreenGrocerShipmentLineNormalizer
{
    private const int GreenGrocerSourceWarehouseNo = 56;
    private static readonly string[] GreenGrocerModelCodes = ["10", "11", "12", "23"];

    public static bool IsGreenGrocerSourceWarehouse(int sourceWarehouseNo) =>
        sourceWarehouseNo == GreenGrocerSourceWarehouseNo;

    public static bool IsGreenGrocerModelCode(string? modelCode) =>
        !string.IsNullOrWhiteSpace(modelCode) &&
        GreenGrocerModelCodes.Contains(modelCode.Trim(), StringComparer.OrdinalIgnoreCase);

    public static async Task<CreateInterWarehouseShipmentLineRequest[]> DetachWarehouseOrderLinksAsync(
        MikroWriteDbContext mikroWriteDbContext,
        CreateInterWarehouseShipmentRequest request,
        IReadOnlyList<CreateInterWarehouseShipmentLineRequest> lines,
        bool orderLinkingEnabled,
        CancellationToken cancellationToken)
    {
        if (orderLinkingEnabled)
        {
            return lines.ToArray();
        }

        if (!IsGreenGrocerSourceWarehouse(request.SourceWarehouseNo) ||
            lines.All(line => !line.WarehouseOrderLineGuid.HasValue))
        {
            return lines.ToArray();
        }

        var linkedStockCodes = lines
            .Where(line => line.WarehouseOrderLineGuid.HasValue)
            .Select(line => NormalizeStockCode(line.StockCode))
            .Where(stockCode => stockCode.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (linkedStockCodes.Length == 0)
        {
            return lines.ToArray();
        }

        var greenGrocerStockCodes = await mikroWriteDbContext.STOKLARs
            .AsNoTracking()
            .Where(stock =>
                linkedStockCodes.Contains(stock.sto_kod.Trim().ToUpper()) &&
                stock.sto_model_kodu != null &&
                GreenGrocerModelCodes.Contains(stock.sto_model_kodu.Trim()))
            .Select(stock => stock.sto_kod.Trim().ToUpper())
            .ToListAsync(cancellationToken);

        if (greenGrocerStockCodes.Count == 0)
        {
            return lines.ToArray();
        }

        var greenGrocerStockCodeSet = greenGrocerStockCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return lines
            .Select(line => line.WarehouseOrderLineGuid.HasValue &&
                            greenGrocerStockCodeSet.Contains(NormalizeStockCode(line.StockCode))
                ? line with { WarehouseOrderLineGuid = null }
                : line)
            .ToArray();
    }

    private static string NormalizeStockCode(string stockCode) =>
        string.IsNullOrWhiteSpace(stockCode) ? string.Empty : stockCode.Trim().ToUpperInvariant();
}
