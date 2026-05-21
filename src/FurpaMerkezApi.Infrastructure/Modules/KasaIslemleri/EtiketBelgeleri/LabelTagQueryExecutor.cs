using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class LabelTagQueryExecutor(FurpaDbContext furpaDbContext)
{
    internal async Task<IReadOnlyCollection<LabelTagDto>> ExecuteAsync(
        LabelTagListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var date = request.DateToGet.Date;
        var nextDate = date.AddDays(1);
        var tags = new List<LabelTagDto>();
        var connection = furpaDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    BranchNo,
                    COALESCE(BranchName, '') AS BranchName,
                    COALESCE(ProductionCity, '') AS ProductionCity,
                    COALESCE(ProductionDistrict, '') AS ProductionDistrict,
                    ProductName,
                    GoodsType,
                    GoodsGenus,
                    Quantity,
                    TakenTag,
                    Buyer,
                    ProductionDate,
                    BuyingPrice,
                    ShippingDate,
                    COALESCE(Manufacturer, '') AS Manufacturer
                FROM dbo.VwKunyeNet
                WHERE BranchNo = @warehouseNo
                  AND ShippingDate >= @date
                  AND ShippingDate < @nextDate
                ORDER BY ProductName, ProductionDate;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;

            AddParameter(command, "@warehouseNo", request.WarehouseNo);
            AddParameter(command, "@date", date);
            AddParameter(command, "@nextDate", nextDate);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                tags.Add(new LabelTagDto
                {
                    BranchNo = ReadInt(reader, "BranchNo"),
                    BranchName = ReadString(reader, "BranchName"),
                    ProductionCity = ReadString(reader, "ProductionCity"),
                    ProductionDistrict = ReadString(reader, "ProductionDistrict"),
                    ProductName = ReadString(reader, "ProductName"),
                    GoodsType = ReadString(reader, "GoodsType"),
                    GoodsGenus = ReadString(reader, "GoodsGenus"),
                    Quantity = ReadDouble(reader, "Quantity"),
                    TakenTag = ReadString(reader, "TakenTag"),
                    Buyer = ReadString(reader, "Buyer"),
                    ProductionDate = ReadDateTime(reader, "ProductionDate"),
                    BuyingPrice = ReadDouble(reader, "BuyingPrice"),
                    ShippingDate = ReadDateTime(reader, "ShippingDate"),
                    Manufacturer = ReadString(reader, "Manufacturer")
                });
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return tags;
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
