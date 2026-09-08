using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.EligibleProducts;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.EligibleProducts;

public sealed class ListWarehouseReturnEligibleProductsUseCase(
    MikroDbContext mikroDbContext,
    IOptions<WarehouseReturnProductOptions> options)
    : IListWarehouseReturnEligibleProductsUseCase
{
    public async Task<WarehouseReturnEligibleProductsDto> ExecuteAsync(
        WarehouseReturnEligibleProductsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceWarehouseNo <= 0)
        {
            throw new ArgumentException("Kaynak depo numarasi sifirdan buyuk olmalidir.", nameof(request));
        }

        var routes = GetRoutes(options.Value);
        if (request.TargetWarehouseNo is > 0 && routes.All(route => route.ReturnWarehouseNo != request.TargetWarehouseNo))
        {
            throw new ArgumentException("Secilen depo tanimli bir iade hedefi degildir.", nameof(request));
        }

        if (request.TargetWarehouseNo == request.SourceWarehouseNo)
        {
            throw new ArgumentException("Kaynak depo ile iade hedef deposu ayni olamaz.", nameof(request));
        }

        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            command.CommandText = BuildSql(command, routes);
            AddParameter(command, "@sourceWarehouseNo", request.SourceWarehouseNo, DbType.Int32);
            AddParameter(command, "@targetWarehouseNo", request.TargetWarehouseNo, DbType.Int32);
            AddParameter(command, "@search", NormalizeOrNull(request.Search), DbType.String);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new ArgumentException("Kaynak depo bulunamadi.", nameof(request));
            }

            var sourceWarehouseName = ReadString(reader, "SourceWarehouseName");
            var items = new List<WarehouseReturnEligibleProductDto>();

            await reader.NextResultAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadItem(reader));
            }

            return new WarehouseReturnEligibleProductsDto(
                request.SourceWarehouseNo,
                sourceWarehouseName,
                items.Count,
                items);
        }
        catch (SqlException exception) when (exception.Number == 50001)
        {
            throw new ArgumentException(exception.Message, nameof(request), exception);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string BuildSql(DbCommand command, IReadOnlyList<WarehouseReturnRouteOptions> routes)
    {
        var routeRows = new List<string>(routes.Count);
        for (var index = 0; index < routes.Count; index++)
        {
            var sourceParameter = $"@routeSource{index}";
            var targetParameter = $"@routeTarget{index}";
            AddParameter(command, sourceParameter, routes[index].ProductSourceWarehouseNo, DbType.Int32);
            AddParameter(command, targetParameter, routes[index].ReturnWarehouseNo, DbType.Int32);
            routeRows.Add($"({sourceParameter}, {targetParameter})");
        }

        return $$"""
            DECLARE @sourceWarehouseName nvarchar(50);

            SELECT @sourceWarehouseName = dep_adi
            FROM dbo.DEPOLAR WITH (NOLOCK)
            WHERE dep_no = @sourceWarehouseNo
              AND ISNULL(dep_iptal, 0) = 0;

            IF @sourceWarehouseName IS NULL
            BEGIN
                THROW 50001, 'Kaynak depo bulunamadi veya aktif degil.', 1;
            END;

            SELECT @sourceWarehouseName AS SourceWarehouseName;

            ;WITH ReturnRoutes AS (
                SELECT route.ProductSourceWarehouseNo, route.ReturnWarehouseNo
                FROM (VALUES {{string.Join(", ", routeRows)}}) AS route(ProductSourceWarehouseNo, ReturnWarehouseNo)
                WHERE route.ReturnWarehouseNo <> @sourceWarehouseNo
                  AND (@targetWarehouseNo IS NULL OR route.ReturnWarehouseNo = @targetWarehouseNo)
            ),
            SourceModels AS (
                SELECT DISTINCT
                    route.ProductSourceWarehouseNo,
                    sourceWarehouse.dep_adi AS ProductSourceWarehouseName,
                    route.ReturnWarehouseNo,
                    returnWarehouse.dep_adi AS ReturnWarehouseName,
                    LTRIM(RTRIM(sourceModel.value)) AS ModelCode
                FROM ReturnRoutes AS route
                INNER JOIN dbo.DEPOLAR AS sourceWarehouse WITH (NOLOCK)
                    ON sourceWarehouse.dep_no = route.ProductSourceWarehouseNo
                   AND ISNULL(sourceWarehouse.dep_iptal, 0) = 0
                INNER JOIN dbo.DEPOLAR AS returnWarehouse WITH (NOLOCK)
                    ON returnWarehouse.dep_no = route.ReturnWarehouseNo
                   AND ISNULL(returnWarehouse.dep_iptal, 0) = 0
                CROSS APPLY STRING_SPLIT(
                    REPLACE(REPLACE(ISNULL(sourceWarehouse.dep_barkod_yazici_yolu, N''), N';', N','), N'|', N','),
                    N',') AS sourceModel
                WHERE LTRIM(RTRIM(sourceModel.value)) <> N''
            ),
            CurrentBalances AS (
                SELECT
                    summary.sho_StokKodu AS StockCode,
                    SUM(
                        (ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_GirisIade, 0))
                        - (ISNULL(summary.sho_CikisNormal, 0) + ISNULL(summary.sho_CikisIade, 0))) AS CurrentStockQuantity
                FROM dbo.STOK_HAREKETLERI_OZET AS summary WITH (NOLOCK)
                WHERE summary.sho_Depo = @sourceWarehouseNo
                  AND summary.sho_HareketCins NOT IN (9, 15)
                GROUP BY summary.sho_StokKodu
                HAVING SUM(
                    (ISNULL(summary.sho_GirisNormal, 0) + ISNULL(summary.sho_GirisIade, 0))
                    - (ISNULL(summary.sho_CikisNormal, 0) + ISNULL(summary.sho_CikisIade, 0))) > 0.00000001
            )
            SELECT
                stock.sto_kod AS StockCode,
                stock.sto_isim AS StockName,
                ISNULL(barcode.bar_kodu, N'') AS Barcode,
                ISNULL(caseBarcode.bar_kodu, N'') AS CaseBarcode,
                LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N''))) AS ModelCode,
                stock.sto_birim1_ad AS UnitName,
                ISNULL(stock.sto_birim2_ad, N'') AS SecondaryUnitName,
                ABS(ISNULL(stock.sto_birim2_katsayi, 0)) AS UnitMultiplier,
                sourceModel.ProductSourceWarehouseNo,
                sourceModel.ProductSourceWarehouseName,
                sourceModel.ReturnWarehouseNo,
                sourceModel.ReturnWarehouseName,
                balance.CurrentStockQuantity,
                CAST(CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(stock.sto_sat_cari_kod, N''))), N'') IS NOT NULL
                         OR EXISTS (
                             SELECT 1
                             FROM dbo.SATINALMA_SARTLARI AS purchaseTerm WITH (NOLOCK)
                             WHERE purchaseTerm.sas_stok_kod = stock.sto_kod
                               AND ISNULL(purchaseTerm.sas_iptal, 0) = 0
                               AND NULLIF(LTRIM(RTRIM(ISNULL(purchaseTerm.sas_cari_kod, N''))), N'') IS NOT NULL
                               AND (purchaseTerm.sas_depo_no IS NULL OR purchaseTerm.sas_depo_no IN (0, @sourceWarehouseNo))
                               AND (purchaseTerm.sas_basla_tarih IS NULL OR purchaseTerm.sas_basla_tarih <= GETDATE())
                               AND (
                                   purchaseTerm.sas_bitis_tarih IS NULL
                                   OR purchaseTerm.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                                   OR purchaseTerm.sas_bitis_tarih >= CONVERT(date, GETDATE()))
                         )
                    THEN 1 ELSE 0
                END AS bit) AS HasPurchaseRequirement
            FROM SourceModels AS sourceModel
            INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N''))) = sourceModel.ModelCode
            INNER JOIN CurrentBalances AS balance
                ON balance.StockCode = stock.sto_kod
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS branchDetail WITH (NOLOCK)
                ON branchDetail.sdp_depo_no = @sourceWarehouseNo
               AND branchDetail.sdp_depo_kod = stock.sto_kod
            OUTER APPLY (
                SELECT TOP 1 definition.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS definition WITH (NOLOCK)
                WHERE definition.bar_stokkodu = stock.sto_kod
                  AND definition.bar_birimpntr = 1
                ORDER BY ISNULL(definition.bar_master, 0) DESC, definition.bar_create_date DESC
            ) AS barcode
            OUTER APPLY (
                SELECT TOP 1 definition.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS definition WITH (NOLOCK)
                WHERE definition.bar_stokkodu = stock.sto_kod
                  AND ISNULL(definition.bar_birimpntr, 1) <> 1
                ORDER BY ISNULL(definition.bar_master, 0) DESC, definition.bar_birimpntr DESC, definition.bar_create_date DESC
            ) AS caseBarcode
            WHERE ISNULL(stock.sto_iptal, 0) = 0
              AND ISNULL(COALESCE(branchDetail.sdp_Pasif_fl, stock.sto_pasif_fl), 0) = 0
              AND stock.sto_isim NOT LIKE N'DLS%'
              AND EXISTS (
                  SELECT 1
                  FROM dbo.STOK_DEPO_DETAYLARI AS productSourceDetail WITH (NOLOCK)
                  WHERE productSourceDetail.sdp_depo_no = sourceModel.ProductSourceWarehouseNo
                    AND productSourceDetail.sdp_depo_kod = stock.sto_kod
                    AND ISNULL(productSourceDetail.sdp_Pasif_fl, 0) = 0
                    AND ISNULL(productSourceDetail.sdp_sipdursun, 0) = 0)
              AND (
                  @search IS NULL
                  OR stock.sto_kod LIKE N'%' + @search + N'%'
                  OR stock.sto_isim LIKE N'%' + @search + N'%'
                  OR EXISTS (
                      SELECT 1
                      FROM dbo.BARKOD_TANIMLARI AS searchBarcode WITH (NOLOCK)
                      WHERE searchBarcode.bar_stokkodu = stock.sto_kod
                        AND searchBarcode.bar_kodu LIKE N'%' + @search + N'%'))
            ORDER BY
                sourceModel.ReturnWarehouseNo,
                stock.sto_model_kodu,
                stock.sto_isim,
                stock.sto_kod;
            """;
    }

    private static IReadOnlyList<WarehouseReturnRouteOptions> GetRoutes(WarehouseReturnProductOptions options)
    {
        var routes = (options.Routes ?? [])
            .Where(route => route.ProductSourceWarehouseNo > 0 && route.ReturnWarehouseNo > 0)
            .DistinctBy(route => (route.ProductSourceWarehouseNo, route.ReturnWarehouseNo))
            .ToArray();

        if (routes.Length == 0)
        {
            throw new InvalidOperationException("En az bir depo iade urun rotasi tanimlanmalidir.");
        }

        var duplicateSource = routes
            .GroupBy(route => route.ProductSourceWarehouseNo)
            .FirstOrDefault(group => group.Select(route => route.ReturnWarehouseNo).Distinct().Count() > 1);
        if (duplicateSource is not null)
        {
            throw new InvalidOperationException(
                $"{duplicateSource.Key} urun kaynak deposu birden fazla iade hedefine baglanamaz.");
        }

        return routes;
    }

    private static WarehouseReturnEligibleProductDto ReadItem(DbDataReader reader)
    {
        var hasPurchaseRequirement = Convert.ToBoolean(reader["HasPurchaseRequirement"]);
        var currentStockQuantity = Math.Round(Convert.ToDouble(reader["CurrentStockQuantity"]), 8);
        var productSourceWarehouseName = ReadString(reader, "ProductSourceWarehouseName");
        var returnWarehouseName = ReadString(reader, "ReturnWarehouseName");

        return new WarehouseReturnEligibleProductDto(
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "CaseBarcode"),
            ReadString(reader, "ModelCode"),
            GetModelName(ReadString(reader, "ModelCode")),
            ReadString(reader, "UnitName"),
            ReadString(reader, "SecondaryUnitName"),
            Math.Abs(Convert.ToDouble(reader["UnitMultiplier"])),
            Convert.ToInt32(reader["ProductSourceWarehouseNo"]),
            productSourceWarehouseName,
            Convert.ToInt32(reader["ReturnWarehouseNo"]),
            returnWarehouseName,
            currentStockQuantity,
            currentStockQuantity,
            hasPurchaseRequirement ? "Mixed" : "Warehouse",
            hasPurchaseRequirement,
            true,
            $"Urun {productSourceWarehouseName} kaynakli; {returnWarehouseName} deposuna iade edilebilir.",
            hasPurchaseRequirement
                ? ["Urunun aktif firma tedarik baglantisi da bulunuyor; kaynak karma olabilir."]
                : []);
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

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string GetModelName(string modelCode) =>
        modelCode.Trim() switch
        {
            "01" or "02" or "03" or "04" or "20" => "Market",
            "10" => "Meyve",
            "11" => "Sebze",
            "12" => "Yesillik",
            "15" or "21" => "Sarkuteri",
            "22" or "30" or "31" or "32" or "33" or "40" => "Unlu Mamul",
            "23" => "Manav Sarf",
            _ => modelCode
        };
}
