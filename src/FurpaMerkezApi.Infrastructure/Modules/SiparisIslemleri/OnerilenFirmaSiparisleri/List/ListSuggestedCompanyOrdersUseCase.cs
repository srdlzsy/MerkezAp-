using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;

public sealed class ListSuggestedCompanyOrdersUseCase(
    MikroDbContext mikroDbContext,
    IOptionsMonitor<SuggestedCompanyOrderOptions> options)
    : IListSuggestedCompanyOrdersUseCase
{
    public async Task<IReadOnlyCollection<SuggestedCompanyOrderListItemDto>> ExecuteAsync(
        SuggestedCompanyOrderListRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var supplierCode = NormalizeOrNull(request.SupplierCode) ?? string.Empty;
        var deductOpenCompanyOrders = ShouldDeductOpenCompanyOrders(supplierCode);

        const string sql = """
            ;WITH StockBase AS (
                SELECT
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_model_kodu,
                    stock.sto_birim2_katsayi,
                    stock.sto_min_stok_belirleme_gun,
                    stock.sto_sip_stok_belirleme_gun,
                    stock.sto_max_stok_belirleme_gun,
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                        NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                    ) AS DefaultSupplierCode,
                    NULLIF(@supplierCode, '') AS EffectiveSupplierCode
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                    ON detail.sdp_depo_no = @warehouseNo
                   AND detail.sdp_depo_kod = stock.sto_kod
                WHERE ISNULL(stock.sto_iptal, 0) = 0
                  AND ISNULL(COALESCE(detail.sdp_Pasif_fl, stock.sto_pasif_fl), 0) = 0
                  AND ISNULL(COALESCE(detail.sdp_sipdursun, stock.sto_siparis_dursun), 0) = 0
                  AND ISNULL(COALESCE(detail.sdp_malkabuldursun, stock.sto_malkabul_dursun), 0) = 0
                  AND stock.sto_isim NOT LIKE 'DLS%'
                  AND stock.sto_isim NOT LIKE 'SRF%'
                  AND stock.sto_kod NOT IN ('011141','013199','000154','000754','000051','089020','000219')
                  AND (
                      COALESCE(
                          NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                          NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                      ) = @supplierCode
                      OR EXISTS (
                          SELECT 1
                          FROM dbo.SATINALMA_SARTLARI AS term WITH (NOLOCK)
                          WHERE term.sas_stok_kod = stock.sto_kod
                            AND term.sas_cari_kod = @supplierCode
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
            ),
            Consumption AS (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    SUM(ISNULL(movement.sth_miktar, 0)) AS SalesQuantity
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                INNER JOIN StockBase AS stock
                    ON stock.sto_kod = movement.sth_stok_kod
                WHERE movement.sth_tarih >= DATEADD(DAY, -@lookbackDays, CONVERT(date, GETDATE()))
                  AND movement.sth_tarih < DATEADD(DAY, 1, CONVERT(date, GETDATE()))
                  AND movement.sth_cikis_depo_no = @warehouseNo
                  AND ISNULL(movement.sth_iptal, 0) = 0
                  AND movement.sth_tip = 1
                  AND movement.sth_cins = 1
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_evraktip IN (4, 1)
                GROUP BY movement.sth_stok_kod
            ),
            OpenCompanyOrders AS (
                SELECT
                    orders.sip_stok_kod AS StockCode,
                    orders.sip_musteri_kod AS SupplierCode,
                    SUM(ISNULL(orders.sip_miktar, 0) - ISNULL(orders.sip_teslim_miktar, 0)) AS OpenOrderQuantity
                FROM dbo.SIPARISLER AS orders WITH (NOLOCK)
                INNER JOIN Consumption AS consumption
                    ON consumption.StockCode = orders.sip_stok_kod
                INNER JOIN StockBase AS stock
                    ON stock.sto_kod = orders.sip_stok_kod
                   AND stock.EffectiveSupplierCode = orders.sip_musteri_kod
                WHERE @deductOpenCompanyOrders = 1
                  AND orders.sip_tip = 1
                  AND orders.sip_cins = 0
                  AND orders.sip_depono = @warehouseNo
                  AND ISNULL(orders.sip_iptal, 0) = 0
                  AND ISNULL(orders.sip_kapat_fl, 0) = 0
                  AND ISNULL(orders.sip_miktar, 0) > ISNULL(orders.sip_teslim_miktar, 0)
                GROUP BY orders.sip_stok_kod, orders.sip_musteri_kod
            ),
            TargetStock AS (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    ROUND(SUM(CASE
                        WHEN movement.sth_tip = 0
                            OR (movement.sth_tip = 2 AND movement.sth_giris_depo_no = @warehouseNo)
                            THEN ISNULL(movement.sth_miktar, 0)
                        WHEN movement.sth_tip = 1
                            OR (movement.sth_tip = 2 AND movement.sth_cikis_depo_no = @warehouseNo)
                            THEN -1 * ISNULL(movement.sth_miktar, 0)
                        ELSE 0
                    END), 8) AS TargetOnHand
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                INNER JOIN Consumption AS consumption
                    ON consumption.StockCode = movement.sth_stok_kod
                WHERE movement.sth_tarih <= GETDATE()
                  AND NOT (movement.sth_cins IN (9, 15))
                  AND (
                      (movement.sth_tip = 0 AND (movement.sth_giris_depo_no = @warehouseNo OR @warehouseNo = 0))
                      OR (movement.sth_tip = 1 AND (movement.sth_cikis_depo_no = @warehouseNo OR @warehouseNo = 0))
                      OR (
                          movement.sth_tip = 2
                          AND movement.sth_giris_depo_no <> movement.sth_cikis_depo_no
                          AND (movement.sth_giris_depo_no = @warehouseNo OR movement.sth_cikis_depo_no = @warehouseNo)
                      )
                  )
                GROUP BY movement.sth_stok_kod
            ),
            Calculated AS (
                SELECT
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_model_kodu,
                    stock.DefaultSupplierCode,
                    stock.EffectiveSupplierCode,
                    supplier.cari_unvan1 AS SupplierName,
                    barcode.bar_kodu,
                    ISNULL(targetStock.TargetOnHand, 0) AS TargetOnHand,
                    ISNULL(consumption.SalesQuantity, 0) AS SalesQuantity,
                    ISNULL(openOrders.OpenOrderQuantity, 0) AS OpenOrderQuantity,
                    stock.sto_birim2_katsayi,
                    stock.sto_min_stok_belirleme_gun,
                    ISNULL(NULLIF(stock.sto_sip_stok_belirleme_gun, 0), @fallbackRecommendedDay) AS RecommendedDay,
                    stock.sto_max_stok_belirleme_gun,
                    purchaseTerm.sas_brut_fiyat,
                    purchaseTerm.sas_asgari_miktar,
                    purchaseTerm.sas_teslim_sure
                FROM StockBase AS stock
                LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                    ON supplier.cari_kod = stock.EffectiveSupplierCode
                INNER JOIN Consumption AS consumption
                    ON consumption.StockCode = stock.sto_kod
                LEFT JOIN OpenCompanyOrders AS openOrders
                    ON openOrders.StockCode = stock.sto_kod
                   AND openOrders.SupplierCode = stock.EffectiveSupplierCode
                LEFT JOIN TargetStock AS targetStock
                    ON targetStock.StockCode = stock.sto_kod
                OUTER APPLY (
                    SELECT TOP 1 barcode.bar_kodu
                    FROM dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                    WHERE barcode.bar_stokkodu = stock.sto_kod
                      AND barcode.bar_birimpntr = 1
                    ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_create_date DESC
                ) AS barcode
                OUTER APPLY (
                    SELECT TOP 1
                        term.sas_brut_fiyat,
                        term.sas_asgari_miktar,
                        term.sas_teslim_sure
                    FROM dbo.SATINALMA_SARTLARI AS term WITH (NOLOCK)
                    WHERE term.sas_stok_kod = stock.sto_kod
                      AND term.sas_cari_kod = stock.EffectiveSupplierCode
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
            )
            SELECT
                calc.EffectiveSupplierCode AS SupplierCode,
                calc.SupplierName,
                calc.sto_kod AS StockCode,
                calc.sto_isim AS StockName,
                calc.sto_model_kodu AS ModelCode,
                ISNULL(calc.bar_kodu, '') AS Barcode,
                calc.TargetOnHand,
                calc.SalesQuantity,
                calc.OpenOrderQuantity AS OpenCompanyOrderQuantity,
                calc.sto_birim2_katsayi AS PackageFactor,
                calc.sto_min_stok_belirleme_gun AS MinDay,
                calc.RecommendedDay,
                calc.sto_max_stok_belirleme_gun AS MaxDay,
                recommended.RecommendedStockQuantity,
                threshold.MinimumNeedQuantity AS NeedQuantity,
                recommended.SuggestedOrderQuantity,
                calc.sas_brut_fiyat AS PurchasePrice,
                calc.sas_asgari_miktar AS MinimumPurchaseQuantity,
                calc.sas_teslim_sure AS DeliveryDay
            FROM Calculated AS calc
            CROSS APPLY (
                SELECT
                    CEILING((calc.SalesQuantity / NULLIF(@lookbackDays, 0)) *
                        ISNULL(NULLIF(calc.sto_min_stok_belirleme_gun, 0), calc.RecommendedDay)) AS MinimumStockQuantity,
                    CEILING((calc.SalesQuantity / NULLIF(@lookbackDays, 0)) * calc.RecommendedDay) AS RecommendedStockQuantity,
                    CASE
                        WHEN ISNULL(calc.sto_max_stok_belirleme_gun, 0) > 0
                            THEN CEILING((calc.SalesQuantity / NULLIF(@lookbackDays, 0)) * calc.sto_max_stok_belirleme_gun)
                        ELSE NULL
                    END AS MaximumStockQuantity,
                    CASE
                        WHEN ABS(ISNULL(calc.sto_birim2_katsayi, 0)) > 1 THEN ABS(calc.sto_birim2_katsayi)
                        ELSE 0
                    END AS PackageQuantity
            ) AS targetQuantity
            CROSS APPLY (
                SELECT
                    targetQuantity.MinimumStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity AS MinimumNeedQuantity,
                    CASE
                        WHEN targetQuantity.MaximumStockQuantity IS NULL THEN NULL
                        ELSE targetQuantity.MaximumStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity
                    END AS MaximumAllowedQuantity
            ) AS threshold
            CROSS APPLY (
                SELECT
                    CASE
                        WHEN threshold.MinimumNeedQuantity <= 0 THEN 0
                        WHEN ISNULL(calc.sas_asgari_miktar, 0) > threshold.MinimumNeedQuantity
                            THEN calc.sas_asgari_miktar
                        ELSE threshold.MinimumNeedQuantity
                    END AS BaseOrderQuantity
            ) AS baseOrder
            CROSS APPLY (
                SELECT
                    CASE
                        WHEN baseOrder.BaseOrderQuantity <= 0 THEN 0
                        WHEN targetQuantity.PackageQuantity > 0
                            THEN CEILING(baseOrder.BaseOrderQuantity / targetQuantity.PackageQuantity) * targetQuantity.PackageQuantity
                        ELSE baseOrder.BaseOrderQuantity
                    END AS RoundedOrderQuantity
            ) AS roundedOrder
            CROSS APPLY (
                SELECT
                    CASE
                        WHEN roundedOrder.RoundedOrderQuantity <= 0 THEN 0
                        WHEN threshold.MaximumAllowedQuantity IS NOT NULL AND threshold.MaximumAllowedQuantity <= 0 THEN 0
                        WHEN threshold.MaximumAllowedQuantity IS NOT NULL AND roundedOrder.RoundedOrderQuantity > threshold.MaximumAllowedQuantity
                            THEN
                                CASE
                                    WHEN targetQuantity.PackageQuantity > 0
                                        THEN FLOOR(threshold.MaximumAllowedQuantity / targetQuantity.PackageQuantity) * targetQuantity.PackageQuantity
                                    ELSE threshold.MaximumAllowedQuantity
                                END
                        ELSE roundedOrder.RoundedOrderQuantity
                    END AS SuggestedOrderQuantity,
                    targetQuantity.RecommendedStockQuantity
            ) AS recommended
            WHERE calc.SalesQuantity > 0
              AND ISNULL(calc.bar_kodu, '') <> ''
              AND recommended.SuggestedOrderQuantity > 0
            ORDER BY recommended.SuggestedOrderQuantity DESC, calc.SupplierName, calc.sto_isim;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@supplierCode", supplierCode, DbType.String);
                AddParameter(command, "@lookbackDays", request.LookbackDays, DbType.Int32);
                AddParameter(command, "@fallbackRecommendedDay", request.FallbackRecommendedDay, DbType.Int32);
                AddParameter(command, "@deductOpenCompanyOrders", deductOpenCompanyOrders, DbType.Boolean);
            },
            ReadItem,
            cancellationToken);
    }

    private bool ShouldDeductOpenCompanyOrders(string supplierCode)
    {
        var deductionOptions = options.CurrentValue.OpenIssuedOrderDeduction;

        return deductionOptions.Enabled &&
               deductionOptions.TrustedSupplierCodes.Any(code =>
                   string.Equals(NormalizeOrNull(code), supplierCode, StringComparison.OrdinalIgnoreCase));
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
            command.CommandTimeout = 180;
            configure(command);

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

    private static SuggestedCompanyOrderListItemDto ReadItem(DbDataReader reader) =>
        new(
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "ModelCode"),
            ReadString(reader, "Barcode"),
            ReadDouble(reader, "TargetOnHand"),
            ReadDouble(reader, "SalesQuantity"),
            ReadDouble(reader, "OpenCompanyOrderQuantity"),
            ReadDouble(reader, "PackageFactor"),
            ReadDouble(reader, "MinDay"),
            ReadDouble(reader, "RecommendedDay"),
            ReadDouble(reader, "MaxDay"),
            ReadDouble(reader, "RecommendedStockQuantity"),
            ReadDouble(reader, "NeedQuantity"),
            ReadDouble(reader, "SuggestedOrderQuantity"),
            ReadDouble(reader, "PurchasePrice"),
            ReadDouble(reader, "MinimumPurchaseQuantity"),
            ReadNullableInt(reader, "DeliveryDay"));

    private static void Validate(SuggestedCompanyOrderListRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (NormalizeOrNull(request.SupplierCode) is null)
        {
            throw new ArgumentException("Supplier code is required.", nameof(request.SupplierCode));
        }

        if (request.LookbackDays <= 0)
        {
            throw new ArgumentException("Lookback days must be greater than zero.", nameof(request.LookbackDays));
        }

        if (request.FallbackRecommendedDay <= 0)
        {
            throw new ArgumentException("Fallback recommended day must be greater than zero.", nameof(request.FallbackRecommendedDay));
        }
    }

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static int? ReadNullableInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
}
