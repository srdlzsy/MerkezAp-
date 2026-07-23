using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;

public sealed class ListSuggestedWarehouseOrdersUseCase(
    MikroDbContext mikroDbContext,
    IOptionsMonitor<SuggestedWarehouseOrderOptions> options)
    : IListSuggestedWarehouseOrdersUseCase
{
    public async Task<IReadOnlyCollection<SuggestedWarehouseOrderListItemDto>> ExecuteAsync(
        SuggestedWarehouseOrderListRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var deductOpenIncomingOrders = ShouldDeductOpenIncomingOrders(request.SourceWarehouseNo);

        const string sql = """
            DECLARE @SourceModelCodes nvarchar(100);

            SELECT @SourceModelCodes = dep_barkod_yazici_yolu
            FROM dbo.DEPOLAR WITH (NOLOCK)
            WHERE dep_no = @sourceWarehouseNo;

            IF NULLIF(LTRIM(RTRIM(ISNULL(@SourceModelCodes, N''))), N'') IS NULL
            BEGIN
                THROW 50001, 'Secilen kaynak depo icin model kodlari tanimli degil.', 1;
            END;

            ;WITH SourceModels AS (
                SELECT LTRIM(RTRIM(value)) AS ModelCode
                FROM STRING_SPLIT(@SourceModelCodes, ',')
                WHERE LTRIM(RTRIM(value)) <> ''
            ),
            StockBase AS (
                SELECT
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_model_kodu,
                    stock.sto_birim2_katsayi,
                    stock.sto_min_stok_belirleme_gun,
                    stock.sto_sip_stok_belirleme_gun,
                    stock.sto_max_stok_belirleme_gun
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                INNER JOIN SourceModels AS model
                    ON model.ModelCode = stock.sto_model_kodu
                WHERE ISNULL(stock.sto_iptal, 0) = 0
                  AND ISNULL(stock.sto_siparis_dursun, 0) = 0
                  AND stock.sto_isim NOT LIKE 'DLS%'
                  AND stock.sto_isim NOT LIKE 'SRF%'
                  AND stock.sto_kod NOT IN ('011141','013199','000154','000754','000051','089020','000219')
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.STOK_DEPO_DETAYLARI AS targetDetail WITH (NOLOCK)
                      WHERE targetDetail.sdp_depo_no = @targetWarehouseNo
                        AND targetDetail.sdp_depo_kod = stock.sto_kod
                        AND ISNULL(targetDetail.sdp_sipdursun, 0) = 0
                  )
                  AND EXISTS (
                      SELECT 1
                      FROM dbo.STOK_DEPO_DETAYLARI AS sourceDetail WITH (NOLOCK)
                      WHERE sourceDetail.sdp_depo_no = @sourceWarehouseNo
                        AND sourceDetail.sdp_depo_kod = stock.sto_kod
                        AND ISNULL(sourceDetail.sdp_sipdursun, 0) = 0
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
                  AND movement.sth_cikis_depo_no = @targetWarehouseNo
                  AND ISNULL(movement.sth_iptal, 0) = 0
                  AND movement.sth_tip = 1
                  AND movement.sth_cins = 1
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_evraktip IN (4, 1)
                GROUP BY movement.sth_stok_kod
            ),
            OpenIncoming AS (
                SELECT
                    warehouseOrder.ssip_stok_kod AS StockCode,
                    SUM(ISNULL(warehouseOrder.ssip_miktar, 0) - ISNULL(warehouseOrder.ssip_teslim_miktar, 0)) AS OpenOrderQuantity
                FROM dbo.DEPOLAR_ARASI_SIPARISLER AS warehouseOrder WITH (NOLOCK)
                INNER JOIN StockBase AS stock
                    ON stock.sto_kod = warehouseOrder.ssip_stok_kod
                WHERE @deductOpenIncomingOrders = 1
                  AND warehouseOrder.ssip_girdepo = @targetWarehouseNo
                  AND warehouseOrder.ssip_cikdepo = @sourceWarehouseNo
                  AND ISNULL(warehouseOrder.ssip_iptal, 0) = 0
                  AND ISNULL(warehouseOrder.ssip_kapat_fl, 0) = 0
                  AND ISNULL(warehouseOrder.ssip_miktar, 0) > ISNULL(warehouseOrder.ssip_teslim_miktar, 0)
                GROUP BY warehouseOrder.ssip_stok_kod
            ),
            TargetStock AS (
                SELECT
                    summary.sho_StokKodu AS StockCode,
                    SUM(ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_CikisIade, 0)
                      - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) AS TargetOnHand
                FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
                INNER JOIN StockBase AS stock
                    ON stock.sto_kod = summary.sho_StokKodu
                WHERE summary.sho_Depo = @targetWarehouseNo
                GROUP BY summary.sho_StokKodu
            ),
            SourceStock AS (
                SELECT
                    summary.sho_StokKodu AS StockCode,
                    SUM(ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_CikisIade, 0)
                      - ISNULL(summary.sho_CikisNormal, 0) - ISNULL(summary.sho_GirisIade, 0)) AS SourceOnHand
                FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
                INNER JOIN StockBase AS stock
                    ON stock.sto_kod = summary.sho_StokKodu
                WHERE summary.sho_Depo = @sourceWarehouseNo
                GROUP BY summary.sho_StokKodu
            ),
            Calculated AS (
                SELECT
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_model_kodu,
                    barcode.bar_kodu,
                    ISNULL(targetStock.TargetOnHand, 0) AS TargetOnHand,
                    ISNULL(sourceStock.SourceOnHand, 0) AS SourceOnHand,
                    ISNULL(consumption.SalesQuantity, 0) AS SalesQuantity,
                    ISNULL(openIncoming.OpenOrderQuantity, 0) AS OpenOrderQuantity,
                    stock.sto_birim2_katsayi,
                    stock.sto_min_stok_belirleme_gun,
                    ISNULL(NULLIF(stock.sto_sip_stok_belirleme_gun, 0), @fallbackRecommendedDay) AS RecommendedDay,
                    stock.sto_max_stok_belirleme_gun
                FROM StockBase AS stock
                LEFT JOIN Consumption AS consumption
                    ON consumption.StockCode = stock.sto_kod
                LEFT JOIN OpenIncoming AS openIncoming
                    ON openIncoming.StockCode = stock.sto_kod
                LEFT JOIN TargetStock AS targetStock
                    ON targetStock.StockCode = stock.sto_kod
                LEFT JOIN SourceStock AS sourceStock
                    ON sourceStock.StockCode = stock.sto_kod
                OUTER APPLY (
                    SELECT TOP 1 barcode.bar_kodu
                    FROM dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                    WHERE barcode.bar_stokkodu = stock.sto_kod
                      AND barcode.bar_birimpntr = 1
                    ORDER BY ISNULL(barcode.bar_master, 0) DESC, barcode.bar_create_date DESC
                ) AS barcode
            )
            SELECT
                calc.sto_kod AS StockCode,
                calc.sto_isim AS StockName,
                calc.sto_model_kodu AS ModelCode,
                ISNULL(calc.bar_kodu, '') AS Barcode,
                calc.TargetOnHand,
                calc.SourceOnHand,
                calc.SalesQuantity,
                calc.OpenOrderQuantity AS OpenIncomingOrderQuantity,
                calc.sto_birim2_katsayi AS PackageFactor,
                calc.sto_min_stok_belirleme_gun AS MinDay,
                calc.RecommendedDay,
                calc.sto_max_stok_belirleme_gun AS MaxDay,
                recommended.RecommendedStockQuantity,
                recommended.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity AS NeedQuantity,
                CASE
                    WHEN calc.SourceOnHand <= 0 THEN 0
                    WHEN recommended.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity > calc.SourceOnHand
                        THEN calc.SourceOnHand
                    ELSE recommended.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity
                END AS SuggestedOrderQuantity
            FROM Calculated AS calc
            CROSS APPLY (
                SELECT CEILING((calc.SalesQuantity / NULLIF(@lookbackDays, 0)) * calc.RecommendedDay) AS RecommendedStockQuantity
            ) AS recommended
            WHERE calc.SalesQuantity > 0
              AND ISNULL(calc.bar_kodu, '') <> ''
              AND recommended.RecommendedStockQuantity > calc.TargetOnHand + calc.OpenOrderQuantity
              AND (
                  CASE
                      WHEN calc.SourceOnHand <= 0 THEN 0
                      WHEN recommended.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity > calc.SourceOnHand
                          THEN calc.SourceOnHand
                      ELSE recommended.RecommendedStockQuantity - calc.TargetOnHand - calc.OpenOrderQuantity
                  END
              ) > 0
            ORDER BY SuggestedOrderQuantity DESC, calc.sto_isim;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@targetWarehouseNo", request.TargetWarehouseNo, DbType.Int32);
                AddParameter(command, "@sourceWarehouseNo", request.SourceWarehouseNo, DbType.Int32);
                AddParameter(command, "@lookbackDays", request.LookbackDays, DbType.Int32);
                AddParameter(command, "@fallbackRecommendedDay", request.FallbackRecommendedDay, DbType.Int32);
                AddParameter(command, "@deductOpenIncomingOrders", deductOpenIncomingOrders, DbType.Boolean);
            },
            ReadItem,
            cancellationToken);
    }

    private bool ShouldDeductOpenIncomingOrders(int sourceWarehouseNo)
    {
        var deductionOptions = options.CurrentValue.OpenIncomingOrderDeduction;

        return deductionOptions.Enabled &&
               deductionOptions.TrustedSourceWarehouseNos.Contains(sourceWarehouseNo);
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

    private static SuggestedWarehouseOrderListItemDto ReadItem(DbDataReader reader) =>
        new(
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "ModelCode"),
            ReadString(reader, "Barcode"),
            ReadDouble(reader, "TargetOnHand"),
            ReadDouble(reader, "SourceOnHand"),
            ReadDouble(reader, "SalesQuantity"),
            ReadDouble(reader, "OpenIncomingOrderQuantity"),
            ReadDouble(reader, "PackageFactor"),
            ReadDouble(reader, "MinDay"),
            ReadDouble(reader, "RecommendedDay"),
            ReadDouble(reader, "MaxDay"),
            ReadDouble(reader, "RecommendedStockQuantity"),
            ReadDouble(reader, "NeedQuantity"),
            ReadDouble(reader, "SuggestedOrderQuantity"));

    private static void Validate(SuggestedWarehouseOrderListRequest request)
    {
        if (request.TargetWarehouseNo <= 0)
        {
            throw new ArgumentException("Target warehouse no must be greater than zero.", nameof(request.TargetWarehouseNo));
        }

        if (request.SourceWarehouseNo <= 0)
        {
            throw new ArgumentException("Source warehouse no must be greater than zero.", nameof(request.SourceWarehouseNo));
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

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
}
