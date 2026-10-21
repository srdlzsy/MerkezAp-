using System.Data;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.Common;

internal static class CompanyProductCompatibilityValidator
{
    internal static async Task ValidateAsync(
        DbContext dbContext,
        int warehouseNo,
        string customerCode,
        IEnumerable<string> stockCodes,
        CancellationToken cancellationToken)
    {
        var normalizedCustomerCode = customerCode.Trim();
        var normalizedStockCodes = stockCodes
            .Select(stockCode => stockCode?.Trim())
            .Where(stockCode => !string.IsNullOrWhiteSpace(stockCode))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCustomerCode.Length == 0 || normalizedStockCodes.Length == 0)
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            var stockParameters = normalizedStockCodes
                .Select((_, index) => $"@stockCode{index}")
                .ToArray();
            command.CommandText = $"""
                WITH RequestedStock AS
                (
                    SELECT StockCode
                    FROM (VALUES {string.Join(", ", stockParameters.Select(parameter => $"({parameter})"))}) AS requested(StockCode)
                )
                SELECT requested.StockCode
                FROM RequestedStock AS requested
                LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                    ON stock.sto_kod = requested.StockCode
                WHERE stock.sto_kod IS NULL
                   OR NOT
                  (
                      LTRIM(RTRIM(ISNULL(stock.sto_sat_cari_kod, N''))) = @customerCode
                      OR EXISTS
                      (
                          SELECT 1
                          FROM dbo.STOK_DEPO_DETAYLARI AS warehouseDetail WITH (NOLOCK)
                          WHERE warehouseDetail.sdp_depo_kod = stock.sto_kod
                            AND warehouseDetail.sdp_depo_no = @warehouseNo
                            AND LTRIM(RTRIM(ISNULL(warehouseDetail.sdp_sat_cari_kod, N''))) = @customerCode
                      )
                      OR EXISTS
                      (
                          SELECT 1
                          FROM dbo.SATINALMA_SARTLARI AS purchaseTerm WITH (NOLOCK)
                          WHERE purchaseTerm.sas_stok_kod = stock.sto_kod
                            AND purchaseTerm.sas_cari_kod = @customerCode
                            AND ISNULL(purchaseTerm.sas_iptal, 0) = 0
                            AND (purchaseTerm.sas_depo_no IS NULL OR purchaseTerm.sas_depo_no IN (0, @warehouseNo))
                            AND (purchaseTerm.sas_basla_tarih IS NULL OR purchaseTerm.sas_basla_tarih <= GETDATE())
                            AND
                            (
                                purchaseTerm.sas_bitis_tarih IS NULL
                                OR purchaseTerm.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                                OR purchaseTerm.sas_bitis_tarih >= CONVERT(date, GETDATE())
                            )
                      )
                  );
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;

            AddParameter(command, "@warehouseNo", warehouseNo, DbType.Int32);
            AddParameter(command, "@customerCode", normalizedCustomerCode, DbType.String);
            for (var index = 0; index < normalizedStockCodes.Length; index++)
            {
                AddParameter(command, stockParameters[index], normalizedStockCodes[index], DbType.String);
            }

            var incompatibleStockCodes = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                incompatibleStockCodes.Add(reader.GetString(0).Trim());
            }

            if (incompatibleStockCodes.Count > 0)
            {
                throw new ArgumentException(
                    $"Secili cari ile uyumlu olmayan urunler var: {string.Join(", ", incompatibleStockCodes.Distinct(StringComparer.OrdinalIgnoreCase))}.");
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(
        IDbCommand command,
        string name,
        object value,
        DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
