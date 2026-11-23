namespace FurpaMerkezApi.Application.Common.Errors;

public sealed class CompanyProductAvailabilityException : ArgumentException
{
    public CompanyProductAvailabilityException(
        string operationName,
        int warehouseNo,
        IEnumerable<string> stockCodes)
        : base(BuildMessage(operationName, warehouseNo, stockCodes))
    {
        OperationName = operationName.Trim();
        WarehouseNo = warehouseNo;
        StockCodes = NormalizeStockCodes(stockCodes);
    }

    public string OperationName { get; }

    public int WarehouseNo { get; }

    public IReadOnlyList<string> StockCodes { get; }

    private static string BuildMessage(
        string operationName,
        int warehouseNo,
        IEnumerable<string> stockCodes)
    {
        var normalizedStockCodes = NormalizeStockCodes(stockCodes);
        return $"{operationName.Trim()} durduruldu. Depo {warehouseNo} icin pasif veya DLS durumundaki " +
               $"urunler kullanilamaz: {string.Join(", ", normalizedStockCodes)}. Mikro'ya evrak yazilmadi. " +
               "Stok kartini kontrol edip bu urunleri cikardiktan sonra ayni taslagi yeniden kaydedin.";
    }

    private static string[] NormalizeStockCodes(IEnumerable<string> stockCodes) =>
        stockCodes
            .Select(stockCode => stockCode.Trim())
            .Where(stockCode => stockCode.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(stockCode => stockCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
