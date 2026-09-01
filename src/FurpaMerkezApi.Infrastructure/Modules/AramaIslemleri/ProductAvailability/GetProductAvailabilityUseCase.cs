using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductAvailability;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ProductAvailability;

public sealed class GetProductAvailabilityUseCase(
    ISearchProductsUseCase searchProductsUseCase,
    MikroDbContext mikroDbContext) : IGetProductAvailabilityUseCase
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public async Task<IReadOnlyCollection<ProductAvailabilityItemDto>> ExecuteAsync(
        ProductAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (IsBlank(request.Barcode) && IsBlank(request.StockCode) && IsBlank(request.StockName))
        {
            throw new ArgumentException("At least one product availability filter is required.");
        }

        if (NormalizeOrNull(request.StockName) is { Length: < 2 })
        {
            throw new ArgumentException("Stock name search text must be at least 2 characters.", nameof(request.StockName));
        }

        var take = NormalizeTake(request.Take);
        var products = await searchProductsUseCase.ExecuteAsync(
            new ProductSearchRequest(
                request.WarehouseNo,
                request.Barcode,
                request.StockCode,
                request.StockName,
                null,
                take),
            cancellationToken);

        if (products.Count == 0)
        {
            return Array.Empty<ProductAvailabilityItemDto>();
        }

        var stockCodes = products
            .Select(product => product.StockCode)
            .Where(stockCode => !string.IsNullOrWhiteSpace(stockCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var balances = await ReadStockBalancesAsync(request.WarehouseNo, stockCodes, cancellationToken);
        var warehouseName = await ReadWarehouseNameAsync(request.WarehouseNo, cancellationToken);

        return products
            .Select(product =>
            {
                var currentStockQuantity = balances.GetValueOrDefault(product.StockCode);

                return new ProductAvailabilityItemDto(
                    product.WarehouseNo,
                    warehouseName,
                    product.Barcode,
                    product.StockCode,
                    product.StockName,
                    product.UnitName,
                    Round(currentStockQuantity),
                    Math.Abs(currentStockQuantity) > 0.000001d,
                    product.Price,
                    product.PriceTypeCode,
                    product.UnitMultiplier,
                    product.SecondaryUnitName,
                    product.SecondaryUnitMultiplier,
                    product.SalesBlockCode,
                    product.OrderBlockCode,
                    product.GoodsAcceptanceBlockCode,
                    product.IsSalesBlocked,
                    product.IsOrderBlocked,
                    product.IsGoodsAcceptanceBlocked,
                    product.ProductManagerCode,
                    product.RequestedBarcode,
                    product.LookupBarcode,
                    product.IsVariableWeightBarcode,
                    product.EmbeddedQuantity,
                    product.EmbeddedQuantityUnit,
                    product.IsBarcodeCheckDigitValid);
            })
            .ToArray();
    }

    private async Task<Dictionary<string, double>> ReadStockBalancesAsync(
        int warehouseNo,
        IReadOnlyCollection<string> stockCodes,
        CancellationToken cancellationToken)
    {
        if (stockCodes.Count == 0)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }

        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandTimeout = 300;
            command.CommandType = CommandType.Text;
            command.CommandText = CreateBalanceSql(stockCodes.Count);

            AddParameter(command, "@warehouseNo", warehouseNo);

            var index = 0;
            foreach (var stockCode in stockCodes)
            {
                AddParameter(command, $"@stockCode{index}", stockCode);
                index++;
            }

            var balances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                balances[ReadString(reader, "StockCode")] = ReadDouble(reader, "CurrentStockQuantity");
            }

            return balances;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<string> ReadWarehouseNameAsync(int warehouseNo, CancellationToken cancellationToken)
    {
        var warehouseName = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(warehouse => warehouse.dep_no == warehouseNo)
            .Select(warehouse => warehouse.dep_adi ?? string.Empty)
            .FirstOrDefaultAsync(cancellationToken);

        return warehouseName ?? string.Empty;
    }

    private static string CreateBalanceSql(int stockCodeCount)
    {
        var rows = Enumerable
            .Range(0, stockCodeCount)
            .Select(index => $"SELECT @stockCode{index} AS StockCode");

        return $"""
            ;WITH StockCodes AS (
                {string.Join($"{Environment.NewLine}                UNION ALL{Environment.NewLine}                ", rows)}
            )
            SELECT
                StockCode,
                COALESCE(dbo.fn_DepodakiMiktar(StockCode, @warehouseNo, CONVERT(date, GETDATE())), 0) AS CurrentStockQuantity
            FROM StockCodes;
            """;
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsBlank(string? value) =>
        string.IsNullOrWhiteSpace(value);

    private static double Round(double value) =>
        Math.Round(value, 8, MidpointRounding.AwayFromZero);

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
}
