using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.TrendyolGo;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal sealed class TrendyolGoCatalogWorkbench(
    ITrendyolGoIntegrationService trendyol,
    MikroDbContext mikroDbContext,
    TrendyolGoPriceStockSnapshotCoordinator snapshotCoordinator,
    IOptionsMonitor<TrendyolGoOptions> options) : ITrendyolGoCatalogWorkbench
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

    private async Task<CatalogSnapshot> BuildSnapshotAsync(long storeId, CancellationToken cancellationToken)
    {
        var config = options.CurrentValue;
        var store = config.Stores.SingleOrDefault(item => item.StoreId == storeId)
            ?? throw new ArgumentException($"storeId {storeId} is not configured.");
        var priceStockSnapshot = snapshotCoordinator.GetState(storeId).Snapshot
            ?? throw new InvalidOperationException(
                "Trendyol catalog snapshot is still preparing. Refresh the price-stock preview first.");
        var mikroProducts = await ReadMikroCatalogAsync(
            store.WarehouseNo, config.PriceListNo, config.PaymentPlanNo, cancellationToken);
        var existingBarcodes = priceStockSnapshot.Products
            .Select(item => item.Barcode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
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
            item => item.CategoryCode, item => item.CategoryId,
            config.Catalog.CategoryMappings);
        var duplicateBarcodes = mikroProducts
            .Where(item => !string.IsNullOrWhiteSpace(item.Barcode))
            .GroupBy(item => item.Barcode!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var rows = mikroProducts.Select(product => BuildRow(
                product, existingBarcodes, duplicateBarcodes, brandMappings, categoryMappings))
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
        IReadOnlyDictionary<string, long> categoryMappings)
    {
        var barcode = string.IsNullOrWhiteSpace(product.Barcode) ? null : product.Barcode.Trim();
        var brandId = ResolveMapping(product.BrandCode, brandMappings);
        var categoryId = ResolveMapping(product.CategoryCode, categoryMappings);
        string? reason = null;
        var status = "Ready";

        if (barcode is not null && existingBarcodes.Contains(barcode))
        {
            status = "Existing";
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
            product.BrandCode, brandId, product.CategoryCode, categoryId,
            product.VatRate, product.Price, status, reason);
    }

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

    private static Dictionary<string, long> InferMappings(
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
            command.CommandText = """
                WITH RankedBarcodes AS
                (
                    SELECT barcode.bar_stokkodu AS StockCode,
                           LTRIM(RTRIM(barcode.bar_kodu)) AS Barcode,
                           COALESCE(barcode.bar_birimpntr, 1) AS UnitPointer,
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY barcode.bar_stokkodu
                               ORDER BY CASE WHEN COALESCE(barcode.bar_master, 0) = 1 THEN 0 ELSE 1 END,
                                        CASE WHEN COALESCE(barcode.bar_birimpntr, 1) = 1 THEN 0 ELSE 1 END,
                                        barcode.bar_kodu
                           ) AS RowNo
                    FROM dbo.BARKOD_TANIMLARI AS barcode
                    WHERE COALESCE(barcode.bar_iptal, 0) = 0
                      AND NULLIF(LTRIM(RTRIM(barcode.bar_kodu)), '') IS NOT NULL
                )
                SELECT stock.sto_kod AS StockCode,
                       COALESCE(stock.sto_isim, '') AS ProductName,
                       barcode.Barcode,
                       NULLIF(LTRIM(RTRIM(stock.sto_marka_kodu)), '') AS BrandCode,
                       COALESCE(NULLIF(LTRIM(RTRIM(stock.sto_kategori_kodu)), ''),
                                NULLIF(LTRIM(RTRIM(stock.sto_anagrup_kod)), '')) AS CategoryCode,
                       CAST(tax.Rate AS decimal(9, 2)) AS VatRate,
                       price.Price,
                       CASE WHEN COALESCE(stock.sto_iptal, 0) = 1
                                  OR COALESCE(stock.sto_pasif_fl, 0) = 1
                                  OR COALESCE(stock.sto_satis_dursun, 0) <> 0
                                  OR COALESCE(detail.sdp_Pasif_fl, 0) = 1
                                  OR COALESCE(detail.sdp_satisdursun, 0) <> 0
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
        string? BrandCode, string? CategoryCode, int? VatRate, decimal? Price, bool Blocked);

    private sealed record CatalogSnapshot(
        long StoreId, int WarehouseNo, string StoreName, string PreviewHash,
        IReadOnlyCollection<TrendyolGoCatalogRow> Rows);
}
