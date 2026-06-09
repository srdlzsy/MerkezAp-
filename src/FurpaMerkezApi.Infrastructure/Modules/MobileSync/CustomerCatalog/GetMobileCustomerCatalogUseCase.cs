using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.MobileSync.CustomerCatalog;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.MobileSync.CustomerCatalog;

public sealed class GetMobileCustomerCatalogUseCase(MikroDbContext mikroDbContext)
    : IGetMobileCustomerCatalogUseCase
{
    private const int DefaultPageSize = 5000;
    private const int MaxPageSize = 10000;

    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MobileCustomerCatalogResponse> ExecuteAsync(
        MobileCustomerCatalogRequest request,
        CancellationToken cancellationToken)
    {
        var pageSize = NormalizePageSize(request.PageSize);
        var takePlusOne = pageSize + 1;
        var cursor = DecodeCursor(request.Cursor);
        var effectiveSince = cursor?.Since ?? request.Since;
        var syncUpperBound = cursor?.SyncUpperBound ?? DateTime.Now;
        var rows = new List<MobileCustomerCatalogItemDto>(takePlusOne);

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
                        LTRIM(RTRIM(customer.cari_kod)) AS CustomerCode,
                        COALESCE(customer.cari_unvan1, '') AS CustomerName,
                        COALESCE(customer.cari_unvan2, '') AS CustomerTitle,
                        COALESCE(customer.cari_VergiKimlikNo, customer.cari_vdaire_no, '') AS TaxNumber,
                        COALESCE(customer.cari_temsilci_kodu, '') AS RepresentativeCode,
                        COALESCE(representative.cari_per_adi, '') AS RepresentativeName,
                        COALESCE(representative.cari_per_soyadi, '') AS RepresentativeSurname,
                        customer.cari_fatura_adres_no AS InvoiceAddressNo,
                        customer.cari_sevk_adres_no AS ShippingAddressNo,
                        COALESCE(customer.cari_cari_kilitli_flg, 0) AS IsLocked,
                        COALESCE(customer.cari_firma_acik_kapal, 0) AS IsClosed,
                        COALESCE(customer.cari_iptal, 0) AS IsDeleted,
                        COALESCE(customer.cari_lastup_date, customer.cari_create_date) AS UpdatedAt
                    FROM dbo.CARI_HESAPLAR customer WITH (NOLOCK)
                    LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI representative WITH (NOLOCK)
                        ON representative.cari_per_kod = customer.cari_temsilci_kodu
                    WHERE customer.cari_kod IS NOT NULL
                        AND LTRIM(RTRIM(customer.cari_kod)) <> ''
                )
                SELECT TOP (@takePlusOne)
                    CustomerCode,
                    CustomerName,
                    CustomerTitle,
                    TaxNumber,
                    RepresentativeCode,
                    RepresentativeName,
                    RepresentativeSurname,
                    InvoiceAddressNo,
                    ShippingAddressNo,
                    IsLocked,
                    IsClosed,
                    IsDeleted,
                    UpdatedAt
                FROM CatalogRows
                WHERE
                    UpdatedAt <= @syncUpperBound
                    AND (@since IS NULL OR UpdatedAt > @since)
                    AND (IsDeleted = 0 OR @since IS NOT NULL)
                    AND (@cursorCustomerCode IS NULL OR CustomerCode > @cursorCustomerCode)
                ORDER BY CustomerCode;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;

            AddParameter(command, "@since", effectiveSince, DbType.DateTime2);
            AddParameter(command, "@syncUpperBound", syncUpperBound, DbType.DateTime2);
            AddParameter(command, "@takePlusOne", takePlusOne, DbType.Int32);
            AddParameter(command, "@cursorCustomerCode", cursor?.CustomerCode, DbType.String);

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
        var deletedCustomerCodes = rows
            .Where(item => item.IsDeleted)
            .Select(item => item.CustomerCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DateTime? syncToken = hasMore ? null : syncUpperBound;

        return new MobileCustomerCatalogResponse(
            syncUpperBound,
            effectiveSince,
            syncToken,
            nextCursor,
            hasMore,
            pageSize,
            rows,
            deletedCustomerCodes);
    }

    private static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static MobileCustomerCatalogItemDto ReadItem(DbDataReader reader)
    {
        var customerName = ReadString(reader, "CustomerName");
        var customerTitle = ReadString(reader, "CustomerTitle");

        return new MobileCustomerCatalogItemDto(
            ReadString(reader, "CustomerCode"),
            customerName,
            customerTitle,
            JoinNonEmpty(customerName, customerTitle),
            ReadString(reader, "TaxNumber"),
            ReadString(reader, "RepresentativeCode"),
            JoinNonEmpty(
                ReadString(reader, "RepresentativeName"),
                ReadString(reader, "RepresentativeSurname")),
            ReadNullableInt(reader, "InvoiceAddressNo"),
            ReadNullableInt(reader, "ShippingAddressNo"),
            ReadBool(reader, "IsLocked"),
            ReadBool(reader, "IsClosed"),
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

    private static int? ReadNullableInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);

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
        MobileCustomerCatalogItemDto item,
        DateTime? since,
        DateTime syncUpperBound)
    {
        var cursor = new CustomerCatalogCursor(item.CustomerCode, since, syncUpperBound);
        var json = JsonSerializer.Serialize(cursor, CursorJsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static CustomerCatalogCursor? DecodeCursor(string? value)
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
            var cursor = JsonSerializer.Deserialize<CustomerCatalogCursor>(json, CursorJsonOptions);

            return cursor is null || string.IsNullOrWhiteSpace(cursor.CustomerCode)
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

    private sealed record CustomerCatalogCursor(
        string CustomerCode,
        DateTime? Since,
        DateTime SyncUpperBound);
}
