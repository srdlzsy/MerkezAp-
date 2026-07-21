using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.StokRaporlari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.StokRaporlari;

public sealed class StockReportsUseCase(MikroDbContext mikroDbContext) : IStockReportsUseCase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 1000;

    public async Task<StockOnHandReportDto> GetStockOnHandAsync(
        StockOnHandReportRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);

        const string sql = """
            SELECT TOP (@take)
                @warehouseNo AS WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                stock.sto_kod AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                COALESCE(stock.sto_birim1_ad, '') AS UnitName,
                quantity.Quantity,
                COALESCE(dbo.fn_StokSatisFiyati(stock.sto_kod, '1', @warehouseNo, '1'), 0) AS SalesPrice,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''),
                    ''
                ) AS SupplierCode,
                COALESCE(supplier.cari_unvan1, '') AS SupplierName,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''),
                    ''
                ) AS ProductManagerCode,
                COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '') AS ProductManagerName,
                COALESCE(stock.sto_kategori_kodu, '') AS CategoryCode,
                COALESCE(stock.sto_reyon_kodu, '') AS RayonCode,
                COALESCE(stock.sto_uretici_kodu, '') AS ProducerCode,
                COALESCE(stock.sto_model_kodu, '') AS ModelCode,
                COALESCE(detail.sdp_satisdursun, stock.sto_satis_dursun) AS SalesBlockCode,
                COALESCE(detail.sdp_sipdursun, stock.sto_siparis_dursun) AS OrderBlockCode,
                COALESCE(detail.sdp_malkabuldursun, stock.sto_malkabul_dursun) AS GoodsAcceptanceBlockCode,
                COALESCE(detail.sdp_Pasif_fl, stock.sto_pasif_fl, 0) AS IsPassive
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                ON detail.sdp_depo_kod = stock.sto_kod
               AND detail.sdp_depo_no = @warehouseNo
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON warehouse.dep_no = @warehouseNo
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = stock.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY
                    CASE WHEN COALESCE(bar.bar_master, 0) = 1 THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(bar.bar_birimpntr, 0) = 1 THEN 0 ELSE 1 END,
                    bar.bar_create_date DESC
            ) AS barcode
            CROSS APPLY (
                SELECT COALESCE(dbo.fn_DepodakiMiktar(stock.sto_kod, @warehouseNo, @reportDate), 0) AS Quantity
            ) AS quantity
            LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                ON supplier.cari_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                )
            LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI AS personnel WITH (NOLOCK)
                ON personnel.cari_per_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')
                )
            WHERE COALESCE(stock.sto_iptal, 0) = 0
              AND (@onlyWithStock = 0 OR ABS(quantity.Quantity) > 0.000001)
              AND (
                    @search IS NULL
                    OR stock.sto_kod LIKE @searchLike
                    OR stock.sto_isim LIKE @searchLike
                    OR barcode.bar_kodu LIKE @searchLike
              )
              AND (
                    @supplierCode IS NULL
                    OR COALESCE(
                        NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                        NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                    ) = @supplierCode
                    OR EXISTS (
                        SELECT 1
                        FROM dbo.SATINALMA_SARTLARI AS term WITH (NOLOCK)
                        WHERE term.sas_stok_kod = stock.sto_kod
                          AND term.sas_cari_kod = @supplierCode
                          AND COALESCE(term.sas_iptal, 0) = 0
                    )
              )
              AND (@categoryCode IS NULL OR stock.sto_kategori_kodu = @categoryCode)
              AND (@producerCode IS NULL OR stock.sto_uretici_kodu = @producerCode)
              AND (
                    @productManagerCode IS NULL
                    OR COALESCE(
                        NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                        NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')
                    ) = @productManagerCode
              )
              AND (@modelCode IS NULL OR stock.sto_model_kodu = @modelCode)
            ORDER BY
                ABS(quantity.Quantity) DESC,
                stock.sto_isim,
                stock.sto_kod;
            """;

        var items = await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", normalized.WarehouseNo, DbType.Int32);
                AddParameter(command, "@reportDate", normalized.ReportDate, DbType.DateTime);
                AddParameter(command, "@search", normalized.Search, DbType.String);
                AddParameter(command, "@searchLike", ToLike(normalized.Search), DbType.String);
                AddParameter(command, "@supplierCode", normalized.SupplierCode, DbType.String);
                AddParameter(command, "@categoryCode", normalized.CategoryCode, DbType.String);
                AddParameter(command, "@producerCode", normalized.ProducerCode, DbType.String);
                AddParameter(command, "@productManagerCode", normalized.ProductManagerCode, DbType.String);
                AddParameter(command, "@modelCode", normalized.ModelCode, DbType.String);
                AddParameter(command, "@onlyWithStock", normalized.OnlyWithStock, DbType.Boolean);
                AddParameter(command, "@take", normalized.Take, DbType.Int32);
            },
            ReadStockOnHandItem,
            cancellationToken);

        var warehouseName = items.FirstOrDefault()?.WarehouseName
            ?? await GetWarehouseNameAsync(normalized.WarehouseNo, cancellationToken);

        return new StockOnHandReportDto(
            normalized.WarehouseNo,
            warehouseName,
            normalized.ReportDate,
            items.Count,
            Round(items.Sum(item => item.Quantity)),
            Round(items.Sum(item => item.SalesValue)),
            items);
    }

    public async Task<IReadOnlyCollection<ProductWarehouseStockDto>> GetProductWarehouseStockAsync(
        ProductWarehouseStockRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);

        const string sql = """
            ;WITH ProductRows AS (
                SELECT TOP (1)
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_birim1_ad,
                    stock.sto_satis_dursun,
                    stock.sto_siparis_dursun,
                    stock.sto_malkabul_dursun,
                    stock.sto_pasif_fl
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                WHERE COALESCE(stock.sto_iptal, 0) = 0
                  AND (
                        stock.sto_kod = @stockCodeOrBarcode
                        OR EXISTS (
                            SELECT 1
                            FROM dbo.BARKOD_TANIMLARI AS code WITH (NOLOCK)
                            WHERE code.bar_stokkodu = stock.sto_kod
                              AND code.bar_kodu = @stockCodeOrBarcode
                              AND COALESCE(code.bar_iptal, 0) = 0
                        )
                  )
                ORDER BY
                    CASE WHEN stock.sto_kod = @stockCodeOrBarcode THEN 0 ELSE 1 END,
                    stock.sto_kod
            )
            SELECT TOP (@take)
                warehouse.dep_no AS WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                product.sto_kod AS StockCode,
                COALESCE(product.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                COALESCE(product.sto_birim1_ad, '') AS UnitName,
                quantity.Quantity,
                COALESCE(dbo.fn_StokSatisFiyati(product.sto_kod, '1', warehouse.dep_no, '1'), 0) AS SalesPrice,
                COALESCE(detail.sdp_satisdursun, product.sto_satis_dursun) AS SalesBlockCode,
                COALESCE(detail.sdp_sipdursun, product.sto_siparis_dursun) AS OrderBlockCode,
                COALESCE(detail.sdp_malkabuldursun, product.sto_malkabul_dursun) AS GoodsAcceptanceBlockCode,
                COALESCE(detail.sdp_Pasif_fl, product.sto_pasif_fl, 0) AS IsPassive
            FROM ProductRows AS product
            INNER JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON (@warehouseNo IS NULL OR warehouse.dep_no = @warehouseNo)
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                ON detail.sdp_depo_kod = product.sto_kod
               AND detail.sdp_depo_no = warehouse.dep_no
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = product.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY
                    CASE WHEN COALESCE(bar.bar_master, 0) = 1 THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(bar.bar_birimpntr, 0) = 1 THEN 0 ELSE 1 END,
                    bar.bar_create_date DESC
            ) AS barcode
            CROSS APPLY (
                SELECT COALESCE(dbo.fn_DepodakiMiktar(product.sto_kod, warehouse.dep_no, @reportDate), 0) AS Quantity
            ) AS quantity
            WHERE COALESCE(warehouse.dep_iptal, 0) = 0
              AND COALESCE(warehouse.dep_envanter_harici_fl, 0) = 0
              AND (@onlyWithStock = 0 OR ABS(quantity.Quantity) > 0.000001)
            ORDER BY
                CASE WHEN ABS(quantity.Quantity) > 0.000001 THEN 0 ELSE 1 END,
                warehouse.dep_no;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", normalized.WarehouseNo, DbType.Int32);
                AddParameter(command, "@reportDate", normalized.ReportDate, DbType.DateTime);
                AddParameter(command, "@stockCodeOrBarcode", normalized.StockCodeOrBarcode, DbType.String);
                AddParameter(command, "@onlyWithStock", normalized.OnlyWithStock, DbType.Boolean);
                AddParameter(command, "@take", normalized.Take, DbType.Int32);
            },
            ReadProductWarehouseStock,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockCardDetailDto>> GetStockCardDetailsAsync(
        StockCardDetailRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);

        const string sql = """
            SELECT TOP (@take)
                @warehouseNo AS WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                stock.sto_kod AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                COALESCE(stock.sto_birim1_ad, '') AS Unit1Name,
                CASE WHEN COALESCE(stock.sto_birim1_katsayi, 0) = 0 THEN 1 ELSE stock.sto_birim1_katsayi END AS Unit1Multiplier,
                COALESCE(stock.sto_birim2_ad, '') AS Unit2Name,
                CASE WHEN COALESCE(stock.sto_birim2_katsayi, 0) = 0 THEN 1 ELSE stock.sto_birim2_katsayi END AS Unit2Multiplier,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''),
                    ''
                ) AS SupplierCode,
                COALESCE(supplier.cari_unvan1, '') AS SupplierName,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''),
                    ''
                ) AS ProductManagerCode,
                COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '') AS ProductManagerName,
                COALESCE(stock.sto_kategori_kodu, '') AS CategoryCode,
                COALESCE(stock.sto_reyon_kodu, '') AS RayonCode,
                COALESCE(stock.sto_uretici_kodu, '') AS ProducerCode,
                COALESCE(stock.sto_model_kodu, '') AS ModelCode,
                COALESCE(stock.sto_marka_kodu, '') AS BrandCode,
                CASE
                    WHEN @warehouseNo IS NULL THEN 0
                    ELSE COALESCE(dbo.fn_StokSatisFiyati(stock.sto_kod, '1', @warehouseNo, '1'), 0)
                END AS SalesPrice,
                COALESCE(detail.sdp_satisdursun, stock.sto_satis_dursun) AS SalesBlockCode,
                COALESCE(detail.sdp_sipdursun, stock.sto_siparis_dursun) AS OrderBlockCode,
                COALESCE(detail.sdp_malkabuldursun, stock.sto_malkabul_dursun) AS GoodsAcceptanceBlockCode,
                COALESCE(detail.sdp_Pasif_fl, stock.sto_pasif_fl, 0) AS IsPassive,
                COALESCE(stock.sto_iptal, 0) AS IsDeleted
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                ON detail.sdp_depo_kod = stock.sto_kod
               AND detail.sdp_depo_no = @warehouseNo
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON warehouse.dep_no = @warehouseNo
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = stock.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY
                    CASE WHEN COALESCE(bar.bar_master, 0) = 1 THEN 0 ELSE 1 END,
                    CASE WHEN COALESCE(bar.bar_birimpntr, 0) = 1 THEN 0 ELSE 1 END,
                    bar.bar_create_date DESC
            ) AS barcode
            LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                ON supplier.cari_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                )
            LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI AS personnel WITH (NOLOCK)
                ON personnel.cari_per_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')
                )
            WHERE (@includeDeleted = 1 OR COALESCE(stock.sto_iptal, 0) = 0)
              AND (@barcode IS NULL OR EXISTS (
                    SELECT 1
                    FROM dbo.BARKOD_TANIMLARI AS code WITH (NOLOCK)
                    WHERE code.bar_stokkodu = stock.sto_kod
                      AND code.bar_kodu LIKE @barcodeLike
                      AND COALESCE(code.bar_iptal, 0) = 0
              ))
              AND (@stockCode IS NULL OR stock.sto_kod LIKE @stockCodeLike)
              AND (@stockName IS NULL OR stock.sto_isim LIKE @stockNameLike)
              AND (
                    @supplierCode IS NULL
                    OR COALESCE(
                        NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                        NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                    ) = @supplierCode
              )
              AND (
                    @productManagerCode IS NULL
                    OR COALESCE(
                        NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                        NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')
                    ) = @productManagerCode
              )
            ORDER BY stock.sto_isim, stock.sto_kod;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", normalized.WarehouseNo, DbType.Int32);
                AddParameter(command, "@barcode", normalized.Barcode, DbType.String);
                AddParameter(command, "@barcodeLike", ToLike(normalized.Barcode), DbType.String);
                AddParameter(command, "@stockCode", normalized.StockCode, DbType.String);
                AddParameter(command, "@stockCodeLike", ToLike(normalized.StockCode), DbType.String);
                AddParameter(command, "@stockName", normalized.StockName, DbType.String);
                AddParameter(command, "@stockNameLike", ToLike(normalized.StockName), DbType.String);
                AddParameter(command, "@supplierCode", normalized.SupplierCode, DbType.String);
                AddParameter(command, "@productManagerCode", normalized.ProductManagerCode, DbType.String);
                AddParameter(command, "@includeDeleted", false, DbType.Boolean);
                AddParameter(command, "@take", normalized.Take, DbType.Int32);
            },
            ReadStockCardDetail,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<WarehouseMissingStockDto>> GetWarehouseHasBranchMissingAsync(
        WarehouseMissingStockRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceWarehouseNo <= 0 || request.TargetWarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.");
        }

        var reportDate = NormalizeReportDate(request.ReportDate);
        var search = NormalizeOrNull(request.Search);
        var modelCode = NormalizeOrNull(request.ModelCode);

        const string sql = """
            SELECT TOP (@take)
                @sourceWarehouseNo AS SourceWarehouseNo,
                COALESCE(sourceWarehouse.dep_adi, '') AS SourceWarehouseName,
                @targetWarehouseNo AS TargetWarehouseNo,
                COALESCE(targetWarehouse.dep_adi, '') AS TargetWarehouseName,
                stock.sto_kod AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                COALESCE(stock.sto_birim1_ad, '') AS UnitName,
                sourceQuantity.Quantity AS SourceQuantity,
                targetQuantity.Quantity AS TargetQuantity,
                COALESCE(dbo.fn_StokSatisFiyati(stock.sto_kod, '1', @targetWarehouseNo, '1'), 0) AS SalesPrice,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(targetDetail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''),
                    ''
                ) AS SupplierCode,
                COALESCE(supplier.cari_unvan1, '') AS SupplierName,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(targetDetail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''),
                    ''
                ) AS ProductManagerCode,
                COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '') AS ProductManagerName,
                COALESCE(stock.sto_model_kodu, '') AS ModelCode
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS targetDetail WITH (NOLOCK)
                ON targetDetail.sdp_depo_kod = stock.sto_kod
               AND targetDetail.sdp_depo_no = @targetWarehouseNo
            LEFT JOIN dbo.DEPOLAR AS sourceWarehouse WITH (NOLOCK)
                ON sourceWarehouse.dep_no = @sourceWarehouseNo
            LEFT JOIN dbo.DEPOLAR AS targetWarehouse WITH (NOLOCK)
                ON targetWarehouse.dep_no = @targetWarehouseNo
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = stock.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY COALESCE(bar.bar_master, 0) DESC, bar.bar_create_date DESC
            ) AS barcode
            CROSS APPLY (
                SELECT COALESCE(dbo.fn_DepodakiMiktar(stock.sto_kod, @sourceWarehouseNo, @reportDate), 0) AS Quantity
            ) AS sourceQuantity
            CROSS APPLY (
                SELECT COALESCE(dbo.fn_DepodakiMiktar(stock.sto_kod, @targetWarehouseNo, @reportDate), 0) AS Quantity
            ) AS targetQuantity
            LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                ON supplier.cari_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(targetDetail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                )
            LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI AS personnel WITH (NOLOCK)
                ON personnel.cari_per_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(targetDetail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')
                )
            WHERE COALESCE(stock.sto_iptal, 0) = 0
              AND sourceQuantity.Quantity > 0
              AND targetQuantity.Quantity <= 0
              AND (@modelCode IS NULL OR stock.sto_model_kodu = @modelCode)
              AND (
                    @search IS NULL
                    OR stock.sto_kod LIKE @searchLike
                    OR stock.sto_isim LIKE @searchLike
                    OR barcode.bar_kodu LIKE @searchLike
              )
            ORDER BY sourceQuantity.Quantity DESC, stock.sto_isim;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@sourceWarehouseNo", request.SourceWarehouseNo, DbType.Int32);
                AddParameter(command, "@targetWarehouseNo", request.TargetWarehouseNo, DbType.Int32);
                AddParameter(command, "@reportDate", reportDate, DbType.DateTime);
                AddParameter(command, "@search", search, DbType.String);
                AddParameter(command, "@searchLike", ToLike(search), DbType.String);
                AddParameter(command, "@modelCode", modelCode, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadWarehouseMissingStock,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<WarehouseZeroStockDto>> GetWarehouseZeroStocksAsync(
        WarehouseZeroStockRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var reportDate = NormalizeReportDate(request.ReportDate);
        var modelCode = NormalizeOrNull(request.ModelCode);

        const string sql = """
            SELECT TOP (@take)
                @warehouseNo AS WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                stock.sto_kod AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                COALESCE(stock.sto_birim1_ad, '') AS UnitName,
                quantity.Quantity,
                COALESCE(dbo.fn_StokSatisFiyati(stock.sto_kod, '1', @warehouseNo, '1'), 0) AS SalesPrice,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''),
                    ''
                ) AS SupplierCode,
                COALESCE(supplier.cari_unvan1, '') AS SupplierName,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''),
                    ''
                ) AS ProductManagerCode,
                COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '') AS ProductManagerName,
                COALESCE(stock.sto_model_kodu, '') AS ModelCode
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                ON detail.sdp_depo_kod = stock.sto_kod
               AND detail.sdp_depo_no = @warehouseNo
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON warehouse.dep_no = @warehouseNo
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = stock.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY COALESCE(bar.bar_master, 0) DESC, bar.bar_create_date DESC
            ) AS barcode
            CROSS APPLY (
                SELECT COALESCE(dbo.fn_DepodakiMiktar(stock.sto_kod, @warehouseNo, @reportDate), 0) AS Quantity
            ) AS quantity
            LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                ON supplier.cari_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                )
            LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI AS personnel WITH (NOLOCK)
                ON personnel.cari_per_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')
                )
            WHERE COALESCE(stock.sto_iptal, 0) = 0
              AND quantity.Quantity <= 0
              AND (@modelCode IS NULL OR stock.sto_model_kodu = @modelCode)
            ORDER BY stock.sto_model_kodu, stock.sto_isim;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@reportDate", reportDate, DbType.DateTime);
                AddParameter(command, "@modelCode", modelCode, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadWarehouseZeroStock,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<StockMovementReportItemDto>> GetStockMovementsAsync(
        StockMovementReportRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.StartDate, request.EndDate);
        var stockCode = NormalizeOrNull(request.StockCode);

        const string sql = """
            SELECT TOP (@take)
                movement.sth_Guid AS MovementGuid,
                movement.sth_tarih AS MovementDate,
                movement.sth_giris_depo_no AS InputWarehouseNo,
                COALESCE(inputWarehouse.dep_adi, '') AS InputWarehouseName,
                movement.sth_cikis_depo_no AS OutputWarehouseNo,
                COALESCE(outputWarehouse.dep_adi, '') AS OutputWarehouseName,
                COALESCE(movement.sth_stok_kod, '') AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(movement.sth_evrakno_seri, '') AS DocumentSerie,
                COALESCE(movement.sth_evrakno_sira, 0) AS DocumentOrderNo,
                COALESCE(movement.sth_belge_no, '') AS DocumentNo,
                COALESCE(movement.sth_tip, 0) AS MovementType,
                COALESCE(movement.sth_cins, 0) AS MovementKind,
                COALESCE(movement.sth_evraktip, 0) AS DocumentType,
                COALESCE(movement.sth_normal_iade, 0) AS NormalReturn,
                COALESCE(movement.sth_miktar, 0) AS Quantity,
                COALESCE(movement.sth_tutar, 0) AS Amount,
                COALESCE(movement.sth_cari_kodu, '') AS CustomerCode,
                COALESCE(movement.sth_aciklama, '') AS Description
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.DEPOLAR AS inputWarehouse WITH (NOLOCK)
                ON inputWarehouse.dep_no = movement.sth_giris_depo_no
            LEFT JOIN dbo.DEPOLAR AS outputWarehouse WITH (NOLOCK)
                ON outputWarehouse.dep_no = movement.sth_cikis_depo_no
            WHERE COALESCE(movement.sth_iptal, 0) = 0
              AND movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND (@warehouseNo IS NULL OR movement.sth_giris_depo_no = @warehouseNo OR movement.sth_cikis_depo_no = @warehouseNo)
              AND (@stockCode IS NULL OR movement.sth_stok_kod = @stockCode)
            ORDER BY movement.sth_tarih DESC, movement.sth_create_date DESC;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", startDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", endDateExclusive, DbType.DateTime);
                AddParameter(command, "@stockCode", stockCode, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadStockMovement,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<MovementInOutComparisonDto>> GetInOutComparisonAsync(
        MovementInOutComparisonRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.StartDate, request.EndDate);
        var filterType = NormalizeFilterType(request.FilterType);
        var filterValue = NormalizeOrNull(request.FilterValue);

        const string sql = """
            ;WITH FilteredStock AS (
                SELECT
                    stock.sto_kod,
                    stock.sto_isim,
                    stock.sto_kategori_kodu,
                    stock.sto_uretici_kodu,
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                        NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''),
                        ''
                    ) AS SupplierCode,
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                        NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''),
                        ''
                    ) AS ProductManagerCode
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                    ON detail.sdp_depo_kod = stock.sto_kod
                   AND detail.sdp_depo_no = @warehouseNo
                WHERE COALESCE(stock.sto_iptal, 0) = 0
                  AND (
                        @filterType IS NULL
                        OR (@filterType = 'stock' AND stock.sto_kod = @filterValue)
                        OR (@filterType = 'category' AND stock.sto_kategori_kodu = @filterValue)
                        OR (@filterType = 'producer' AND stock.sto_uretici_kodu = @filterValue)
                        OR (@filterType = 'supplier' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''), NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')) = @filterValue)
                        OR (@filterType = 'product-manager' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')) = @filterValue)
                        OR (@filterType = 'model' AND stock.sto_model_kodu = @filterValue)
                  )
            ),
            MovementRows AS (
                SELECT
                    movement.sth_stok_kod AS StockCode,
                    CASE
                        WHEN movement.sth_tip = 0 AND movement.sth_cins = 0 AND COALESCE(movement.sth_normal_iade, 0) = 0 THEN 'purchase'
                        WHEN movement.sth_tip = 1 AND movement.sth_cins = 1 AND COALESCE(movement.sth_normal_iade, 0) = 0 THEN 'sale'
                        WHEN COALESCE(movement.sth_normal_iade, 0) = 1 THEN 'return'
                        ELSE 'other'
                    END AS Kind,
                    COALESCE(movement.sth_miktar, 0) AS Quantity,
                    COALESCE(movement.sth_tutar, 0) AS Amount
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                INNER JOIN FilteredStock AS stock ON stock.sto_kod = movement.sth_stok_kod
                WHERE COALESCE(movement.sth_iptal, 0) = 0
                  AND movement.sth_tarih >= @startDate
                  AND movement.sth_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR movement.sth_giris_depo_no = @warehouseNo OR movement.sth_cikis_depo_no = @warehouseNo)
            )
            SELECT TOP (@take)
                stock.sto_kod AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                stock.SupplierCode,
                COALESCE(supplier.cari_unvan1, '') AS SupplierName,
                COALESCE(stock.sto_kategori_kodu, '') AS CategoryCode,
                COALESCE(stock.sto_uretici_kodu, '') AS ProducerCode,
                stock.ProductManagerCode,
                COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '') AS ProductManagerName,
                SUM(CASE WHEN rows.Kind = 'purchase' THEN rows.Quantity ELSE 0 END) AS PurchaseQuantity,
                SUM(CASE WHEN rows.Kind = 'purchase' THEN rows.Amount ELSE 0 END) AS PurchaseAmount,
                SUM(CASE WHEN rows.Kind = 'sale' THEN rows.Quantity ELSE 0 END) AS SalesQuantity,
                SUM(CASE WHEN rows.Kind = 'sale' THEN rows.Amount ELSE 0 END) AS SalesAmount,
                SUM(CASE WHEN rows.Kind = 'return' THEN rows.Quantity ELSE 0 END) AS ReturnQuantity,
                SUM(CASE WHEN rows.Kind = 'return' THEN rows.Amount ELSE 0 END) AS ReturnAmount
            FROM FilteredStock AS stock
            LEFT JOIN MovementRows AS rows ON rows.StockCode = stock.sto_kod
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = stock.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY COALESCE(bar.bar_master, 0) DESC, bar.bar_create_date DESC
            ) AS barcode
            LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                ON supplier.cari_kod = stock.SupplierCode
            LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI AS personnel WITH (NOLOCK)
                ON personnel.cari_per_kod = stock.ProductManagerCode
            GROUP BY
                stock.sto_kod,
                stock.sto_isim,
                barcode.bar_kodu,
                stock.SupplierCode,
                supplier.cari_unvan1,
                stock.sto_kategori_kodu,
                stock.sto_uretici_kodu,
                stock.ProductManagerCode,
                personnel.cari_per_adi,
                personnel.cari_per_soyadi
            HAVING
                SUM(CASE WHEN rows.Kind IN ('purchase', 'sale', 'return') THEN ABS(rows.Quantity) ELSE 0 END) > 0
            ORDER BY SalesAmount DESC, PurchaseAmount DESC, stock.sto_isim;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", startDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", endDateExclusive, DbType.DateTime);
                AddParameter(command, "@filterType", filterType, DbType.String);
                AddParameter(command, "@filterValue", filterValue, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadInOutComparison,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<BranchSalesReportItemDto>> GetBranchSalesAsync(
        BranchSalesReportRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.StartDate, request.EndDate);
        var filterType = NormalizeFilterType(request.FilterType);
        var filterValue = NormalizeOrNull(request.FilterValue);

        const string sql = """
            SELECT TOP (@take)
                movement.sth_cikis_depo_no AS WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                COALESCE(movement.sth_stok_kod, '') AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                SUM(COALESCE(movement.sth_miktar, 0)) AS Quantity,
                SUM(COALESCE(movement.sth_tutar, 0)) AS Amount,
                SUM(COALESCE(movement.sth_vergi, 0)) AS TaxAmount,
                COALESCE(dbo.fn_DepodakiMiktar(movement.sth_stok_kod, movement.sth_cikis_depo_no, CONVERT(date, GETDATE())), 0) AS CurrentStock
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                ON detail.sdp_depo_kod = stock.sto_kod
               AND detail.sdp_depo_no = movement.sth_cikis_depo_no
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON warehouse.dep_no = movement.sth_cikis_depo_no
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = stock.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY COALESCE(bar.bar_master, 0) DESC, bar.bar_create_date DESC
            ) AS barcode
            WHERE COALESCE(movement.sth_iptal, 0) = 0
              AND movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND movement.sth_tip = 1
              AND movement.sth_cins = 1
              AND COALESCE(movement.sth_normal_iade, 0) = 0
              AND movement.sth_evraktip IN (1, 4)
              AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo)
              AND (
                    @filterType IS NULL
                    OR (@filterType = 'stock' AND stock.sto_kod = @filterValue)
                    OR (@filterType = 'category' AND stock.sto_kategori_kodu = @filterValue)
                    OR (@filterType = 'producer' AND stock.sto_uretici_kodu = @filterValue)
                    OR (@filterType = 'supplier' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''), NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')) = @filterValue)
                    OR (@filterType = 'product-manager' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')) = @filterValue)
                    OR (@filterType = 'model' AND stock.sto_model_kodu = @filterValue)
              )
            GROUP BY
                movement.sth_cikis_depo_no,
                warehouse.dep_adi,
                movement.sth_stok_kod,
                stock.sto_isim,
                barcode.bar_kodu
            ORDER BY Amount DESC, Quantity DESC;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", startDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", endDateExclusive, DbType.DateTime);
                AddParameter(command, "@filterType", filterType, DbType.String);
                AddParameter(command, "@filterValue", filterValue, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadBranchSales,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<YearSalesComparisonItemDto>> GetYearSalesComparisonAsync(
        YearSalesComparisonRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.StartDate, request.EndDate);
        var previousStartDate = startDate.AddYears(-1);
        var previousEndDateExclusive = endDateExclusive.AddYears(-1);
        var filterType = NormalizeFilterType(request.FilterType);
        var filterValue = NormalizeOrNull(request.FilterValue);

        const string sql = """
            ;WITH SalesRows AS (
                SELECT
                    stock.sto_kod AS StockCode,
                    COALESCE(stock.sto_isim, '') AS StockName,
                    CASE
                        WHEN movement.sth_tarih >= @startDate AND movement.sth_tarih < @endDateExclusive THEN 'current'
                        ELSE 'previous'
                    END AS Period,
                    SUM(COALESCE(movement.sth_miktar, 0)) AS Quantity,
                    SUM(COALESCE(movement.sth_tutar, 0)) AS Amount
                FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
                INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                    ON stock.sto_kod = movement.sth_stok_kod
                LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                    ON detail.sdp_depo_kod = stock.sto_kod
                   AND detail.sdp_depo_no = movement.sth_cikis_depo_no
                WHERE COALESCE(movement.sth_iptal, 0) = 0
                  AND (
                        (movement.sth_tarih >= @startDate AND movement.sth_tarih < @endDateExclusive)
                        OR (movement.sth_tarih >= @previousStartDate AND movement.sth_tarih < @previousEndDateExclusive)
                  )
                  AND movement.sth_tip = 1
                  AND movement.sth_cins = 1
                  AND COALESCE(movement.sth_normal_iade, 0) = 0
                  AND movement.sth_evraktip IN (1, 4)
                  AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo)
                  AND (
                        @filterType IS NULL
                        OR (@filterType = 'stock' AND stock.sto_kod = @filterValue)
                        OR (@filterType = 'category' AND stock.sto_kategori_kodu = @filterValue)
                        OR (@filterType = 'producer' AND stock.sto_uretici_kodu = @filterValue)
                        OR (@filterType = 'supplier' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''), NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')) = @filterValue)
                        OR (@filterType = 'product-manager' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')) = @filterValue)
                        OR (@filterType = 'model' AND stock.sto_model_kodu = @filterValue)
                  )
                GROUP BY
                    stock.sto_kod,
                    stock.sto_isim,
                    CASE
                        WHEN movement.sth_tarih >= @startDate AND movement.sth_tarih < @endDateExclusive THEN 'current'
                        ELSE 'previous'
                    END
            )
            SELECT TOP (@take)
                rows.StockCode,
                rows.StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                SUM(CASE WHEN rows.Period = 'current' THEN rows.Quantity ELSE 0 END) AS CurrentQuantity,
                SUM(CASE WHEN rows.Period = 'current' THEN rows.Amount ELSE 0 END) AS CurrentAmount,
                SUM(CASE WHEN rows.Period = 'previous' THEN rows.Quantity ELSE 0 END) AS PreviousQuantity,
                SUM(CASE WHEN rows.Period = 'previous' THEN rows.Amount ELSE 0 END) AS PreviousAmount
            FROM SalesRows AS rows
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = rows.StockCode
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY COALESCE(bar.bar_master, 0) DESC, bar.bar_create_date DESC
            ) AS barcode
            GROUP BY rows.StockCode, rows.StockName, barcode.bar_kodu
            ORDER BY CurrentAmount DESC, PreviousAmount DESC;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", startDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", endDateExclusive, DbType.DateTime);
                AddParameter(command, "@previousStartDate", previousStartDate, DbType.DateTime);
                AddParameter(command, "@previousEndDateExclusive", previousEndDateExclusive, DbType.DateTime);
                AddParameter(command, "@filterType", filterType, DbType.String);
                AddParameter(command, "@filterValue", filterValue, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadYearSalesComparison,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<ReturnBranchReportItemDto>> GetReturnBranchesAsync(
        ReturnBranchReportRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.StartDate, request.EndDate);
        var stockCode = NormalizeOrNull(request.StockCode)
            ?? throw new ArgumentException("Stock code is required.", nameof(request.StockCode));

        const string sql = """
            SELECT TOP (@take)
                COALESCE(movement.sth_giris_depo_no, movement.sth_cikis_depo_no, 0) AS WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                COALESCE(movement.sth_stok_kod, '') AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                movement.sth_tarih AS ReturnDate,
                COALESCE(movement.sth_evrakno_seri, '') AS DocumentSerie,
                COALESCE(movement.sth_evrakno_sira, 0) AS DocumentOrderNo,
                COALESCE(movement.sth_belge_no, '') AS DocumentNo,
                COALESCE(movement.sth_miktar, 0) AS Quantity,
                COALESCE(movement.sth_tutar, 0) AS Amount,
                COALESCE(movement.sth_cari_kodu, '') AS CustomerCode
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON warehouse.dep_no = COALESCE(movement.sth_giris_depo_no, movement.sth_cikis_depo_no)
            WHERE COALESCE(movement.sth_iptal, 0) = 0
              AND movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND movement.sth_stok_kod = @stockCode
              AND COALESCE(movement.sth_normal_iade, 0) = 1
              AND (@warehouseNo IS NULL OR movement.sth_giris_depo_no = @warehouseNo OR movement.sth_cikis_depo_no = @warehouseNo)
            ORDER BY movement.sth_tarih DESC, movement.sth_create_date DESC;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", startDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", endDateExclusive, DbType.DateTime);
                AddParameter(command, "@stockCode", stockCode, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadReturnBranch,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<NotSoldProductReportItemDto>> GetNotSoldProductsAsync(
        NotSoldProductReportRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.StartDate, request.EndDate);
        var productManagerCode = NormalizeOrNull(request.ProductManagerCode);

        const string sql = """
            SELECT TOP (@take)
                @warehouseNo AS WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                stock.sto_kod AS StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(barcode.bar_kodu, '') AS Barcode,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''),
                    ''
                ) AS SupplierCode,
                COALESCE(supplier.cari_unvan1, '') AS SupplierName,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''),
                    ''
                ) AS ProductManagerCode,
                COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '') AS ProductManagerName,
                CASE
                    WHEN @warehouseNo IS NULL THEN 0
                    ELSE COALESCE(dbo.fn_DepodakiMiktar(stock.sto_kod, @warehouseNo, CONVERT(date, GETDATE())), 0)
                END AS CurrentStock,
                lastSale.LastSaleDate
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                ON detail.sdp_depo_kod = stock.sto_kod
               AND detail.sdp_depo_no = @warehouseNo
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON warehouse.dep_no = @warehouseNo
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = stock.sto_kod
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY COALESCE(bar.bar_master, 0) DESC, bar.bar_create_date DESC
            ) AS barcode
            OUTER APPLY (
                SELECT MAX(sale.sth_tarih) AS LastSaleDate
                FROM dbo.STOK_HAREKETLERI AS sale WITH (NOLOCK)
                WHERE sale.sth_stok_kod = stock.sto_kod
                  AND COALESCE(sale.sth_iptal, 0) = 0
                  AND sale.sth_tip = 1
                  AND sale.sth_cins = 1
                  AND COALESCE(sale.sth_normal_iade, 0) = 0
                  AND sale.sth_evraktip IN (1, 4)
                  AND (@warehouseNo IS NULL OR sale.sth_cikis_depo_no = @warehouseNo)
            ) AS lastSale
            LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                ON supplier.cari_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')
                )
            LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI AS personnel WITH (NOLOCK)
                ON personnel.cari_per_kod = COALESCE(
                    NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''),
                    NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')
                )
            WHERE COALESCE(stock.sto_iptal, 0) = 0
              AND COALESCE(stock.sto_pasif_fl, 0) = 0
              AND (@includeDls = 1 OR (stock.sto_isim NOT LIKE 'DLS%' AND stock.sto_isim NOT LIKE 'SRF%'))
              AND (
                    @productManagerCode IS NULL
                    OR COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')) = @productManagerCode
              )
              AND NOT EXISTS (
                    SELECT 1
                    FROM dbo.STOK_HAREKETLERI AS sale WITH (NOLOCK)
                    WHERE sale.sth_stok_kod = stock.sto_kod
                      AND COALESCE(sale.sth_iptal, 0) = 0
                      AND sale.sth_tarih >= @startDate
                      AND sale.sth_tarih < @endDateExclusive
                      AND sale.sth_tip = 1
                      AND sale.sth_cins = 1
                      AND COALESCE(sale.sth_normal_iade, 0) = 0
                      AND sale.sth_evraktip IN (1, 4)
                      AND (@warehouseNo IS NULL OR sale.sth_cikis_depo_no = @warehouseNo)
              )
            ORDER BY lastSale.LastSaleDate DESC, stock.sto_isim;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", startDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", endDateExclusive, DbType.DateTime);
                AddParameter(command, "@productManagerCode", productManagerCode, DbType.String);
                AddParameter(command, "@includeDls", request.IncludeDls, DbType.Boolean);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadNotSoldProduct,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProfitabilityReportItemDto>> GetProfitabilityAsync(
        ProfitabilityReportRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDateExclusive) = NormalizeDateRange(request.StartDate, request.EndDate);
        var scope = NormalizeProfitabilityScope(request.Scope);
        var filterValue = NormalizeOrNull(request.FilterValue);

        const string sql = """
            SELECT TOP (@take)
                CASE
                    WHEN @scope = 'producer' THEN COALESCE(stock.sto_uretici_kodu, '')
                    WHEN @scope = 'supplier' THEN COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''), NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''), '')
                    WHEN @scope = 'product-manager' THEN COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''), '')
                    WHEN @scope = 'category' THEN COALESCE(stock.sto_kategori_kodu, '')
                    ELSE COALESCE(stock.sto_kod, '')
                END AS GroupCode,
                CASE
                    WHEN @scope = 'supplier' THEN COALESCE(supplier.cari_unvan1, '')
                    WHEN @scope = 'product-manager' THEN COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '')
                    ELSE COALESCE(stock.sto_isim, '')
                END AS GroupName,
                SUM(COALESCE(movement.sth_miktar, 0)) AS SalesQuantity,
                SUM(COALESCE(movement.sth_tutar, 0)) AS SalesAmount,
                SUM(ABS(COALESCE(movement.sth_maliyet_ana, 0))) AS CostAmount
            FROM dbo.STOK_HAREKETLERI AS movement WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = movement.sth_stok_kod
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                ON detail.sdp_depo_kod = stock.sto_kod
               AND detail.sdp_depo_no = movement.sth_cikis_depo_no
            LEFT JOIN dbo.CARI_HESAPLAR AS supplier WITH (NOLOCK)
                ON supplier.cari_kod = COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''), NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''))
            LEFT JOIN dbo.CARI_PERSONEL_TANIMLARI AS personnel WITH (NOLOCK)
                ON personnel.cari_per_kod = COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''))
            WHERE COALESCE(movement.sth_iptal, 0) = 0
              AND movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND movement.sth_tip = 1
              AND movement.sth_cins = 1
              AND COALESCE(movement.sth_normal_iade, 0) = 0
              AND movement.sth_evraktip IN (1, 4)
              AND (@warehouseNo IS NULL OR movement.sth_cikis_depo_no = @warehouseNo)
              AND (
                    @filterValue IS NULL
                    OR (@scope = 'producer' AND stock.sto_uretici_kodu = @filterValue)
                    OR (@scope = 'supplier' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''), NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), '')) = @filterValue)
                    OR (@scope = 'product-manager' AND COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), '')) = @filterValue)
                    OR (@scope = 'category' AND stock.sto_kategori_kodu = @filterValue)
                    OR (@scope = 'stock' AND stock.sto_kod = @filterValue)
              )
            GROUP BY
                CASE
                    WHEN @scope = 'producer' THEN COALESCE(stock.sto_uretici_kodu, '')
                    WHEN @scope = 'supplier' THEN COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_sat_cari_kod)), ''), NULLIF(LTRIM(RTRIM(stock.sto_sat_cari_kod)), ''), '')
                    WHEN @scope = 'product-manager' THEN COALESCE(NULLIF(LTRIM(RTRIM(detail.sdp_UrunSorumlusuKodu)), ''), NULLIF(LTRIM(RTRIM(stock.sto_urun_sorkod)), ''), '')
                    WHEN @scope = 'category' THEN COALESCE(stock.sto_kategori_kodu, '')
                    ELSE COALESCE(stock.sto_kod, '')
                END,
                CASE
                    WHEN @scope = 'supplier' THEN COALESCE(supplier.cari_unvan1, '')
                    WHEN @scope = 'product-manager' THEN COALESCE(NULLIF(CONCAT(personnel.cari_per_adi, ' ', personnel.cari_per_soyadi), ' '), '')
                    ELSE COALESCE(stock.sto_isim, '')
                END
            ORDER BY SalesAmount DESC;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@startDate", startDate, DbType.DateTime);
                AddParameter(command, "@endDateExclusive", endDateExclusive, DbType.DateTime);
                AddParameter(command, "@scope", scope, DbType.String);
                AddParameter(command, "@filterValue", filterValue, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadProfitability,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<CountingComparisonReportItemDto>> GetCountingComparisonAsync(
        CountingComparisonReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var countDate = NormalizeReportDate(request.CountDate);
        var packageCode = NormalizeOrNull(request.PackageCode);

        const string sql = """
            ;WITH CountRows AS (
                SELECT
                    result.sym_depono AS WarehouseNo,
                    result.sym_tarihi AS CountDate,
                    COALESCE(result.sym_evrakno, 0) AS DocumentNo,
                    result.sym_Stokkodu AS StockCode,
                    SUM(COALESCE(result.sym_miktar1, 0)) AS CountQuantity,
                    MAX(COALESCE(result.sym_barkod, '')) AS Barcode
                FROM dbo.SAYIM_SONUCLARI AS result WITH (NOLOCK)
                INNER JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                    ON stock.sto_kod = result.sym_Stokkodu
                WHERE COALESCE(result.sym_iptal, 0) = 0
                  AND result.sym_depono = @warehouseNo
                  AND result.sym_tarihi >= @countDate
                  AND result.sym_tarihi < DATEADD(DAY, 1, @countDate)
                  AND (@documentNo IS NULL OR result.sym_evrakno = @documentNo)
                  AND (@packageCode IS NULL OR stock.sto_ambalaj_kodu = @packageCode)
                GROUP BY
                    result.sym_depono,
                    result.sym_tarihi,
                    COALESCE(result.sym_evrakno, 0),
                    result.sym_Stokkodu
            )
            SELECT TOP (@take)
                rows.WarehouseNo,
                COALESCE(warehouse.dep_adi, '') AS WarehouseName,
                rows.CountDate,
                rows.DocumentNo,
                rows.StockCode,
                COALESCE(stock.sto_isim, '') AS StockName,
                COALESCE(NULLIF(rows.Barcode, ''), barcode.bar_kodu, '') AS Barcode,
                COALESCE(stock.sto_birim1_ad, '') AS UnitName,
                rows.CountQuantity,
                COALESCE(dbo.fn_DepodakiMiktar(rows.StockCode, rows.WarehouseNo, rows.CountDate), 0) AS SystemQuantity,
                COALESCE(dbo.fn_StokSatisFiyati(rows.StockCode, '1', rows.WarehouseNo, '1'), 0) AS SalesPrice
            FROM CountRows AS rows
            LEFT JOIN dbo.STOKLAR AS stock WITH (NOLOCK)
                ON stock.sto_kod = rows.StockCode
            LEFT JOIN dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                ON warehouse.dep_no = rows.WarehouseNo
            OUTER APPLY (
                SELECT TOP (1) bar.bar_kodu
                FROM dbo.BARKOD_TANIMLARI AS bar WITH (NOLOCK)
                WHERE bar.bar_stokkodu = rows.StockCode
                  AND COALESCE(bar.bar_iptal, 0) = 0
                ORDER BY COALESCE(bar.bar_master, 0) DESC, bar.bar_create_date DESC
            ) AS barcode
            ORDER BY ABS(rows.CountQuantity - COALESCE(dbo.fn_DepodakiMiktar(rows.StockCode, rows.WarehouseNo, rows.CountDate), 0)) DESC;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@warehouseNo", request.WarehouseNo, DbType.Int32);
                AddParameter(command, "@countDate", countDate, DbType.DateTime);
                AddParameter(command, "@documentNo", request.DocumentNo, DbType.Int32);
                AddParameter(command, "@packageCode", packageCode, DbType.String);
                AddParameter(command, "@take", NormalizeTake(request.Take), DbType.Int32);
            },
            ReadCountingComparison,
            cancellationToken);
    }

    private async Task<string> GetWarehouseNameAsync(int warehouseNo, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) COALESCE(dep_adi, '') AS WarehouseName
            FROM dbo.DEPOLAR WITH (NOLOCK)
            WHERE dep_no = @warehouseNo;
            """;

        var rows = await ExecuteReaderAsync(
            sql,
            command => AddParameter(command, "@warehouseNo", warehouseNo, DbType.Int32),
            reader => ReadString(reader, "WarehouseName"),
            cancellationToken);

        return rows.FirstOrDefault() ?? string.Empty;
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
            await using var command = connection.CreateCommand();
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

    private static NormalizedStockOnHandReportRequest Normalize(StockOnHandReportRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        return new NormalizedStockOnHandReportRequest(
            request.WarehouseNo,
            NormalizeReportDate(request.ReportDate),
            NormalizeOrNull(request.Search),
            NormalizeOrNull(request.SupplierCode),
            NormalizeOrNull(request.CategoryCode),
            NormalizeOrNull(request.ProducerCode),
            NormalizeOrNull(request.ProductManagerCode),
            NormalizeOrNull(request.ModelCode),
            request.OnlyWithStock,
            NormalizeTake(request.Take));
    }

    private static NormalizedProductWarehouseStockRequest Normalize(ProductWarehouseStockRequest request)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var stockCodeOrBarcode = NormalizeOrNull(request.StockCodeOrBarcode);

        if (stockCodeOrBarcode is null)
        {
            throw new ArgumentException("Stock code or barcode is required.", nameof(request.StockCodeOrBarcode));
        }

        return new NormalizedProductWarehouseStockRequest(
            request.WarehouseNo,
            NormalizeReportDate(request.ReportDate),
            stockCodeOrBarcode,
            request.OnlyWithStock,
            NormalizeTake(request.Take));
    }

    private static NormalizedStockCardDetailRequest Normalize(StockCardDetailRequest request)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var barcode = NormalizeOrNull(request.Barcode);
        var stockCode = NormalizeOrNull(request.StockCode);
        var stockName = NormalizeOrNull(request.StockName);
        var supplierCode = NormalizeOrNull(request.SupplierCode);
        var productManagerCode = NormalizeOrNull(request.ProductManagerCode);

        if (stockName is { Length: < 2 })
        {
            throw new ArgumentException("Stock name search text must be at least 2 characters.", nameof(request.StockName));
        }

        return new NormalizedStockCardDetailRequest(
            request.WarehouseNo,
            barcode,
            stockCode,
            stockName,
            supplierCode,
            productManagerCode,
            NormalizeTake(request.Take));
    }

    private static DateTime NormalizeReportDate(DateTime reportDate) =>
        reportDate == default ? DateTime.Today : reportDate.Date;

    private static (DateTime StartDate, DateTime EndDateExclusive) NormalizeDateRange(
        DateTime startDateValue,
        DateTime endDateValue)
    {
        if (startDateValue == default)
        {
            throw new ArgumentException("Start date is required.", nameof(startDateValue));
        }

        if (endDateValue == default)
        {
            throw new ArgumentException("End date is required.", nameof(endDateValue));
        }

        var startDate = startDateValue.Date;
        var endDate = endDateValue.Date;

        if (endDate < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.");
        }

        return (startDate, endDate.AddDays(1));
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? ToLike(string? value) =>
        value is null ? null : $"%{value}%";

    private static string? NormalizeFilterType(string? value)
    {
        var normalized = NormalizeOrNull(value)?.ToLowerInvariant();

        return normalized switch
        {
            null => null,
            "stok" or "stock" => "stock",
            "kategori" or "category" => "category",
            "uretici" or "üretici" or "producer" => "producer",
            "tedarikci" or "tedarikçi" or "supplier" => "supplier",
            "satin-almaci" or "satinalmaci" or "satın-almacı" or "product-manager" => "product-manager",
            "model" => "model",
            _ => throw new ArgumentException("Unsupported filter type.")
        };
    }

    private static string NormalizeProfitabilityScope(string? value)
    {
        var normalized = NormalizeOrNull(value)?.ToLowerInvariant();

        return normalized switch
        {
            null => "producer",
            "stok" or "stock" => "stock",
            "kategori" or "category" => "category",
            "uretici" or "üretici" or "producer" => "producer",
            "tedarikci" or "tedarikçi" or "supplier" => "supplier",
            "satin-almaci" or "satinalmaci" or "satın-almacı" or "product-manager" => "product-manager",
            _ => throw new ArgumentException("Unsupported profitability scope.")
        };
    }

    private static StockOnHandReportItemDto ReadStockOnHandItem(DbDataReader reader)
    {
        var quantity = Round(ReadDouble(reader, "Quantity"));
        var salesPrice = Round(ReadDouble(reader, "SalesPrice"));

        return new StockOnHandReportItemDto(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "UnitName"),
            quantity,
            salesPrice,
            Round(quantity * salesPrice),
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            ReadString(reader, "ProductManagerCode"),
            ReadString(reader, "ProductManagerName"),
            ReadString(reader, "CategoryCode"),
            ReadString(reader, "RayonCode"),
            ReadString(reader, "ProducerCode"),
            ReadString(reader, "ModelCode"),
            ReadNullableInt(reader, "SalesBlockCode"),
            ReadNullableInt(reader, "OrderBlockCode"),
            ReadNullableInt(reader, "GoodsAcceptanceBlockCode"),
            ReadBool(reader, "IsPassive"));
    }

    private static ProductWarehouseStockDto ReadProductWarehouseStock(DbDataReader reader)
    {
        var quantity = Round(ReadDouble(reader, "Quantity"));
        var salesPrice = Round(ReadDouble(reader, "SalesPrice"));

        return new ProductWarehouseStockDto(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "UnitName"),
            quantity,
            salesPrice,
            Round(quantity * salesPrice),
            ReadNullableInt(reader, "SalesBlockCode"),
            ReadNullableInt(reader, "OrderBlockCode"),
            ReadNullableInt(reader, "GoodsAcceptanceBlockCode"),
            ReadBool(reader, "IsPassive"));
    }

    private static StockCardDetailDto ReadStockCardDetail(DbDataReader reader) =>
        new(
            ReadNullableInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "Unit1Name"),
            Round(ReadDouble(reader, "Unit1Multiplier")),
            ReadString(reader, "Unit2Name"),
            Round(ReadDouble(reader, "Unit2Multiplier")),
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            ReadString(reader, "ProductManagerCode"),
            ReadString(reader, "ProductManagerName"),
            ReadString(reader, "CategoryCode"),
            ReadString(reader, "RayonCode"),
            ReadString(reader, "ProducerCode"),
            ReadString(reader, "ModelCode"),
            ReadString(reader, "BrandCode"),
            Round(ReadDouble(reader, "SalesPrice")),
            ReadNullableInt(reader, "SalesBlockCode"),
            ReadNullableInt(reader, "OrderBlockCode"),
            ReadNullableInt(reader, "GoodsAcceptanceBlockCode"),
            ReadBool(reader, "IsPassive"),
            ReadBool(reader, "IsDeleted"));

    private static WarehouseMissingStockDto ReadWarehouseMissingStock(DbDataReader reader) =>
        new(
            ReadInt(reader, "SourceWarehouseNo"),
            ReadString(reader, "SourceWarehouseName"),
            ReadInt(reader, "TargetWarehouseNo"),
            ReadString(reader, "TargetWarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "UnitName"),
            Round(ReadDouble(reader, "SourceQuantity")),
            Round(ReadDouble(reader, "TargetQuantity")),
            Round(ReadDouble(reader, "SalesPrice")),
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            ReadString(reader, "ProductManagerCode"),
            ReadString(reader, "ProductManagerName"),
            ReadString(reader, "ModelCode"));

    private static WarehouseZeroStockDto ReadWarehouseZeroStock(DbDataReader reader) =>
        new(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "UnitName"),
            Round(ReadDouble(reader, "Quantity")),
            Round(ReadDouble(reader, "SalesPrice")),
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            ReadString(reader, "ProductManagerCode"),
            ReadString(reader, "ProductManagerName"),
            ReadString(reader, "ModelCode"));

    private static StockMovementReportItemDto ReadStockMovement(DbDataReader reader) =>
        new(
            ReadGuid(reader, "MovementGuid"),
            ReadDateTime(reader, "MovementDate"),
            ReadNullableInt(reader, "InputWarehouseNo"),
            ReadString(reader, "InputWarehouseName"),
            ReadNullableInt(reader, "OutputWarehouseNo"),
            ReadString(reader, "OutputWarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "DocumentSerie"),
            ReadInt(reader, "DocumentOrderNo"),
            ReadString(reader, "DocumentNo"),
            ReadInt(reader, "MovementType"),
            ReadInt(reader, "MovementKind"),
            ReadInt(reader, "DocumentType"),
            ReadInt(reader, "NormalReturn"),
            Round(ReadDouble(reader, "Quantity")),
            Round(ReadDouble(reader, "Amount")),
            ReadString(reader, "CustomerCode"),
            ReadString(reader, "Description"));

    private static MovementInOutComparisonDto ReadInOutComparison(DbDataReader reader)
    {
        var purchaseQuantity = Round(ReadDouble(reader, "PurchaseQuantity"));
        var salesQuantity = Round(ReadDouble(reader, "SalesQuantity"));
        var returnQuantity = Round(ReadDouble(reader, "ReturnQuantity"));

        return new MovementInOutComparisonDto(
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            ReadString(reader, "CategoryCode"),
            ReadString(reader, "ProducerCode"),
            ReadString(reader, "ProductManagerCode"),
            ReadString(reader, "ProductManagerName"),
            purchaseQuantity,
            Round(ReadDouble(reader, "PurchaseAmount")),
            salesQuantity,
            Round(ReadDouble(reader, "SalesAmount")),
            returnQuantity,
            Round(ReadDouble(reader, "ReturnAmount")),
            Round(purchaseQuantity - salesQuantity + returnQuantity));
    }

    private static BranchSalesReportItemDto ReadBranchSales(DbDataReader reader) =>
        new(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            Round(ReadDouble(reader, "Quantity")),
            Round(ReadDouble(reader, "Amount")),
            Round(ReadDouble(reader, "TaxAmount")),
            Round(ReadDouble(reader, "CurrentStock")));

    private static YearSalesComparisonItemDto ReadYearSalesComparison(DbDataReader reader)
    {
        var currentQuantity = Round(ReadDouble(reader, "CurrentQuantity"));
        var currentAmount = Round(ReadDouble(reader, "CurrentAmount"));
        var previousQuantity = Round(ReadDouble(reader, "PreviousQuantity"));
        var previousAmount = Round(ReadDouble(reader, "PreviousAmount"));

        return new YearSalesComparisonItemDto(
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            currentQuantity,
            currentAmount,
            previousQuantity,
            previousAmount,
            Round(currentQuantity - previousQuantity),
            Round(currentAmount - previousAmount),
            Percent(currentQuantity - previousQuantity, previousQuantity),
            Percent(currentAmount - previousAmount, previousAmount));
    }

    private static ReturnBranchReportItemDto ReadReturnBranch(DbDataReader reader) =>
        new(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadDateTime(reader, "ReturnDate"),
            ReadString(reader, "DocumentSerie"),
            ReadInt(reader, "DocumentOrderNo"),
            ReadString(reader, "DocumentNo"),
            Round(ReadDouble(reader, "Quantity")),
            Round(ReadDouble(reader, "Amount")),
            ReadString(reader, "CustomerCode"));

    private static NotSoldProductReportItemDto ReadNotSoldProduct(DbDataReader reader) =>
        new(
            ReadNullableInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "SupplierCode"),
            ReadString(reader, "SupplierName"),
            ReadString(reader, "ProductManagerCode"),
            ReadString(reader, "ProductManagerName"),
            Round(ReadDouble(reader, "CurrentStock")),
            ReadNullableDateTime(reader, "LastSaleDate"));

    private static ProfitabilityReportItemDto ReadProfitability(DbDataReader reader)
    {
        var salesAmount = Round(ReadDouble(reader, "SalesAmount"));
        var costAmount = Round(ReadDouble(reader, "CostAmount"));
        var profitAmount = Round(salesAmount - costAmount);

        return new ProfitabilityReportItemDto(
            ReadString(reader, "GroupCode"),
            ReadString(reader, "GroupName"),
            Round(ReadDouble(reader, "SalesQuantity")),
            salesAmount,
            costAmount,
            profitAmount,
            Percent(profitAmount, salesAmount));
    }

    private static CountingComparisonReportItemDto ReadCountingComparison(DbDataReader reader)
    {
        var countQuantity = Round(ReadDouble(reader, "CountQuantity"));
        var systemQuantity = Round(ReadDouble(reader, "SystemQuantity"));
        var differenceQuantity = Round(countQuantity - systemQuantity);
        var salesPrice = Round(ReadDouble(reader, "SalesPrice"));

        return new CountingComparisonReportItemDto(
            ReadInt(reader, "WarehouseNo"),
            ReadString(reader, "WarehouseName"),
            ReadDateTime(reader, "CountDate"),
            ReadInt(reader, "DocumentNo"),
            ReadString(reader, "StockCode"),
            ReadString(reader, "StockName"),
            ReadString(reader, "Barcode"),
            ReadString(reader, "UnitName"),
            countQuantity,
            systemQuantity,
            differenceQuantity,
            salesPrice,
            Round(differenceQuantity * salesPrice));
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int? ReadNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static Guid ReadGuid(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        if (reader.IsDBNull(ordinal))
        {
            return Guid.Empty;
        }

        var value = reader.GetValue(ordinal);

        return value switch
        {
            Guid guid => guid,
            _ when Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) => parsed,
            _ => Guid.Empty
        };
    }

    private static double ReadDouble(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0d
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? default
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static bool ReadBool(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return !reader.IsDBNull(ordinal) &&
            Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double Percent(double numerator, double denominator) =>
        Math.Abs(denominator) <= 0.000001d
            ? 0d
            : Round((numerator / denominator) * 100d);

    private sealed record NormalizedStockOnHandReportRequest(
        int WarehouseNo,
        DateTime ReportDate,
        string? Search,
        string? SupplierCode,
        string? CategoryCode,
        string? ProducerCode,
        string? ProductManagerCode,
        string? ModelCode,
        bool OnlyWithStock,
        int Take);

    private sealed record NormalizedProductWarehouseStockRequest(
        int? WarehouseNo,
        DateTime ReportDate,
        string StockCodeOrBarcode,
        bool OnlyWithStock,
        int Take);

    private sealed record NormalizedStockCardDetailRequest(
        int? WarehouseNo,
        string? Barcode,
        string? StockCode,
        string? StockName,
        string? SupplierCode,
        string? ProductManagerCode,
        int Take);
}
