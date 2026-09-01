using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.SourceProducts;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.SourceProducts;

public sealed class SuggestedWarehouseSourceProductsUseCase(MikroDbContext mikroDbContext)
    : ISuggestedWarehouseSourceProductsUseCase
{
    public async Task<IReadOnlyCollection<SuggestedWarehouseSourceProductDto>> ExecuteAsync(
        int sourceWarehouseNo,
        CancellationToken cancellationToken)
    {
        if (sourceWarehouseNo <= 0)
        {
            throw new ArgumentException("Source warehouse no must be greater than zero.", nameof(sourceWarehouseNo));
        }

        const string sql = """
            DECLARE @SourceWarehouseName nvarchar(50);
            DECLARE @SourceModelCodes nvarchar(100);

            SELECT
                @SourceWarehouseName = dep_adi,
                @SourceModelCodes = dep_barkod_yazici_yolu
            FROM dbo.DEPOLAR WITH (NOLOCK)
            WHERE dep_no = @sourceWarehouseNo;

            IF @SourceWarehouseName IS NULL
            BEGIN
                THROW 50001, 'Secilen kaynak depo bulunamadi.', 1;
            END;

            SET @SourceModelCodes = REPLACE(REPLACE(ISNULL(@SourceModelCodes, N''), N';', N','), N'|', N',');

            IF NULLIF(LTRIM(RTRIM(@SourceModelCodes)), N'') IS NULL
            BEGIN
                THROW 50002, 'Secilen kaynak depo icin model kodlari tanimli degil.', 1;
            END;

            ;WITH SourceModels AS (
                SELECT DISTINCT LTRIM(RTRIM(value)) AS ModelCode
                FROM STRING_SPLIT(@SourceModelCodes, N',')
                WHERE LTRIM(RTRIM(value)) <> N''
            )
            SELECT
                @sourceWarehouseNo AS SourceWarehouseNo,
                ISNULL(@SourceWarehouseName, N'') AS SourceWarehouseName,
                stock.sto_kod AS StockCode,
                stock.sto_isim AS StockName,
                stock.sto_model_kodu AS ModelCode,
                stock.sto_birim1_ad AS UnitName,
                ISNULL(stock.sto_birim2_ad, N'') AS SecondaryUnitName,
                ISNULL(stock.sto_birim2_katsayi, 0) AS PackageFactor,
                ISNULL(barcode.bar_kodu, N'') AS Barcode,
                ISNULL(caseBarcode.bar_kodu, N'') AS CaseBarcode
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            INNER JOIN SourceModels AS model
                ON model.ModelCode = LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N'')))
            OUTER APPLY (
                SELECT TOP 1 barcode.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                WHERE barcode.bar_stokkodu = stock.sto_kod
                  AND barcode.bar_birimpntr = 1
                ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_create_date DESC
            ) AS barcode
            OUTER APPLY (
                SELECT TOP 1 barcode.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                WHERE barcode.bar_stokkodu = stock.sto_kod
                  AND ISNULL(barcode.bar_birimpntr, 1) <> 1
                ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_birimpntr DESC, barcode.bar_create_date DESC
            ) AS caseBarcode
            WHERE ISNULL(stock.sto_iptal, 0) = 0
              AND ISNULL(stock.sto_siparis_dursun, 0) = 0
              AND stock.sto_kod IS NOT NULL
              AND stock.sto_isim IS NOT NULL
              AND stock.sto_isim NOT LIKE N'DLS%'
              AND EXISTS (
                  SELECT 1
                  FROM dbo.STOK_DEPO_DETAYLARI AS sourceDetail WITH (NOLOCK)
                  WHERE sourceDetail.sdp_depo_no = @sourceWarehouseNo
                    AND sourceDetail.sdp_depo_kod = stock.sto_kod
                    AND ISNULL(sourceDetail.sdp_sipdursun, 0) = 0
              )
            ORDER BY stock.sto_model_kodu, stock.sto_isim, stock.sto_kod;
            """;

        return await ExecuteReaderAsync(
            sql,
            command => AddParameter(command, "@sourceWarehouseNo", sourceWarehouseNo, DbType.Int32),
            ReadItem,
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        string sql,
        Action<DbCommand> configure,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            configure(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(map(reader));
            }
        }
        catch (SqlException exception) when (IsBusinessRuleSqlException(exception))
        {
            throw new ArgumentException(exception.Message, exception);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return items;
    }

    private static SuggestedWarehouseSourceProductDto ReadItem(DbDataReader reader)
    {
        var modelCode = ReadString(reader, "ModelCode");

        return new SuggestedWarehouseSourceProductDto(
            Convert.ToInt32(reader["SourceWarehouseNo"]),
            ReadString(reader, "SourceWarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            modelCode,
            GetModelName(modelCode),
            ReadString(reader, "UnitName"),
            ReadString(reader, "SecondaryUnitName"),
            NormalizeUnitMultiplier(ReadDouble(reader, "PackageFactor")),
            ReadString(reader, "Barcode"),
            ReadString(reader, "CaseBarcode"),
            0,
            0,
            0,
            1);
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static double NormalizeUnitMultiplier(double value)
    {
        var normalized = Math.Abs(value);
        return normalized > 0d ? normalized : 0d;
    }

    private static bool IsBusinessRuleSqlException(SqlException exception) =>
        exception.Number is 50001 or 50002;

    private static string GetModelName(string modelCode) =>
        modelCode.Trim() switch
        {
            "10" => "Meyve",
            "11" => "Sebze",
            "12" => "Yesillik",
            "23" => "Manav Sarf",
            _ => modelCode
        };
}
