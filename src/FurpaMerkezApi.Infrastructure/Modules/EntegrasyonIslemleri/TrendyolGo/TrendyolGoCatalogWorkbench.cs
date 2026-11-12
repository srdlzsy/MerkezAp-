using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoCatalogWorkbench(
    ITrendyolGoIntegrationService trendyol,
    MikroDbContext mikroDbContext,
    AuthDbContext authDbContext,
    TrendyolGoPriceStockSnapshotCoordinator snapshotCoordinator,
    IOptionsMonitor<TrendyolGoOptions> options,
    TrendyolGoRuntimeSettingsProvider runtimeSettingsProvider) : ITrendyolGoCatalogWorkbench
{
    private static readonly HashSet<int> AllowedVatRates = [0, 1, 10, 20];
    private const string UnsupportedCharacters = "?/&%+^'*_";

    public async Task<TrendyolGoCatalogPreview> PreviewAsync(
        long storeId, string view, string? search, int take, CancellationToken cancellationToken)
    {
        if (take is < 1 or > 5000)
        {
            throw new ArgumentException("take must be between 1 and 5000.");
        }

        var normalizedView = NormalizeView(view);
        var snapshot = await BuildSnapshotAsync(storeId, cancellationToken);
        var visible = FilterRows(snapshot.Rows, normalizedView, search).Take(take).ToArray();
        return new TrendyolGoCatalogPreview(
            snapshot.StoreId, snapshot.WarehouseNo, snapshot.StoreName, snapshot.PreviewHash,
            snapshot.Rows.Count, snapshot.Rows.Count(item => item.Status == "Existing"),
            snapshot.Rows.Count(item => item.Status == "Ready"),
            snapshot.Rows.Count(item => item.Status == "MappingRequired"),
            snapshot.Rows.Count(item => item.Status == "Skipped"),
            normalizedView, visible.Length, visible);
    }

    public async Task<TrendyolGoCatalogDispatch> DispatchAsync(
        long storeId, string previewHash, bool sendAll,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previewHash) || barcodes.Count > 10000 ||
            barcodes.Any(string.IsNullOrWhiteSpace) ||
            barcodes.Count != barcodes.Distinct(StringComparer.Ordinal).Count() ||
            (!sendAll && barcodes.Count == 0))
        {
            throw new ArgumentException(
                "A preview hash and either sendAll or distinct barcodes are required.");
        }

        var snapshot = await BuildSnapshotAsync(storeId, cancellationToken);
        if (!string.Equals(snapshot.PreviewHash, previewHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Mikro product data, catalog mappings or Trendyol catalog changed. Refresh the catalog preview before sending.");
        }

        var selected = SelectRows(snapshot.Rows, sendAll, barcodes);
        if (selected.Length == 0)
        {
            throw new ArgumentException("There are no catalog rows ready to create.");
        }

        var responses = new List<JsonElement?>();
        foreach (var batch in selected.Chunk(1000))
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                items = batch.Select(item => new
                {
                    barcode = item.Barcode,
                    vatRate = item.VatRate!.Value,
                    title = NormalizeTitle(item.ProductName),
                    stockCode = item.StockCode,
                    brandId = item.TrendyolBrandId!.Value,
                    categoryId = item.TrendyolCategoryId!.Value
                }).ToArray()
            });
            responses.Add(await trendyol.CreateProductsAsync(payload, cancellationToken));
        }

        return new TrendyolGoCatalogDispatch(
            storeId, snapshot.WarehouseNo, selected.Length, responses.Count,
            "Batch sonuclarini kontrol edin. Urunler kabul edilip TGO katalogunda gorundukten sonra price-stock all-existing gonderimi yapin.",
            responses);
    }

    public async Task<TrendyolGoCatalogAutoCreateResult> AutoCreateProductsAsync(
        long storeId,
        IReadOnlyCollection<string> barcodes,
        CancellationToken cancellationToken)
    {
        if (barcodes.Count == 0)
        {
            return new TrendyolGoCatalogAutoCreateResult(0, [], null);
        }

        var distinctBarcodes = barcodes
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(b => b.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinctBarcodes.Length == 0)
        {
            return new TrendyolGoCatalogAutoCreateResult(0, [], null);
        }

        var config = options.CurrentValue;
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        var store = runtime.RequireStore(storeId);
        var priceStockSnapshot = RequireCatalogSnapshot(storeId);

        var mikroProducts = await ReadMikroCatalogByBarcodesAsync(
            store.WarehouseNo, runtime.PriceListNo, runtime.PaymentPlanNo, distinctBarcodes, cancellationToken);

        var missingInMikro = distinctBarcodes
            .Except(mikroProducts.Select(p => p.Barcode).Where(b => !string.IsNullOrWhiteSpace(b))!, StringComparer.Ordinal)
            .ToArray();

        if (mikroProducts.Length == 0)
        {
            var notFoundMsg = missingInMikro.Length > 0
                ? $"Mikro'da bu barkodlara ait stok karti bulunamadi: {string.Join(", ", missingInMikro)}."
                : "Mikro'da bu barkodlara ait uygun stok karti bulunamadi.";
            return new TrendyolGoCatalogAutoCreateResult(
                0,
                [],
                notFoundMsg);
        }

        var existingBarcodes = priceStockSnapshot.Products
            .Select(item => item.Barcode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
        var existingStockCodes = BuildExistingStockCodeMap(priceStockSnapshot.Products);

        var neededBrandCodes = mikroProducts
            .Select(item => item.BrandCode?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var neededCategoryCodes = mikroProducts
            .SelectMany(item => new[] { item.CategoryCode?.Trim(), item.MainGroupCode?.Trim() })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var mappingProducts = await ReadMikroMappingProductsAsync(
            neededBrandCodes!, neededCategoryCodes!, cancellationToken);

        var mappingMikroByBarcode = mappingProducts
            .Concat(mikroProducts)
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .GroupBy(item => item.Barcode!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var brandMappings = InferMappings(
            priceStockSnapshot.Products, mappingMikroByBarcode,
            item => item.BrandCode, item => item.BrandId,
            config.Catalog.BrandMappings);
        var categoryMappings = InferMappings(
            priceStockSnapshot.Products, mappingMikroByBarcode,
            item => item.EffectiveCategoryCode, item => item.CategoryId,
            config.Catalog.CategoryMappings);
        var approvedRules = await LoadApprovedCategoryRulesAsync(cancellationToken);
        var duplicateBarcodes = mikroProducts
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .GroupBy(item => item.Barcode!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var rows = new List<TrendyolGoCatalogRow>();
        var validRowsToCreate = new List<TrendyolGoCatalogRow>();

        foreach (var product in mikroProducts)
        {
            var approvedCategory = ResolveApprovedCategory(product, approvedRules);
            var row = BuildRow(
                product,
                existingBarcodes,
                duplicateBarcodes,
                brandMappings,
                categoryMappings,
                config.Catalog.DefaultBrandId,
                config.Catalog.DefaultCategoryId,
                existingStockCodes,
                approvedCategory.CategoryId,
                approvedCategory.Source);
            rows.Add(row);
            if (row.Status == "Ready")
            {
                validRowsToCreate.Add(row);
            }
        }

        if (validRowsToCreate.Count == 0)
        {
            var issues = rows
                .Where(r => r.Status != "Ready")
                .Select(r => $"{r.Barcode ?? r.StockCode} ({r.ProductName}): {r.Reason ?? r.Status}")
                .ToList();
            if (missingInMikro.Length > 0)
            {
                issues.Add($"Mikro'da bulunamayan barkodlar: {string.Join(", ", missingInMikro)}");
            }
            var errorMessage = issues.Count > 0
                ? $"Katalog olusturma kriterlerini karsilayan urun bulunamadi: {string.Join("; ", issues)}"
                : "Yeni ve katalog kurallarina uygun urun bulunamadi. Mevcut urunler yeniden olusturulmadi.";
            return new TrendyolGoCatalogAutoCreateResult(
                0,
                [],
                errorMessage);
        }

        var batchRequestIds = new List<string>();
        var batchSubmissions = new List<TrendyolGoCatalogBatchSubmission>();
        foreach (var batch in validRowsToCreate.Chunk(1000))
        {
            var payload = JsonSerializer.SerializeToElement(new
            {
                items = batch.Select(item => new
                {
                    barcode = item.Barcode,
                    vatRate = item.VatRate!.Value,
                    title = NormalizeTitle(item.ProductName),
                    stockCode = item.StockCode,
                    brandId = item.TrendyolBrandId!.Value,
                    categoryId = item.TrendyolCategoryId!.Value
                }).ToArray()
            });
            var response = await trendyol.CreateProductsAsync(payload, cancellationToken);
            var batchId = TrendyolGoBatchFollowUpProcessor.RequireBatchRequestId(
                response,
                "catalog create");
            batchRequestIds.Add(batchId);
            batchSubmissions.Add(new TrendyolGoCatalogBatchSubmission(
                batchId,
                batch.Select(item => item.Barcode!).ToArray()));
        }

        return new TrendyolGoCatalogAutoCreateResult(
            validRowsToCreate.Count,
            batchRequestIds,
            null,
            batchSubmissions);
    }

    private async Task<CatalogSnapshot> BuildSnapshotAsync(long storeId, CancellationToken cancellationToken)
    {
        var config = options.CurrentValue;
        var runtime = await runtimeSettingsProvider.GetAsync(cancellationToken);
        var store = runtime.RequireStore(storeId);
        var priceStockSnapshot = RequireCatalogSnapshot(storeId);
        var mikroProducts = await ReadMikroCatalogAsync(
            store.WarehouseNo, runtime.PriceListNo, runtime.PaymentPlanNo, cancellationToken);
        var existingBarcodes = priceStockSnapshot.Products
            .Select(item => item.Barcode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
        var existingStockCodes = BuildExistingStockCodeMap(priceStockSnapshot.Products);
        var mikroByBarcode = mikroProducts
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .GroupBy(item => item.Barcode!, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        var brandMappings = InferMappings(
            priceStockSnapshot.Products, mikroByBarcode,
            item => item.BrandCode, item => item.BrandId,
            config.Catalog.BrandMappings);
        var categoryMappings = InferMappings(
            priceStockSnapshot.Products, mikroByBarcode,
            item => item.EffectiveCategoryCode, item => item.CategoryId,
            config.Catalog.CategoryMappings);
        var approvedRules = await LoadApprovedCategoryRulesAsync(cancellationToken);
        var duplicateBarcodes = mikroProducts
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .GroupBy(item => item.Barcode!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var rows = mikroProducts.Select(product =>
            {
                var approvedCategory = ResolveApprovedCategory(product, approvedRules);
                return BuildRow(
                    product,
                    existingBarcodes,
                    duplicateBarcodes,
                    brandMappings,
                    categoryMappings,
                    config.Catalog.DefaultBrandId,
                    config.Catalog.DefaultCategoryId,
                    existingStockCodes,
                    approvedCategory.CategoryId,
                    approvedCategory.Source);
            })
            .OrderBy(item => item.StockCode, StringComparer.Ordinal)
            .ToArray();
        var hashData = JsonSerializer.SerializeToUtf8Bytes(new { storeId, rows });
        return new CatalogSnapshot(
            storeId, store.WarehouseNo, store.StoreName,
            Convert.ToHexString(SHA256.HashData(hashData)), rows);
    }

    internal static TrendyolGoCatalogRow BuildRow(
        MikroCatalogProduct product,
        IReadOnlySet<string> existingBarcodes,
        IReadOnlySet<string> duplicateBarcodes,
        IReadOnlyDictionary<string, long> brandMappings,
        IReadOnlyDictionary<string, long> categoryMappings,
        long defaultBrandId = 0,
        long defaultCategoryId = 0,
        IReadOnlyDictionary<string, string>? existingStockCodes = null,
        long? approvedCategoryId = null,
        string? categoryMappingSource = null)
    {
        var barcode = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim();
        _ = defaultBrandId;
        _ = defaultCategoryId;
        var brandId = ResolveMapping(product.BrandCode, brandMappings);
        var categoryId = approvedCategoryId
            ?? ResolveMapping(product.EffectiveCategoryCode, categoryMappings);
        categoryMappingSource ??= approvedCategoryId.HasValue
            ? "ApprovedRule"
            : ResolveMapping(product.EffectiveCategoryCode, categoryMappings).HasValue
                ? "ExistingCatalog"
                : null;
        string? reason = null;
        var status = "Ready";

        if (barcode is not null && existingBarcodes.Contains(barcode))
        {
            status = "Existing";
        }
        else if (existingStockCodes is not null &&
                 existingStockCodes.TryGetValue(product.StockCode, out var existingBarcode))
        {
            status = "ExistingStockCode";
            reason = $"Ayni stok kodu Trendyol Go katalogunda {existingBarcode} barkoduyla mevcut. " +
                     "Mikro ve Trendyol barkodlarini esitleyin; yeni urun olusturulmadi.";
        }
        else if (product.Blocked)
        {
            status = "Skipped";
            reason = "Urun Mikro'da pasif veya satisa kapali.";
        }
        else if (barcode is null)
        {
            status = "Skipped";
            reason = "Aktif urun barkodu yok.";
        }
        else if (barcode.Any(char.IsWhiteSpace) || barcode.Any(UnsupportedCharacters.Contains))
        {
            status = "Skipped";
            reason = "Barkod Trendyol'un kabul etmedigi karakterler iceriyor.";
        }
        else if (duplicateBarcodes.Contains(barcode))
        {
            status = "Skipped";
            reason = "Ayni Mikro barkodu birden fazla stok kartinda kullaniliyor.";
        }
        else if (product.VatRate is null || !AllowedVatRates.Contains(product.VatRate.Value))
        {
            status = "Skipped";
            reason = "Mikro perakende KDV orani Trendyol icin 0, 1, 10 veya 20 olmali.";
        }
        else if (product.Price is null or <= 0)
        {
            status = "Skipped";
            reason = "Secili depo icin Trendyol satis fiyati yok veya sifir.";
        }
        else if (string.IsNullOrWhiteSpace(NormalizeTitle(product.ProductName)))
        {
            status = "Skipped";
            reason = "Urun adi Trendyol kurallarina gore bos kaliyor.";
        }
        else if (brandId is null || categoryId is null)
        {
            status = "MappingRequired";
            reason = brandId is null && categoryId is null
                ? "Trendyol marka ve kategori eslemesi gerekli."
                : brandId is null
                    ? "Trendyol marka eslemesi gerekli."
                    : "Trendyol kategori eslemesi gerekli.";
        }

        return new TrendyolGoCatalogRow(
            product.StockCode, product.ProductName, barcode,
            product.BrandCode, brandId, product.CategoryCode, product.MainGroupCode, categoryId,
            categoryMappingSource, product.VatRate, product.Price, status, reason);
    }

    internal static IReadOnlyDictionary<string, string> BuildExistingStockCodeMap(
        IReadOnlyCollection<TrendyolGoPriceStockWorkbench.TrendyolProduct> products) =>
        products
            .Where(item => !string.IsNullOrWhiteSpace(item.StockCode) &&
                           !string.IsNullOrWhiteSpace(item.Barcode))
            .GroupBy(item => item.StockCode!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Barcode).Distinct(StringComparer.Ordinal).Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.First().Barcode,
                StringComparer.OrdinalIgnoreCase);

    internal static TrendyolGoCatalogRow[] SelectRows(
        IReadOnlyCollection<TrendyolGoCatalogRow> rows,
        bool sendAll,
        IReadOnlyCollection<string> barcodes)
    {
        if (sendAll)
        {
            return rows.Where(item => item.Status == "Ready").ToArray();
        }

        var selected = barcodes.Select(barcode =>
            rows.SingleOrDefault(item => string.Equals(item.Barcode, barcode, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Barcode {barcode} was not found in the catalog preview."))
            .ToArray();
        if (selected.Any(item => item.Status != "Ready"))
        {
            throw new ArgumentException("Only catalog rows with Ready status can be created.");
        }

        return selected;
    }

    private TrendyolGoPriceStockSnapshot RequireCatalogSnapshot(long storeId)
    {
        var snapshot = snapshotCoordinator.GetState(storeId).Snapshot;
        if (snapshot is not null)
        {
            return snapshot;
        }

        snapshotCoordinator.RequestRefresh(storeId);
        throw new InvalidOperationException(
            "Trendyol katalogu henuz hazir degil. Fiyat-stok onizlemesini yenileyip snapshot tamamlandiktan sonra tekrar deneyin.");
    }

    internal static Dictionary<string, long> InferMappings(
        IReadOnlyCollection<TrendyolGoPriceStockWorkbench.TrendyolProduct> trendyolProducts,
        IReadOnlyDictionary<string, MikroCatalogProduct> mikroByBarcode,
        Func<MikroCatalogProduct, string?> mikroCode,
        Func<TrendyolGoPriceStockWorkbench.TrendyolProduct, long?> trendyolId,
        IReadOnlyDictionary<string, long> configuredMappings)
    {
        var inferred = trendyolProducts
            .Where(item => mikroByBarcode.ContainsKey(item.Barcode))
            .Select(item => new { Code = mikroCode(mikroByBarcode[item.Barcode]), Id = trendyolId(item) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Code) && item.Id is > 0)
            .GroupBy(item => item.Code!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new { group.Key, Ids = group.Select(item => item.Id!.Value).Distinct().ToArray() })
            .Where(item => item.Ids.Length == 1)
            .ToDictionary(item => item.Key, item => item.Ids[0], StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in configuredMappings.Where(item => item.Value > 0))
        {
            inferred[mapping.Key.Trim()] = mapping.Value;
        }

        return inferred;
    }

    private async Task<ApprovedCategoryRules> LoadApprovedCategoryRulesAsync(
        CancellationToken cancellationToken)
    {
        var mappings = await authDbContext.TrendyolGoCategoryMappings
            .AsNoTracking()
            .Where(item => item.IsActive)
            .ToArrayAsync(cancellationToken);
        var overrides = await authDbContext.TrendyolGoProductCategoryOverrides
            .AsNoTracking()
            .Where(item => item.IsActive)
            .ToArrayAsync(cancellationToken);
        return new ApprovedCategoryRules(
            mappings.ToDictionary(item => item.MappingKey, item => item.CategoryId, StringComparer.OrdinalIgnoreCase),
            overrides.ToDictionary(item => item.StockCode, item => item.CategoryId, StringComparer.OrdinalIgnoreCase));
    }

    internal static ApprovedCategoryResolution ResolveApprovedCategory(
        MikroCatalogProduct product,
        ApprovedCategoryRules rules)
    {
        if (rules.ProductOverrides.TryGetValue(product.StockCode, out var productCategoryId))
        {
            return new ApprovedCategoryResolution(productCategoryId, "ProductOverride");
        }

        if (!string.IsNullOrWhiteSpace(product.CategoryCode))
        {
            var exactKey = TrendyolGoCategoryMapping.BuildKey(product.CategoryCode, product.MainGroupCode);
            if (rules.CategoryMappings.TryGetValue(exactKey, out var exactCategoryId))
            {
                return new ApprovedCategoryResolution(exactCategoryId, "CategoryAndMainGroupRule");
            }

            var categoryKey = TrendyolGoCategoryMapping.BuildKey(product.CategoryCode, null);
            if (rules.CategoryMappings.TryGetValue(categoryKey, out var categoryId))
            {
                return new ApprovedCategoryResolution(categoryId, "CategoryRule");
            }
        }

        return new ApprovedCategoryResolution(null, null);
    }

    private async Task<MikroCatalogProduct[]> ReadMikroCatalogAsync(
        int warehouseNo, int priceListNo, int paymentPlanNo,
        CancellationToken cancellationToken)
    {
        var result = new List<MikroCatalogProduct>();
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
            var barcodeExpression = TrendyolGoBarcodeRule.SqlBarcodeExpression("barcode.Barcode");
            var rankedBarcodesCte = TrendyolGoBarcodeRule.SqlRankedBarcodesCte();
            command.CommandText = $"""
                WITH {rankedBarcodesCte}
                SELECT stock.sto_kod AS StockCode,
                       COALESCE(stock.sto_isim, '') AS ProductName,
                       {barcodeExpression} AS Barcode,
                       NULLIF(LTRIM(RTRIM(stock.sto_marka_kodu)), '') AS BrandCode,
                       NULLIF(LTRIM(RTRIM(stock.sto_kategori_kodu)), '') AS CategoryCode,
                       NULLIF(LTRIM(RTRIM(stock.sto_anagrup_kod)), '') AS MainGroupCode,
                       CAST(tax.Rate AS decimal(9, 2)) AS VatRate,
                       price.Price,
                       CASE WHEN COALESCE(stock.sto_iptal, 0) = 1
                                  OR COALESCE(stock.sto_pasif_fl, 0) = 1
                                  OR COALESCE(stock.sto_satis_dursun, 0) <> 0
                                  OR COALESCE(detail.sdp_Pasif_fl, 0) = 1
                                  OR COALESCE(detail.sdp_satisdursun, 0) <> 0
                                  OR COALESCE(stock.sto_webe_gonderilecek_fl, 0) = 0
                            THEN 1 ELSE 0 END AS Blocked
                FROM dbo.STOKLAR AS stock
                LEFT JOIN RankedBarcodes AS barcode
                  ON barcode.StockCode = stock.sto_kod AND barcode.RowNo = 1
                LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail
                  ON detail.sdp_depo_kod = stock.sto_kod AND detail.sdp_depo_no = @warehouseNo
                LEFT JOIN dbo.fn_hs_vergi_oran_listesi() AS tax
                  ON tax.DepartmentNo = COALESCE(stock.sto_perakende_vergi, 0)
                OUTER APPLY
                (
                    SELECT TOP (1) priceRow.sfiyat_fiyati AS Price
                    FROM dbo.STOK_SATIS_FIYAT_LISTELERI AS priceRow
                    WHERE priceRow.sfiyat_stokkod = stock.sto_kod
                      AND priceRow.sfiyat_deposirano = @warehouseNo
                      AND priceRow.sfiyat_listesirano = @priceListNo
                      AND priceRow.sfiyat_odemeplan = @paymentPlanNo
                      AND priceRow.sfiyat_birim_pntr = COALESCE(barcode.UnitPointer, 1)
                      AND COALESCE(priceRow.sfiyat_iptal, 0) = 0
                    ORDER BY COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) DESC
                ) AS price
                WHERE NULLIF(LTRIM(RTRIM(stock.sto_kod)), '') IS NOT NULL
                  AND NULLIF(LTRIM(RTRIM(stock.sto_isim)), '') IS NOT NULL
                  AND COALESCE(stock.sto_webe_gonderilecek_fl, 0) = 1
                ORDER BY stock.sto_kod;
                """;
            AddParameter(command, "@warehouseNo", warehouseNo);
            AddParameter(command, "@priceListNo", priceListNo);
            AddParameter(command, "@paymentPlanNo", paymentPlanNo);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vatRate = reader["VatRate"] is DBNull
                    ? null
                    : (int?)decimal.ToInt32(Convert.ToDecimal(reader["VatRate"], CultureInfo.InvariantCulture));
                result.Add(new MikroCatalogProduct(
                    Convert.ToString(reader["StockCode"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["ProductName"], CultureInfo.InvariantCulture) ?? string.Empty,
                    reader["Barcode"] is DBNull ? null : Convert.ToString(reader["Barcode"], CultureInfo.InvariantCulture),
                    reader["BrandCode"] is DBNull ? null : Convert.ToString(reader["BrandCode"], CultureInfo.InvariantCulture),
                    reader["CategoryCode"] is DBNull ? null : Convert.ToString(reader["CategoryCode"], CultureInfo.InvariantCulture),
                    reader["MainGroupCode"] is DBNull ? null : Convert.ToString(reader["MainGroupCode"], CultureInfo.InvariantCulture),
                    vatRate,
                    reader["Price"] is DBNull ? null : Convert.ToDecimal(reader["Price"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["Blocked"], CultureInfo.InvariantCulture) != 0));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return result.ToArray();
    }

    private async Task<IReadOnlyCollection<MikroCatalogProduct>> ReadMikroMappingProductsAsync(
        IReadOnlyCollection<string> brandCodes,
        IReadOnlyCollection<string> categoryCodes,
        CancellationToken cancellationToken)
    {
        if (brandCodes.Count == 0 && categoryCodes.Count == 0)
        {
            return [];
        }

        var result = new List<MikroCatalogProduct>();
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
            command.CommandTimeout = 30;
            var barcodeExpression = TrendyolGoBarcodeRule.SqlBarcodeExpression("barcode.bar_kodu");
            command.CommandText = $"""
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                SELECT {barcodeExpression} AS Barcode,
                       stock.sto_kod AS StockCode,
                       COALESCE(stock.sto_isim, '') AS ProductName,
                       NULLIF(LTRIM(RTRIM(stock.sto_marka_kodu)), '') AS BrandCode,
                       NULLIF(LTRIM(RTRIM(stock.sto_kategori_kodu)), '') AS CategoryCode,
                       NULLIF(LTRIM(RTRIM(stock.sto_anagrup_kod)), '') AS MainGroupCode
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                INNER JOIN dbo.BARKOD_TANIMLARI AS barcode WITH (NOLOCK)
                  ON barcode.bar_stokkodu = stock.sto_kod
                WHERE COALESCE(barcode.bar_iptal, 0) = 0
                  AND NULLIF(LTRIM(RTRIM(barcode.bar_kodu)), '') IS NOT NULL
                  AND (
                      stock.sto_marka_kodu IN (SELECT CONVERT(nvarchar(100), [value]) FROM OPENJSON(@brandCodesJson))
                      OR stock.sto_kategori_kodu IN (SELECT CONVERT(nvarchar(100), [value]) FROM OPENJSON(@categoryCodesJson))
                      OR stock.sto_anagrup_kod IN (SELECT CONVERT(nvarchar(100), [value]) FROM OPENJSON(@categoryCodesJson))
                  );
                """;

            AddParameter(command, "@brandCodesJson", JsonSerializer.Serialize(brandCodes));
            AddParameter(command, "@categoryCodesJson", JsonSerializer.Serialize(categoryCodes));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                result.Add(new MikroCatalogProduct(
                    Convert.ToString(reader["StockCode"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["ProductName"], CultureInfo.InvariantCulture) ?? string.Empty,
                    reader["Barcode"] is DBNull ? null : Convert.ToString(reader["Barcode"], CultureInfo.InvariantCulture),
                    reader["BrandCode"] is DBNull ? null : Convert.ToString(reader["BrandCode"], CultureInfo.InvariantCulture),
                    reader["CategoryCode"] is DBNull ? null : Convert.ToString(reader["CategoryCode"], CultureInfo.InvariantCulture),
                    reader["MainGroupCode"] is DBNull ? null : Convert.ToString(reader["MainGroupCode"], CultureInfo.InvariantCulture),
                    null,
                    null,
                    false));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }

    private async Task<MikroCatalogProduct[]> ReadMikroCatalogByBarcodesAsync(
        int warehouseNo, int priceListNo, int paymentPlanNo,
        IReadOnlyCollection<string> barcodes,
        CancellationToken cancellationToken)
    {
        var result = new List<MikroCatalogProduct>();
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
            command.CommandTimeout = 120;
            var barcodeExpression = TrendyolGoBarcodeRule.SqlBarcodeExpression("barcode.Barcode");
            var lookupBarcodes = TrendyolGoBarcodeRule.ExpandLookupBarcodes(barcodes);
            var rankedBarcodesCte = TrendyolGoBarcodeRule.SqlRankedBarcodesCte(
                "LTRIM(RTRIM(barcode.bar_kodu)) IN (SELECT CONVERT(nvarchar(100), [value]) FROM OPENJSON(@lookupBarcodesJson))");
            command.CommandText = $"""
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                WITH {rankedBarcodesCte}
                SELECT stock.sto_kod AS StockCode,
                       COALESCE(stock.sto_isim, '') AS ProductName,
                       {barcodeExpression} AS Barcode,
                       NULLIF(LTRIM(RTRIM(stock.sto_marka_kodu)), '') AS BrandCode,
                       NULLIF(LTRIM(RTRIM(stock.sto_kategori_kodu)), '') AS CategoryCode,
                       NULLIF(LTRIM(RTRIM(stock.sto_anagrup_kod)), '') AS MainGroupCode,
                       CAST(tax.Rate AS decimal(9, 2)) AS VatRate,
                       price.Price,
                       CASE WHEN COALESCE(stock.sto_iptal, 0) = 1
                                  OR COALESCE(stock.sto_pasif_fl, 0) = 1
                                  OR COALESCE(stock.sto_satis_dursun, 0) <> 0
                                  OR COALESCE(detail.sdp_Pasif_fl, 0) = 1
                                  OR COALESCE(detail.sdp_satisdursun, 0) <> 0
                                  OR COALESCE(stock.sto_webe_gonderilecek_fl, 0) = 0
                            THEN 1 ELSE 0 END AS Blocked
                FROM dbo.STOKLAR AS stock WITH (NOLOCK)
                INNER JOIN RankedBarcodes AS barcode
                  ON barcode.StockCode = stock.sto_kod AND barcode.RowNo = 1
                LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail WITH (NOLOCK)
                  ON detail.sdp_depo_kod = stock.sto_kod AND detail.sdp_depo_no = @warehouseNo
                LEFT JOIN dbo.fn_hs_vergi_oran_listesi() AS tax
                  ON tax.DepartmentNo = COALESCE(stock.sto_perakende_vergi, 0)
                OUTER APPLY
                (
                    SELECT TOP (1) priceRow.sfiyat_fiyati AS Price
                    FROM dbo.STOK_SATIS_FIYAT_LISTELERI AS priceRow WITH (NOLOCK)
                    WHERE priceRow.sfiyat_stokkod = stock.sto_kod
                      AND priceRow.sfiyat_deposirano = @warehouseNo
                      AND priceRow.sfiyat_listesirano = @priceListNo
                      AND priceRow.sfiyat_odemeplan = @paymentPlanNo
                      AND priceRow.sfiyat_birim_pntr = COALESCE(barcode.UnitPointer, 1)
                      AND COALESCE(priceRow.sfiyat_iptal, 0) = 0
                    ORDER BY COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) DESC
                ) AS price
                WHERE {barcodeExpression} IN (SELECT CONVERT(nvarchar(100), [value]) FROM OPENJSON(@barcodesJson));
                """;

            AddParameter(command, "@warehouseNo", warehouseNo);
            AddParameter(command, "@priceListNo", priceListNo);
            AddParameter(command, "@paymentPlanNo", paymentPlanNo);
            AddParameter(command, "@barcodesJson", JsonSerializer.Serialize(barcodes));
            AddParameter(command, "@lookupBarcodesJson", JsonSerializer.Serialize(lookupBarcodes));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vatRate = reader["VatRate"] is DBNull
                    ? null
                    : (int?)decimal.ToInt32(Convert.ToDecimal(reader["VatRate"], CultureInfo.InvariantCulture));
                result.Add(new MikroCatalogProduct(
                    Convert.ToString(reader["StockCode"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["ProductName"], CultureInfo.InvariantCulture) ?? string.Empty,
                    reader["Barcode"] is DBNull ? null : Convert.ToString(reader["Barcode"], CultureInfo.InvariantCulture),
                    reader["BrandCode"] is DBNull ? null : Convert.ToString(reader["BrandCode"], CultureInfo.InvariantCulture),
                    reader["CategoryCode"] is DBNull ? null : Convert.ToString(reader["CategoryCode"], CultureInfo.InvariantCulture),
                    reader["MainGroupCode"] is DBNull ? null : Convert.ToString(reader["MainGroupCode"], CultureInfo.InvariantCulture),
                    vatRate,
                    reader["Price"] is DBNull ? null : Convert.ToDecimal(reader["Price"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["Blocked"], CultureInfo.InvariantCulture) != 0));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        return result.ToArray();
    }

    private static IEnumerable<TrendyolGoCatalogRow> FilterRows(
        IReadOnlyCollection<TrendyolGoCatalogRow> rows, string view, string? search)
    {
        IEnumerable<TrendyolGoCatalogRow> filtered = view switch
        {
            "all" => rows,
            "issues" => rows.Where(item => item.Status is "MappingRequired" or "Skipped"),
            "existing" => rows.Where(item => item.Status == "Existing"),
            _ => rows.Where(item => item.Status == "Ready")
        };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(item =>
                item.StockCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                item.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (item.Barcode?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return filtered;
    }

    private static string NormalizeView(string view) => view.Trim().ToLowerInvariant() switch
    {
        "actionable" or "all" or "issues" or "existing" => view.Trim().ToLowerInvariant(),
        _ => throw new ArgumentException("view must be actionable, all, issues or existing.")
    };

    private static long? ResolveMapping(string? code, IReadOnlyDictionary<string, long> mappings) =>
        !string.IsNullOrWhiteSpace(code) && mappings.TryGetValue(code.Trim(), out var id) ? id : null;

    internal static string NormalizeTitle(string title)
    {
        var sanitized = new string(title.Select(character =>
            UnsupportedCharacters.Contains(character) ? ' ' : character).ToArray());
        sanitized = string.Join(' ', sanitized.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return sanitized.Length <= 100 ? sanitized : sanitized[..100].TrimEnd();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    internal sealed record MikroCatalogProduct(
        string StockCode, string ProductName, string? Barcode,
        string? BrandCode, string? CategoryCode, string? MainGroupCode,
        int? VatRate, decimal? Price, bool Blocked)
    {
        public MikroCatalogProduct(
            string stockCode,
            string productName,
            string? barcode,
            string? brandCode,
            string? categoryCode,
            int? vatRate,
            decimal? price,
            bool blocked)
            : this(stockCode, productName, barcode, brandCode, categoryCode, null, vatRate, price, blocked)
        {
        }

        public string? EffectiveCategoryCode =>
            !string.IsNullOrWhiteSpace(CategoryCode) ? CategoryCode : MainGroupCode;
    }

    internal sealed record ApprovedCategoryRules(
        IReadOnlyDictionary<string, long> CategoryMappings,
        IReadOnlyDictionary<string, long> ProductOverrides);

    internal sealed record ApprovedCategoryResolution(long? CategoryId, string? Source);

    private sealed record CatalogSnapshot(
        long StoreId, int WarehouseNo, string StoreName, string PreviewHash,
        IReadOnlyCollection<TrendyolGoCatalogRow> Rows);
}
