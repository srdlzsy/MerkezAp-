using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductLatestTag;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ProductLatestTag;

public sealed class GetProductLatestTagUseCase(MikroDbContext mikroDbContext) : IGetProductLatestTagUseCase
{
    public async Task<ProductLatestTagDto?> ExecuteAsync(
        ProductLatestTagRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var stockCode = NormalizeOrNull(request.StockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(request.StockCode));

        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (1)
                    vw.[BranchNo],
                    COALESCE(vw.[BranchName], '') AS BranchName,
                    COALESCE(vw.[ProductionCity], '') AS ProductionCity,
                    s.sto_kod AS StockCode,
                    COALESCE(s.sto_isim, '') AS StockName,
                    dbo.[fn_StokSatisFiyati](s.sto_kod, '1', vw.[BranchNo], '1') AS SalesPrice,
                    COALESCE(vw.[ProductionDistrict], '') AS ProductionDistrict,
                    COALESCE(vw.[ProductName], '') AS ProductName,
                    COALESCE(vw.[GoodsType], '') AS GoodsType,
                    COALESCE(vw.[GoodsGenus], '') AS GoodsGenus,
                    vw.[Quantity],
                    COALESCE(vw.[TakenTag], '') AS TakenTag,
                    COALESCE(vw.[Buyer], '') AS Buyer,
                    vw.[ProductionDate],
                    vw.[BuyingPrice],
                    vw.[ShippingDate],
                    COALESCE(vw.[Manufacturer], '') AS Manufacturer,
                    COALESCE(vw.[ProductUnit], '') AS ProductUnit
                FROM [Furpa].[dbo].[VwKunyeNet] vw WITH (NOLOCK)
                INNER JOIN [KUNYENET].[dbo].[FaturaIslem] fi WITH (NOLOCK)
                    ON fi.Kunye = vw.[TakenTag]
                INNER JOIN [KUNYENET].[dbo].[MuhStok] ms WITH (NOLOCK)
                    ON ms.Stokid = fi.StokId
                INNER JOIN dbo.STOKLAR s WITH (NOLOCK)
                    ON s.sto_kod = ms.StokKodu
                WHERE
                    vw.ShippingDate <= GETDATE()
                    AND s.sto_kod = @stockCode
                    AND vw.BranchNo = @warehouseNo
                ORDER BY vw.ShippingDate DESC;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;

            AddParameter(command, "@stockCode", stockCode);
            AddParameter(command, "@warehouseNo", request.WarehouseNo);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new ProductLatestTagDto(
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                ReadString(reader, "ProductionCity"),
                ReadString(reader, "StockCode"),
                ReadString(reader, "StockName"),
                ReadDouble(reader, "SalesPrice"),
                ReadString(reader, "ProductionDistrict"),
                ReadString(reader, "ProductName"),
                ReadString(reader, "GoodsType"),
                ReadString(reader, "GoodsGenus"),
                ReadDouble(reader, "Quantity"),
                ReadString(reader, "TakenTag"),
                ReadString(reader, "Buyer"),
                ReadDateTime(reader, "ProductionDate"),
                ReadDouble(reader, "BuyingPrice"),
                ReadDateTime(reader, "ShippingDate"),
                ReadString(reader, "Manufacturer"),
                ReadString(reader, "ProductUnit"));
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name]);

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static DateTime ReadDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
}
