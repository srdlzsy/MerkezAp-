using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;

namespace FurpaMerkezApi.Infrastructure.Modules.Common;

internal static class SourceWarehouseOrderCatalogReader
{
    public static async Task<IReadOnlyDictionary<string, SourceWarehouseProcurementSnapshot>>
        ReadProcurementSourcesAsync(
            DbConnection connection,
            IReadOnlyCollection<string> stockCodes,
            int warehouseNo,
            CancellationToken cancellationToken)
    {
        if (stockCodes.Count == 0)
        {
            return new Dictionary<string, SourceWarehouseProcurementSnapshot>(
                StringComparer.OrdinalIgnoreCase);
        }

        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var sources = new Dictionary<string, ProcurementSourceAccumulator>(
                StringComparer.OrdinalIgnoreCase);
            using var command = connection.CreateCommand();
            var stockCodeParameterNames = stockCodes
                .Select((stockCode, index) =>
                {
                    var parameterName = $"@procurementStockCode{index}";
                    AddParameter(command, parameterName, NormalizeOrNull(stockCode), DbType.String);
                    return parameterName;
                })
                .ToArray();

            command.CommandText = $"""
                SELECT
                    LTRIM(RTRIM(stock.sto_kod)) AS StockCode,
                    LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N''))) AS ModelCode,
                    CASE
                        WHEN NULLIF(LTRIM(RTRIM(ISNULL(stock.sto_sat_cari_kod, N''))), N'') IS NOT NULL
                             OR EXISTS
                             (
                                 SELECT 1
                                 FROM dbo.SATINALMA_SARTLARI AS purchaseTerm WITH (NOLOCK)
                                 WHERE purchaseTerm.sas_stok_kod = stock.sto_kod
                                   AND ISNULL(purchaseTerm.sas_iptal, 0) = 0
                                   AND NULLIF(LTRIM(RTRIM(ISNULL(purchaseTerm.sas_cari_kod, N''))), N'') IS NOT NULL
                                   AND (purchaseTerm.sas_depo_no IS NULL OR purchaseTerm.sas_depo_no IN (0, @warehouseNo))
                                   AND (purchaseTerm.sas_basla_tarih IS NULL OR purchaseTerm.sas_basla_tarih <= GETDATE())
                                   AND
                                   (
                                       purchaseTerm.sas_bitis_tarih IS NULL
                                       OR purchaseTerm.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                                       OR purchaseTerm.sas_bitis_tarih >= CONVERT(date, GETDATE())
                                   )
                             )
                        THEN CAST(1 AS bit)
                        ELSE CAST(0 AS bit)
                    END AS HasPurchaseRequirement,
                    sourceWarehouse.dep_no AS SourceWarehouseNo,
                    sourceWarehouse.dep_adi AS SourceWarehouseName
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                OUTER APPLY
                (
                    SELECT warehouse.dep_no, warehouse.dep_adi
                    FROM dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                    CROSS APPLY STRING_SPLIT(
                        REPLACE(REPLACE(ISNULL(warehouse.dep_barkod_yazici_yolu, N''), N';', N','), N'|', N','),
                        N',') AS sourceModel
                    WHERE LTRIM(RTRIM(sourceModel.value)) = LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N'')))
                      AND ISNULL(stock.sto_iptal, 0) = 0
                      AND ISNULL(stock.sto_siparis_dursun, 0) = 0
                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.STOK_DEPO_DETAYLARI AS targetDetail WITH (NOLOCK)
                          WHERE targetDetail.sdp_depo_no = @warehouseNo
                            AND targetDetail.sdp_depo_kod = stock.sto_kod
                            AND ISNULL(targetDetail.sdp_sipdursun, 0) = 0
                      )
                      AND EXISTS
                      (
                          SELECT 1
                          FROM dbo.STOK_DEPO_DETAYLARI AS sourceDetail WITH (NOLOCK)
                          WHERE sourceDetail.sdp_depo_no = warehouse.dep_no
                            AND sourceDetail.sdp_depo_kod = stock.sto_kod
                            AND ISNULL(sourceDetail.sdp_sipdursun, 0) = 0
                      )
                ) AS sourceWarehouse
                WHERE stock.sto_kod IN ({string.Join(", ", stockCodeParameterNames)})
                ORDER BY stock.sto_kod, sourceWarehouse.dep_no;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            AddParameter(command, "@warehouseNo", warehouseNo, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var stockCode = ReadString(reader, "StockCode");
                if (string.IsNullOrWhiteSpace(stockCode))
                {
                    continue;
                }

                if (!sources.TryGetValue(stockCode, out var source))
                {
                    source = new ProcurementSourceAccumulator(
                        ReadString(reader, "ModelCode"),
                        ReadBool(reader, "HasPurchaseRequirement"));
                    sources[stockCode] = source;
                }

                if (reader["SourceWarehouseNo"] is not DBNull)
                {
                    source.AddWarehouse(new ProductSourceWarehouseDto(
                        Convert.ToInt32(reader["SourceWarehouseNo"]),
                        ReadString(reader, "SourceWarehouseName")));
                }
            }

            return sources.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToSnapshot(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static string ResolveProcurementType(
        bool hasSourceWarehouse,
        bool hasPurchaseRequirement) =>
        (hasSourceWarehouse, hasPurchaseRequirement) switch
        {
            (true, true) => "Mixed",
            (true, false) => "Warehouse",
            (false, true) => "Company",
            _ => "Unassigned"
        };

    public static async Task<IReadOnlySet<string>> ReadStockCodesAsync(
        DbConnection connection,
        int sourceWarehouseNo,
        string? stockCode,
        CancellationToken cancellationToken,
        int? targetWarehouseNo = null)
    {
        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DISTINCT
                    LTRIM(RTRIM(stock.sto_kod)) AS StockCode
                FROM dbo.DEPOLAR AS sourceWarehouse WITH (NOLOCK)
                CROSS APPLY STRING_SPLIT(
                    REPLACE(REPLACE(ISNULL(sourceWarehouse.dep_barkod_yazici_yolu, N''), N';', N','), N'|', N','),
                    N',') AS sourceModel
                INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                    ON LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N''))) = LTRIM(RTRIM(sourceModel.value))
                INNER JOIN dbo.STOK_DEPO_DETAYLARI AS sourceDetail WITH (NOLOCK)
                    ON sourceDetail.sdp_depo_no = sourceWarehouse.dep_no
                   AND sourceDetail.sdp_depo_kod = stock.sto_kod
                   AND ISNULL(sourceDetail.sdp_sipdursun, 0) = 0
                WHERE sourceWarehouse.dep_no = @sourceWarehouseNo
                  AND LTRIM(RTRIM(sourceModel.value)) <> N''
                  AND ISNULL(stock.sto_iptal, 0) = 0
                  AND ISNULL(stock.sto_siparis_dursun, 0) = 0
                  AND (@stockCode IS NULL OR stock.sto_kod = @stockCode)
                  AND (
                      @targetWarehouseNo IS NULL
                      OR EXISTS
                      (
                          SELECT 1
                          FROM dbo.STOK_DEPO_DETAYLARI AS targetDetail WITH (NOLOCK)
                          WHERE targetDetail.sdp_depo_no = @targetWarehouseNo
                            AND targetDetail.sdp_depo_kod = stock.sto_kod
                            AND ISNULL(targetDetail.sdp_sipdursun, 0) = 0
                      )
                  );
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;

            AddParameter(command, "@sourceWarehouseNo", sourceWarehouseNo, DbType.Int32);
            AddParameter(command, "@stockCode", NormalizeOrNull(stockCode), DbType.String);
            AddParameter(command, "@targetWarehouseNo", targetWarehouseNo, DbType.Int32);

            var stockCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var value = reader["StockCode"] is DBNull
                    ? null
                    : Convert.ToString(reader["StockCode"])?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    stockCodes.Add(value);
                }
            }

            return stockCodes;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static async Task<bool> IsStockOrderableAsync(
        DbConnection connection,
        int sourceWarehouseNo,
        string stockCode,
        CancellationToken cancellationToken,
        int? targetWarehouseNo = null)
    {
        var normalizedStockCode = NormalizeOrNull(stockCode);
        if (sourceWarehouseNo <= 0 || normalizedStockCode is null)
        {
            return false;
        }

        var stockCodes = await ReadStockCodesAsync(
            connection,
            sourceWarehouseNo,
            normalizedStockCode,
            cancellationToken,
            targetWarehouseNo);
        return stockCodes.Contains(normalizedStockCode);
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static bool ReadBool(DbDataReader reader, string name) =>
        reader[name] is not DBNull && Convert.ToBoolean(reader[name]);

    private sealed class ProcurementSourceAccumulator(
        string modelCode,
        bool hasPurchaseRequirement)
    {
        private readonly Dictionary<int, ProductSourceWarehouseDto> sourceWarehouses = new();

        public void AddWarehouse(ProductSourceWarehouseDto warehouse) =>
            sourceWarehouses.TryAdd(warehouse.WarehouseNo, warehouse);

        public SourceWarehouseProcurementSnapshot ToSnapshot() => new(
            modelCode,
            hasPurchaseRequirement,
            sourceWarehouses.Values
                .OrderBy(warehouse => warehouse.WarehouseNo)
                .ToArray());
    }
}

internal sealed record SourceWarehouseProcurementSnapshot(
    string ModelCode,
    bool HasPurchaseRequirement,
    IReadOnlyCollection<ProductSourceWarehouseDto> SourceWarehouses)
{
    public static readonly SourceWarehouseProcurementSnapshot Empty = new(
        string.Empty,
        false,
        Array.Empty<ProductSourceWarehouseDto>());
}
