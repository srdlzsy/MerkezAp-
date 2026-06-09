using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.MobileSync.WarehouseCatalog;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.MobileSync.WarehouseCatalog;

public sealed class GetMobileWarehouseCatalogUseCase(MikroDbContext mikroDbContext)
    : IGetMobileWarehouseCatalogUseCase
{
    private const int DefaultPageSize = 1000;
    private const int MaxPageSize = 10000;

    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MobileWarehouseCatalogResponse> ExecuteAsync(
        MobileWarehouseCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var pageSize = NormalizePageSize(request.PageSize);
        var takePlusOne = pageSize + 1;
        var cursor = DecodeCursor(request.Cursor);
        var effectiveSince = cursor?.Since ?? request.Since;
        var syncUpperBound = cursor?.SyncUpperBound ?? DateTime.Now;
        var rows = new List<MobileWarehouseCatalogItemDto>(takePlusOne);

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
                WITH CatalogRows AS
                (
                    SELECT
                        warehouse.dep_no AS WarehouseNo,
                        COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                        warehouse.dep_firmano AS CompanyNo,
                        warehouse.dep_subeno AS BranchNo,
                        COALESCE(warehouse.dep_grup_kodu, '') AS GroupCode,
                        warehouse.dep_tipi AS WarehouseType,
                        COALESCE(warehouse.dep_sor_mer_kodu, '') AS ResponsibilityCenterCode,
                        COALESCE(warehouse.dep_proje_kodu, '') AS ProjectCode,
                        COALESCE(warehouse.dep_cadde, '') AS Street,
                        COALESCE(warehouse.dep_mahalle, '') AS Neighborhood,
                        COALESCE(warehouse.dep_sokak, '') AS Avenue,
                        COALESCE(warehouse.dep_Ilce, '') AS District,
                        COALESCE(warehouse.dep_Il, '') AS Province,
                        COALESCE(warehouse.dep_envanter_harici_fl, 0) AS IsInventoryExcluded,
                        COALESCE(warehouse.dep_iptal, 0) AS IsDeleted,
                        COALESCE(warehouse.dep_lastup_date, warehouse.dep_create_date) AS UpdatedAt
                    FROM dbo.DEPOLAR warehouse WITH (NOLOCK)
                    WHERE warehouse.dep_no IS NOT NULL
                        AND warehouse.dep_no > 0
                )
                SELECT TOP (@takePlusOne)
                    WarehouseNo,
                    WarehouseName,
                    CompanyNo,
                    BranchNo,
                    GroupCode,
                    WarehouseType,
                    ResponsibilityCenterCode,
                    ProjectCode,
                    Street,
                    Neighborhood,
                    Avenue,
                    District,
                    Province,
                    IsInventoryExcluded,
                    IsDeleted,
                    UpdatedAt
                FROM CatalogRows
                WHERE
                    UpdatedAt <= @syncUpperBound
                    AND (@since IS NULL OR UpdatedAt > @since)
                    AND (IsDeleted = 0 OR @since IS NOT NULL)
                    AND (@cursorWarehouseNo IS NULL OR WarehouseNo > @cursorWarehouseNo)
                ORDER BY WarehouseNo;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;

            AddParameter(command, "@since", effectiveSince, DbType.DateTime2);
            AddParameter(command, "@syncUpperBound", syncUpperBound, DbType.DateTime2);
            AddParameter(command, "@takePlusOne", takePlusOne, DbType.Int32);
            AddParameter(command, "@cursorWarehouseNo", cursor?.WarehouseNo, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadItem(reader));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var nextCursor = hasMore && rows.Count > 0
            ? EncodeCursor(rows[^1], effectiveSince, syncUpperBound)
            : null;
        var deletedWarehouseNos = rows
            .Where(item => item.IsDeleted)
            .Select(item => item.WarehouseNo)
            .Distinct()
            .ToArray();
        DateTime? syncToken = hasMore ? null : syncUpperBound;

        return new MobileWarehouseCatalogResponse(
            syncUpperBound,
            effectiveSince,
            syncToken,
            nextCursor,
            hasMore,
            pageSize,
            rows,
            deletedWarehouseNos);
    }

    private static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static MobileWarehouseCatalogItemDto ReadItem(DbDataReader reader)
    {
        return new MobileWarehouseCatalogItemDto(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadNullableInt(reader, "CompanyNo"),
            ReadNullableInt(reader, "BranchNo"),
            ReadString(reader, "GroupCode"),
            ReadNullableByte(reader, "WarehouseType"),
            ReadString(reader, "ResponsibilityCenterCode"),
            ReadString(reader, "ProjectCode"),
            JoinNonEmpty(
                ReadString(reader, "Street"),
                ReadString(reader, "Neighborhood"),
                ReadString(reader, "Avenue")),
            ReadString(reader, "District"),
            ReadString(reader, "Province"),
            ReadBool(reader, "IsInventoryExcluded"),
            ReadBool(reader, "IsDeleted"),
            ReadDateTime(reader, "UpdatedAt"));
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name]);

    private static int? ReadNullableInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);

    private static byte? ReadNullableByte(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToByte(reader[name]);

    private static bool ReadBool(DbDataReader reader, string name) =>
        reader[name] is not DBNull && Convert.ToBoolean(reader[name]);

    private static DateTime ReadDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static string JoinNonEmpty(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

    private static string EncodeCursor(
        MobileWarehouseCatalogItemDto item,
        DateTime? since,
        DateTime syncUpperBound)
    {
        var cursor = new WarehouseCatalogCursor(item.WarehouseNo, since, syncUpperBound);
        var json = JsonSerializer.Serialize(cursor, CursorJsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static WarehouseCatalogCursor? DecodeCursor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var base64 = value.Trim()
                .Replace('-', '+')
                .Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            var cursor = JsonSerializer.Deserialize<WarehouseCatalogCursor>(json, CursorJsonOptions);

            return cursor is null || cursor.WarehouseNo <= 0
                ? throw new ArgumentException("Cursor is invalid.", nameof(value))
                : cursor;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Cursor is invalid.", nameof(value), exception);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Cursor is invalid.", nameof(value), exception);
        }
    }

    private sealed record WarehouseCatalogCursor(
        int WarehouseNo,
        DateTime? Since,
        DateTime SyncUpperBound);
}
