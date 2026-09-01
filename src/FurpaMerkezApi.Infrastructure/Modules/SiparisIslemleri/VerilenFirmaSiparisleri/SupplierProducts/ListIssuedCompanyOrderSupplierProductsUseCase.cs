using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.SupplierProducts;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.SupplierProducts;

public sealed class ListIssuedCompanyOrderSupplierProductsUseCase(MikroDbContext mikroDbContext)
    : IListIssuedCompanyOrderSupplierProductsUseCase
{
    private const int DefaultTake = 500;
    private const int MaxTake = 2000;

    public async Task<IReadOnlyCollection<IssuedCompanyOrderSupplierProductDto>> ExecuteAsync(
        IssuedCompanyOrderSupplierProductsRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var customerCode = NormalizeOrNull(request.CustomerCode) ?? string.Empty;
        var search = NormalizeOrNull(request.Search);
        var take = NormalizeTake(request.Take);

        const string sql = """
            DECLARE @CustomerName nvarchar(200);

            SELECT TOP 1
                @CustomerName = LTRIM(RTRIM(CONCAT(ISNULL(customer.cari_unvan1, N''), N' ', ISNULL(customer.cari_unvan2, N''))))
            FROM dbo.CARI_HESAPLAR AS customer WITH (NOLOCK)
            WHERE customer.cari_kod = @customerCode;

            IF @CustomerName IS NULL
            BEGIN
                THROW 50003, 'Secilen cari bulunamadi.', 1;
            END;

            ;WITH ProductBase AS (
                SELECT
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_model_kodu,
                    stock.sto_birim1_ad,
                    stock.sto_birim2_ad,
                    stock.sto_birim2_katsayi
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                INNER JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                    ON detail.sdp_depo_no = @warehouseNo
                   AND detail.sdp_depo_kod = stock.sto_kod
                WHERE ISNULL(stock.sto_iptal, 0) = 0
                  AND ISNULL(COALESCE(detail.sdp_Pasif_fl, stock.sto_pasif_fl), 0) = 0
                  AND ISNULL(COALESCE(detail.sdp_sipdursun, stock.sto_siparis_dursun), 0) = 0
                  AND stock.sto_kod IS NOT NULL
                  AND stock.sto_isim IS NOT NULL
                  AND stock.sto_isim NOT LIKE N'DLS%'
                  AND stock.sto_kod NOT IN (N'011141', N'013199', N'000154', N'000754', N'000051', N'089020', N'000219')
                  AND (
                      NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), N'') = @customerCode
                      OR NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), N'') = @customerCode
                      OR EXISTS (
                          SELECT 1
                          FROM dbo.SATINALMA_SARTLARI AS term WITH (NOLOCK)
                          WHERE term.sas_stok_kod = stock.sto_kod
                            AND term.sas_cari_kod = @customerCode
                            AND ISNULL(term.sas_iptal, 0) = 0
                            AND (term.sas_depo_no IN (0, @warehouseNo) OR term.sas_depo_no IS NULL)
                            AND (term.sas_basla_tarih IS NULL OR term.sas_basla_tarih <= GETDATE())
                            AND (
                                term.sas_bitis_tarih IS NULL
                                OR term.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                                OR term.sas_bitis_tarih >= CONVERT(date, GETDATE())
                            )
                      )
                  )
                  AND (
                      @search IS NULL
                      OR stock.sto_kod LIKE @searchLike
                      OR stock.sto_isim LIKE @searchLike
                      OR EXISTS (
                          SELECT 1
                          FROM dbo.BARKOD_TANIMLARI AS searchBarcode WITH (NOLOCK)
                          WHERE searchBarcode.bar_stokkodu = stock.sto_kod
                            AND searchBarcode.bar_kodu LIKE @searchLike
                      )
                  )
            )
            SELECT TOP (@take)
                @warehouseNo AS WarehouseNo,
                @customerCode AS CustomerCode,
                ISNULL(@CustomerName, N'') AS CustomerName,
                product.sto_kod AS StockCode,
                product.sto_isim AS StockName,
                ISNULL(product.sto_model_kodu, N'') AS ModelCode,
                ISNULL(product.sto_birim1_ad, N'') AS UnitName,
                ISNULL(product.sto_birim2_ad, N'') AS SecondaryUnitName,
                ISNULL(product.sto_birim2_katsayi, 0) AS PackageFactor,
                ISNULL(barcode.bar_kodu, N'') AS Barcode,
                ISNULL(caseBarcode.bar_kodu, N'') AS CaseBarcode,
                ISNULL(purchaseTerm.PurchasePrice, 0) AS UnitPrice,
                ISNULL(purchaseTerm.sas_asgari_miktar, 0) AS MinimumPurchaseQuantity,
                purchaseTerm.sas_teslim_sure AS DeliveryDay
            FROM ProductBase AS product
            OUTER APPLY (
                SELECT TOP 1 barcode.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                WHERE barcode.bar_stokkodu = product.sto_kod
                  AND barcode.bar_birimpntr = 1
                ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_create_date DESC
            ) AS barcode
            OUTER APPLY (
                SELECT TOP 1 barcode.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                WHERE barcode.bar_stokkodu = product.sto_kod
                  AND ISNULL(barcode.bar_birimpntr, 1) <> 1
                ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_birimpntr DESC, barcode.bar_create_date DESC
            ) AS caseBarcode
            OUTER APPLY (
                SELECT TOP 1
                    ISNULL(term.sas_brut_fiyat, 0) AS PurchasePrice,
                    term.sas_asgari_miktar,
                    term.sas_teslim_sure
                FROM dbo.SATINALMA_SARTLARI AS term WITH (NOLOCK)
                WHERE term.sas_stok_kod = product.sto_kod
                  AND term.sas_cari_kod = @customerCode
                  AND ISNULL(term.sas_iptal, 0) = 0
                  AND (term.sas_depo_no IN (0, @warehouseNo) OR term.sas_depo_no IS NULL)
                  AND (term.sas_basla_tarih IS NULL OR term.sas_basla_tarih <= GETDATE())
                  AND (
                      term.sas_bitis_tarih IS NULL
                      OR term.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                      OR term.sas_bitis_tarih >= CONVERT(date, GETDATE())
                  )
                ORDER BY
                    CASE WHEN term.sas_depo_no = @warehouseNo THEN 0 ELSE 1 END,
                    term.sas_belge_tarih DESC,
                    term.sas_create_date DESC
            ) AS purchaseTerm
            ORDER BY product.sto_isim, product.sto_kod;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@customerCode", customerCode, DbType.String);
                AddParameter(command, "@search", search, DbType.String);
                AddParameter(command, "@searchLike", search is null ? null : $"%{search}%", DbType.String);
                AddParameter(command, "@take", take, DbType.Int32);
            },
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
        catch (SqlException exception) when (exception.Number == 50003)
        {
            throw new ArgumentException(exception.Message, nameof(IssuedCompanyOrderSupplierProductsRequest.CustomerCode));
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

    private static IssuedCompanyOrderSupplierProductDto ReadItem(DbDataReader reader)
    {
        var modelCode = ReadString(reader, "ModelCode");

        return new IssuedCompanyOrderSupplierProductDto(
            Convert.ToInt32(reader["WarehouseNo"]),
            ReadString(reader, "CustomerCode"),
            ReadString(reader, "CustomerName"),
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
            ReadDouble(reader, "UnitPrice"),
            ReadDouble(reader, "MinimumPurchaseQuantity"),
            ReadNullableInt(reader, "DeliveryDay"),
            1);
    }

    private static void Validate(IssuedCompanyOrderSupplierProductsRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (NormalizeOrNull(request.CustomerCode) is null)
        {
            throw new ArgumentException("Customer code is required.", nameof(request.CustomerCode));
        }

        if (request.Take <= 0)
        {
            throw new ArgumentException("Take must be greater than zero.", nameof(request.Take));
        }
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

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

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static double NormalizeUnitMultiplier(double value)
    {
        var normalized = Math.Abs(value);
        return normalized > 0d ? normalized : 0d;
    }

    private static int? ReadNullableInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);

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
