using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.MobileSync.ProductPriceCatalog;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.MobileSync.ProductPriceCatalog;

public sealed class GetMobileProductPriceCatalogUseCase(MikroDbContext mikroDbContext)
    : IGetMobileProductPriceCatalogUseCase
{
    private const int DefaultPageSize = 5000;
    private const int MaxPageSize = 10000;

    private static readonly JsonSerializerOptions CursorJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MobileProductPriceCatalogResponse> ExecuteAsync(
        MobileProductPriceCatalogRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var pageSize = NormalizePageSize(request.PageSize);
        var takePlusOne = pageSize + 1;
        var cursor = DecodeCursor(request.Cursor);
        var effectiveSince = cursor?.Since ?? request.Since;
        var syncUpperBound = cursor?.SyncUpperBound ?? DateTime.Now;
        var rows = new List<MobileProductPriceCatalogItemDto>(takePlusOne);

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
                WITH StockRows AS
                (
                    SELECT
                        LTRIM(RTRIM(s.sto_kod)) AS StockCode,
                        COALESCE(s.sto_isim, '') AS StockName,
                        NULLIF(LTRIM(RTRIM(s.sto_kuresel_urun_numarasi)), '') AS GlobalTradeItemNo,
                        COALESCE(s.sto_birim1_ad, '') AS Unit1Name,
                        CASE WHEN COALESCE(s.sto_birim1_katsayi, 0) <= 0 THEN 1 ELSE s.sto_birim1_katsayi END AS Unit1Multiplier,
                        COALESCE(s.sto_birim2_ad, '') AS Unit2Name,
                        CASE WHEN COALESCE(s.sto_birim2_katsayi, 0) <= 0 THEN 1 ELSE s.sto_birim2_katsayi END AS Unit2Multiplier,
                        COALESCE(s.sto_birim3_ad, '') AS Unit3Name,
                        CASE WHEN COALESCE(s.sto_birim3_katsayi, 0) <= 0 THEN 1 ELSE s.sto_birim3_katsayi END AS Unit3Multiplier,
                        COALESCE(s.sto_birim4_ad, '') AS Unit4Name,
                        CASE WHEN COALESCE(s.sto_birim4_katsayi, 0) <= 0 THEN 1 ELSE s.sto_birim4_katsayi END AS Unit4Multiplier,
                        s.sto_satis_dursun AS StockSalesBlockCode,
                        s.sto_siparis_dursun AS StockOrderBlockCode,
                        s.sto_malkabul_dursun AS StockGoodsAcceptanceBlockCode,
                        COALESCE(s.sto_pasif_fl, 0) AS StockIsPassive,
                        COALESCE(s.sto_urun_sorkod, '') AS StockProductManagerCode,
                        COALESCE(s.sto_lastup_date, s.sto_create_date) AS StockUpdatedAt
                    FROM dbo.STOKLAR s WITH (NOLOCK)
                    WHERE s.sto_kod IS NOT NULL
                        AND LTRIM(RTRIM(s.sto_kod)) <> ''
                ),
                LookupRows AS
                (
                    SELECT
                        sr.*,
                        LTRIM(RTRIM(b.bar_kodu)) AS Barcode,
                        CAST('barcode' AS varchar(20)) AS LookupSource,
                        CASE WHEN COALESCE(CONVERT(int, b.bar_birimpntr), 0) <= 0 THEN 1 ELSE CONVERT(int, b.bar_birimpntr) END AS UnitPointer,
                        COALESCE(b.bar_iptal, 0) AS IsBarcodeDeleted,
                        COALESCE(b.bar_lastup_date, b.bar_create_date, sr.StockUpdatedAt) AS BarcodeUpdatedAt
                    FROM StockRows sr
                    INNER JOIN dbo.BARKOD_TANIMLARI b WITH (NOLOCK)
                        ON b.bar_stokkodu = sr.StockCode
                    WHERE b.bar_kodu IS NOT NULL
                        AND LTRIM(RTRIM(b.bar_kodu)) <> ''

                    UNION ALL

                    SELECT
                        sr.*,
                        sr.GlobalTradeItemNo AS Barcode,
                        CAST('gtin' AS varchar(20)) AS LookupSource,
                        1 AS UnitPointer,
                        CAST(0 AS bit) AS IsBarcodeDeleted,
                        sr.StockUpdatedAt AS BarcodeUpdatedAt
                    FROM StockRows sr
                    WHERE sr.GlobalTradeItemNo IS NOT NULL
                        AND NOT EXISTS
                        (
                            SELECT 1
                            FROM dbo.BARKOD_TANIMLARI existingBarcode WITH (NOLOCK)
                            WHERE existingBarcode.bar_kodu = sr.GlobalTradeItemNo
                                AND COALESCE(existingBarcode.bar_iptal, 0) = 0
                        )

                    UNION ALL

                    SELECT
                        sr.*,
                        sr.StockCode AS Barcode,
                        CAST('stock-code' AS varchar(20)) AS LookupSource,
                        1 AS UnitPointer,
                        CAST(0 AS bit) AS IsBarcodeDeleted,
                        sr.StockUpdatedAt AS BarcodeUpdatedAt
                    FROM StockRows sr
                    WHERE NOT EXISTS
                        (
                            SELECT 1
                            FROM dbo.BARKOD_TANIMLARI existingBarcode WITH (NOLOCK)
                            WHERE existingBarcode.bar_kodu = sr.StockCode
                                AND COALESCE(existingBarcode.bar_iptal, 0) = 0
                        )
                ),
                CatalogRows AS
                (
                    SELECT
                        @warehouseNo AS WarehouseNo,
                        lr.Barcode,
                        lr.LookupSource,
                        lr.StockCode,
                        lr.StockName,
                        COALESCE(price.Price, 0) AS Price,
                        COALESCE(price.PriceTypeCode, 0) AS PriceTypeCode,
                        lr.UnitPointer,
                        CASE lr.UnitPointer
                            WHEN 2 THEN lr.Unit2Name
                            WHEN 3 THEN lr.Unit3Name
                            WHEN 4 THEN lr.Unit4Name
                            ELSE lr.Unit1Name
                        END AS UnitName,
                        CASE lr.UnitPointer
                            WHEN 2 THEN lr.Unit2Multiplier
                            WHEN 3 THEN lr.Unit3Multiplier
                            WHEN 4 THEN lr.Unit4Multiplier
                            ELSE lr.Unit1Multiplier
                        END AS UnitMultiplier,
                        lr.Unit2Name AS SecondaryUnitName,
                        lr.Unit2Multiplier AS SecondaryUnitMultiplier,
                        COALESCE(warehouseDetail.sdp_satisdursun, lr.StockSalesBlockCode) AS SalesBlockCode,
                        COALESCE(warehouseDetail.sdp_sipdursun, lr.StockOrderBlockCode) AS OrderBlockCode,
                        COALESCE(warehouseDetail.sdp_malkabuldursun, lr.StockGoodsAcceptanceBlockCode) AS GoodsAcceptanceBlockCode,
                        COALESCE(warehouseDetail.sdp_Pasif_fl, lr.StockIsPassive) AS IsPassive,
                        lr.IsBarcodeDeleted AS IsDeleted,
                        COALESCE(warehouseDetail.sdp_UrunSorumlusuKodu, lr.StockProductManagerCode, '') AS ProductManagerCode,
                        updateInfo.UpdatedAt
                    FROM LookupRows lr
                    LEFT JOIN dbo.STOK_DEPO_DETAYLARI warehouseDetail WITH (NOLOCK)
                        ON warehouseDetail.sdp_depo_kod = lr.StockCode
                        AND warehouseDetail.sdp_depo_no = @warehouseNo
                    OUTER APPLY
                    (
                        SELECT TOP (1)
                            priceRow.sfiyat_fiyati AS Price,
                            COALESCE(priceRow.sfiyat_listesirano, 0) AS PriceTypeCode,
                            COALESCE(priceRow.sfiyat_birim_pntr, lr.UnitPointer) AS PriceUnitPointer,
                            COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) AS PriceUpdatedAt
                        FROM dbo.STOK_SATIS_FIYAT_LISTELERI priceRow WITH (NOLOCK)
                        WHERE priceRow.sfiyat_stokkod = lr.StockCode
                            AND priceRow.sfiyat_deposirano = @warehouseNo
                            AND priceRow.sfiyat_fiyati IS NOT NULL
                            AND
                            (
                                priceRow.sfiyat_birim_pntr = lr.UnitPointer
                                OR priceRow.sfiyat_birim_pntr = 1
                                OR priceRow.sfiyat_birim_pntr IS NULL
                            )
                        ORDER BY
                            CASE WHEN priceRow.sfiyat_birim_pntr = lr.UnitPointer THEN 0 ELSE 1 END,
                            COALESCE(priceRow.sfiyat_listesirano, 2147483647),
                            COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) DESC
                    ) price
                    CROSS APPLY
                    (
                        SELECT MAX(value) AS UpdatedAt
                        FROM
                        (
                            VALUES
                                (lr.StockUpdatedAt),
                                (lr.BarcodeUpdatedAt),
                                (price.PriceUpdatedAt),
                                (COALESCE(warehouseDetail.sdp_lastup_date, warehouseDetail.sdp_create_date))
                        ) AS dates(value)
                    ) updateInfo
                    WHERE
                        (lr.IsBarcodeDeleted = 0 AND price.Price IS NOT NULL)
                        OR (@since IS NOT NULL AND lr.IsBarcodeDeleted = 1)
                )
                SELECT TOP (@takePlusOne)
                    WarehouseNo,
                    Barcode,
                    LookupSource,
                    StockCode,
                    StockName,
                    Price,
                    PriceTypeCode,
                    UnitPointer,
                    UnitName,
                    UnitMultiplier,
                    SecondaryUnitName,
                    SecondaryUnitMultiplier,
                    SalesBlockCode,
                    OrderBlockCode,
                    GoodsAcceptanceBlockCode,
                    IsPassive,
                    IsDeleted,
                    ProductManagerCode,
                    UpdatedAt
                FROM CatalogRows
                WHERE
                    UpdatedAt <= @syncUpperBound
                    AND (@since IS NULL OR UpdatedAt > @since)
                    AND
                    (
                        @cursorStockCode IS NULL
                        OR StockCode > @cursorStockCode
                        OR
                        (
                            StockCode = @cursorStockCode
                            AND Barcode > @cursorBarcode
                        )
                        OR
                        (
                            StockCode = @cursorStockCode
                            AND Barcode = @cursorBarcode
                            AND LookupSource > @cursorLookupSource
                        )
                    )
                ORDER BY StockCode, Barcode, LookupSource;
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 180;

            AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
            AddParameter(command, "@since", effectiveSince, DbType.DateTime2);
            AddParameter(command, "@syncUpperBound", syncUpperBound, DbType.DateTime2);
            AddParameter(command, "@takePlusOne", takePlusOne, DbType.Int32);
            AddParameter(command, "@cursorStockCode", cursor?.StockCode, DbType.String);
            AddParameter(command, "@cursorBarcode", cursor?.Barcode, DbType.String);
            AddParameter(command, "@cursorLookupSource", cursor?.LookupSource, DbType.String);

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
        var deletedBarcodes = rows
            .Where(item => item.IsDeleted)
            .Select(item => item.Barcode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DateTime? syncToken = hasMore ? null : syncUpperBound;

        return new MobileProductPriceCatalogResponse(
            request.WarehouseNo,
            syncUpperBound,
            effectiveSince,
            syncToken,
            nextCursor,
            hasMore,
            pageSize,
            rows,
            deletedBarcodes);
    }

    private static int NormalizePageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

    private static MobileProductPriceCatalogItemDto ReadItem(DbDataReader reader)
    {
        var salesBlockCode = ReadNullableInt(reader, "SalesBlockCode");
        var orderBlockCode = ReadNullableInt(reader, "OrderBlockCode");
        var goodsAcceptanceBlockCode = ReadNullableInt(reader, "GoodsAcceptanceBlockCode");
        var isDeleted = ReadBool(reader, "IsDeleted");

        return new MobileProductPriceCatalogItemDto(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "LookupSource"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadDouble(reader, "Price"),
            ReadInt(reader, "PriceTypeCode"),
            ReadInt(reader, "UnitPointer"),
            ReadString(reader, "UnitName"),
            ReadDouble(reader, "UnitMultiplier"),
            ReadString(reader, "SecondaryUnitName"),
            ReadDouble(reader, "SecondaryUnitMultiplier"),
            salesBlockCode,
            orderBlockCode,
            goodsAcceptanceBlockCode,
            IsBlocked(salesBlockCode),
            IsBlocked(orderBlockCode),
            IsBlocked(goodsAcceptanceBlockCode),
            ReadBool(reader, "IsPassive"),
            isDeleted,
            ReadString(reader, "ProductManagerCode"),
            ReadDateTime(reader, "UpdatedAt"));
    }

    private static bool IsBlocked(int? code) =>
        code.GetValueOrDefault() != 0;

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

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static bool ReadBool(DbDataReader reader, string name) =>
        reader[name] is not DBNull && Convert.ToBoolean(reader[name]);

    private static DateTime ReadDateTime(DbDataReader reader, string name) =>
        reader[name] is DBNull ? DateTime.MinValue : Convert.ToDateTime(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private static string EncodeCursor(
        MobileProductPriceCatalogItemDto item,
        DateTime? since,
        DateTime syncUpperBound)
    {
        var cursor = new CatalogCursor(
            item.StockCode,
            item.Barcode,
            item.LookupSource,
            since,
            syncUpperBound);
        var json = JsonSerializer.Serialize(cursor, CursorJsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static CatalogCursor? DecodeCursor(string? value)
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
            var cursor = JsonSerializer.Deserialize<CatalogCursor>(json, CursorJsonOptions);

            return cursor is null ||
                   string.IsNullOrWhiteSpace(cursor.StockCode) ||
                   string.IsNullOrWhiteSpace(cursor.Barcode) ||
                   string.IsNullOrWhiteSpace(cursor.LookupSource)
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

    private sealed record CatalogCursor(
        string StockCode,
        string Barcode,
        string LookupSource,
        DateTime? Since,
        DateTime SyncUpperBound);
}
