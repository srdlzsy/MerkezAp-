namespace FurpaMerkezApi.Application.Common.Errors;

public sealed class CompanyProductCompatibilityException : ArgumentException
{
    public CompanyProductCompatibilityException(
        string operationName,
        int warehouseNo,
        string customerCode,
        DateTime validationDate,
        IEnumerable<string> stockCodes)
        : base(BuildMessage(
            operationName,
            warehouseNo,
            customerCode,
            validationDate,
            stockCodes))
    {
        OperationName = operationName.Trim();
        WarehouseNo = warehouseNo;
        CustomerCode = customerCode.Trim();
        ValidationDate = validationDate.Date;
        StockCodes = stockCodes
            .Select(stockCode => stockCode.Trim())
            .Where(stockCode => stockCode.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(stockCode => stockCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string OperationName { get; }

    public int WarehouseNo { get; }

    public string CustomerCode { get; }

    public DateTime ValidationDate { get; }

    public IReadOnlyList<string> StockCodes { get; }

    private static string BuildMessage(
        string operationName,
        int warehouseNo,
        string customerCode,
        DateTime validationDate,
        IEnumerable<string> stockCodes)
    {
        var normalizedOperationName = operationName.Trim();
        var normalizedStockCodes = stockCodes
            .Select(stockCode => stockCode.Trim())
            .Where(stockCode => stockCode.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(stockCode => stockCode, StringComparer.OrdinalIgnoreCase);

        return $"{normalizedOperationName} durduruldu. Cari {customerCode.Trim()} ve depo {warehouseNo} icin " +
               $"{validationDate:dd.MM.yyyy} tarihinde gecerli stok/cari eslesmesi veya aktif satin alma sarti " +
               $"bulunmayan urunler: {string.Join(", ", normalizedStockCodes)}. Mikro'ya evrak yazilmadi. " +
               "Satin alma sartini ya da stok kartindaki tedarikci tanimini kontrol edip ayni taslagi yeniden kaydedin.";
    }
}
