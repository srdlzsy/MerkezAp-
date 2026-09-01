using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchProducts;

public sealed class SearchProductsUseCase(MikroDbContext mikroDbContext) : ISearchProductsUseCase
{
    private const int DefaultTake = 20;
    private const int MaxTake = 100;

    public async Task<IReadOnlyCollection<ProductLookupItemDto>> ExecuteAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var requestedBarcode = NormalizeOrNull(request.Barcode);
        var barcodeLookup = requestedBarcode is null
            ? null
            : BarcodeLookupNormalizer.Normalize(requestedBarcode);
        var stockCode = NormalizeOrNull(request.StockCode);
        var stockName = NormalizeOrNull(request.StockName);
        var supplierCode = NormalizeOrNull(request.SupplierCode);

        if (barcodeLookup is null && stockCode is null && stockName is null && supplierCode is null)
        {
            throw new ArgumentException("At least one product search filter is required.");
        }

        if (stockName is { Length: < 2 })
        {
            throw new ArgumentException("Stock name search text must be at least 2 characters.", nameof(request.StockName));
        }

        var take = NormalizeTake(request.Take);
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var products = barcodeLookup is null
                ? await ReadProductsAsync(
                    connection,
                    request.WarehouseNo,
                    null,
                    stockCode,
                    stockName,
                    supplierCode,
                    take,
                    null,
                    cancellationToken)
                : await ReadProductsByBarcodeCandidatesAsync(
                    connection,
                    request.WarehouseNo,
                    barcodeLookup,
                    stockCode,
                    stockName,
                    supplierCode,
                    take,
                    cancellationToken);

            if (products.Count == 0 &&
                barcodeLookup is null &&
                IsDigitsOnly(stockCode))
            {
                var fallbackBarcodeLookup = BarcodeLookupNormalizer.Normalize(stockCode!);
                products = await ReadProductsByBarcodeCandidatesAsync(
                    connection,
                    request.WarehouseNo,
                    fallbackBarcodeLookup,
                    null,
                    null,
                    supplierCode,
                    take,
                    cancellationToken);
            }

            if (products.Count == 0 &&
                barcodeLookup is null &&
                stockCode is null &&
                IsDigitsOnly(stockName))
            {
                var fallbackBarcodeLookup = BarcodeLookupNormalizer.Normalize(stockName!);
                products = await ReadProductsByBarcodeCandidatesAsync(
                    connection,
                    request.WarehouseNo,
                    fallbackBarcodeLookup,
                    null,
                    null,
                    supplierCode,
                    take,
                    cancellationToken);

                if (products.Count == 0)
                {
                    products = await ReadProductsAsync(
                        connection,
                        request.WarehouseNo,
                        null,
                        stockName,
                        null,
                        supplierCode,
                        take,
                        null,
                        cancellationToken);
                }
            }

            return products;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> ReadProductsByBarcodeCandidatesAsync(
        DbConnection connection,
        int warehouseNo,
        BarcodeLookupInfo barcodeLookup,
        string? stockCode,
        string? stockName,
        string? supplierCode,
        int take,
        CancellationToken cancellationToken)
    {
        foreach (var barcode in BarcodeLookupNormalizer.GetLookupCandidates(barcodeLookup))
        {
            var products = await ReadProductsAsync(
                connection,
                warehouseNo,
                barcode,
                stockCode,
                stockName,
                supplierCode,
                take,
                barcodeLookup,
                cancellationToken);

            if (products.Count > 0)
            {
                return products;
            }
        }

        return Array.Empty<ProductLookupItemDto>();
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> ReadProductsAsync(
        DbConnection connection,
        int warehouseNo,
        string? barcode,
        string? stockCode,
        string? stockName,
        string? supplierCode,
        int take,
        BarcodeLookupInfo? barcodeLookup,
        CancellationToken cancellationToken)
    {
        var products = new List<ProductLookupItemDto>(take);

        using var command = connection.CreateCommand();
        command.CommandText = "dbo.__StokveFiyatArama_Gokhan";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 300;

        AddParameter(command, "@sfiyat_deposirano", warehouseNo);
        AddParameter(command, "@bar_kodu", barcode);
        AddParameter(command, "@sfiyat_stokkod", stockCode);
        AddParameter(command, "@sto_isim", stockName);
        AddParameter(command, "@tedarikci", supplierCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (products.Count < take && await reader.ReadAsync(cancellationToken))
        {
            var salesBlockCode = ReadNullableInt(reader, "SatisDursun");
            var orderBlockCode = ReadNullableInt(reader, "SipDursun");
            var goodsAcceptanceBlockCode = ReadNullableInt(reader, "MalKabulDursun");
            var primaryUnitMultiplier = ReadDouble(reader, "BirimKatsayisi");
            var secondaryUnitMultiplier = NormalizeUnitMultiplier(ReadDouble(reader, "BirimKatsayisi2"));

            products.Add(new ProductLookupItemDto(
                ReadInt(reader, "DepoNo"),
                ReadString(reader, "BarKodu"),
                ReadString(reader, "StokKod"),
                ReadString(reader, "StokIsim"),
                ReadDouble(reader, "Fiyati"),
                ReadInt(reader, "FiyatTipKodu"),
                ReadString(reader, "BirimAd"),
                secondaryUnitMultiplier > 0d ? secondaryUnitMultiplier : NormalizeUnitMultiplier(primaryUnitMultiplier, 1d),
                ReadString(reader, "BirimAd2"),
                secondaryUnitMultiplier,
                salesBlockCode,
                orderBlockCode,
                goodsAcceptanceBlockCode,
                IsBlocked(salesBlockCode),
                IsBlocked(orderBlockCode),
                IsBlocked(goodsAcceptanceBlockCode),
                ReadString(reader, "UrunSorumlusu"),
                barcodeLookup?.OriginalBarcode,
                barcodeLookup?.LookupBarcode,
                barcodeLookup?.IsVariableWeightBarcode ?? false,
                barcodeLookup?.EmbeddedQuantity,
                barcodeLookup?.EmbeddedQuantityUnit,
                barcodeLookup?.IsCheckDigitValid));
        }

        return products;
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsDigitsOnly(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);

    private static bool IsBlocked(int? code) =>
        code.GetValueOrDefault() != 0;

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        Convert.ToInt32(reader[name]);

    private static int? ReadNullableInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static double NormalizeUnitMultiplier(double value, double fallback = 0d)
    {
        var normalized = Math.Abs(value);
        return normalized > 0d ? normalized : fallback;
    }

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
}
