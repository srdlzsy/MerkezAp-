using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri;

public sealed class BanknoteTrackQueryExecutor(MikroDbContext mikroDbContext)
{
    public async Task<IReadOnlyCollection<BanknoteTrackDto>> ListAsync(
        BanknoteTrackListRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var (date, nextDate) = NormalizeDateRange(request.DateToGet);

        const string sql = """
            SELECT
                t.Id AS BanknoteTrackId,
                t.WarehouseNo,
                COALESCE(w.dep_adi, '') AS WarehouseName,
                t.BanknoteTrackDate,
                t.TotalAmount,
                t.DeliveryTotalAmount,
                t.Deliverer,
                t.Receiver,
                t.CreateDate
            FROM BanknoteTracks t
            LEFT JOIN DEPOLAR w ON t.WarehouseNo = w.dep_no
            WHERE t.BanknoteTrackDate >= @date
              AND t.BanknoteTrackDate < @nextDate
              AND (@warehouseNo = 1 OR t.WarehouseNo = @warehouseNo)
            ORDER BY
                t.BanknoteTrackDate,
                t.WarehouseNo,
                t.CreateDate;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@date", date);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            Map,
            cancellationToken);
    }

    public async Task<BanknoteTrackDto?> GetAsync(
        BanknoteTrackDetailRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        const string sql = """
            SELECT
                t.Id AS BanknoteTrackId,
                t.WarehouseNo,
                COALESCE(w.dep_adi, '') AS WarehouseName,
                t.BanknoteTrackDate,
                t.TotalAmount,
                t.DeliveryTotalAmount,
                t.Deliverer,
                t.Receiver,
                t.CreateDate
            FROM BanknoteTracks t
            LEFT JOIN DEPOLAR w ON t.WarehouseNo = w.dep_no
            WHERE t.Id = @banknoteTrackId
              AND (@warehouseNo = 1 OR t.WarehouseNo = @warehouseNo);
            """;

        var items = await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@banknoteTrackId", request.BanknoteTrackId);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
            },
            Map,
            cancellationToken);

        return items.FirstOrDefault();
    }

    private static BanknoteTrackDto Map(DbDataReader reader)
    {
        var totalAmount = Round(ReadDouble(reader, "TotalAmount"));
        var deliveryTotalAmount = Round(ReadDouble(reader, "DeliveryTotalAmount"));

        return new BanknoteTrackDto(
            ReadGuid(reader, "BanknoteTrackId"),
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadDateTime(reader, "BanknoteTrackDate"),
            totalAmount,
            deliveryTotalAmount,
            Round(deliveryTotalAmount - totalAmount),
            ReadString(reader, "Deliverer"),
            ReadString(reader, "Receiver"),
            ReadDateTime(reader, "CreateDate"));
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        string sql,
        Action<DbCommand> configureCommand,
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
            command.CommandTimeout = 180;
            configureCommand(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(map(reader));
            }
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

    private static (DateTime Date, DateTime NextDate) NormalizeDateRange(DateTime dateToGet)
    {
        var date = dateToGet.Date;
        return (date, date.AddDays(1));
    }

    private static void Validate(BanknoteTrackListRequest request)
    {
        if (request.DateToGet == default)
        {
            throw new ArgumentException("Banknote track date is required.", nameof(request.DateToGet));
        }

        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }
    }

    private static void Validate(BanknoteTrackDetailRequest request)
    {
        if (request.BanknoteTrackId == Guid.Empty)
        {
            throw new ArgumentException("Banknote track id is required.", nameof(request.BanknoteTrackId));
        }

        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name]);

    private static Guid ReadGuid(DbDataReader reader, string name) =>
        reader[name] is DBNull ? Guid.Empty : (Guid)reader[name];

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static DateTime ReadDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
