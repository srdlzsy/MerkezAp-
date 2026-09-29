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

internal sealed class TrendyolGoPriceStockWorkbench(
    ITrendyolGoIntegrationService trendyol,
    MikroDbContext mikroDbContext,
    TrendyolGoBranchPosPriceSyncService branchPosPriceSync,
    IOptionsMonitor<TrendyolGoOptions> options) : ITrendyolGoPriceStockWorkbench
{
    public async Task<TrendyolGoPriceStockPreview> PreviewAsync(
        long storeId, int page, int size, CancellationToken cancellationToken)
    {
        if (page < 0 || size is < 1 or > 100)
        {
            throw new ArgumentException("page must be non-negative and size must be between 1 and 100.");
        }

        var config = options.CurrentValue;
        if (config.PriceListNo < 1 || config.PaymentPlanNo < 0)
        {
            throw new InvalidOperationException("Trendyol Go price list configuration is invalid.");
        }

        var store = config.Stores.SingleOrDefault(item => item.StoreId == storeId)
            ?? throw new ArgumentException($"storeId {storeId} is not configured.");

        var catalog = await trendyol.ListProductsAsync(
            new TrendyolGoProductListRequest(storeId, "ALL_PRODUCT", null, null, null, null, [], page, size),
            cancellationToken);
        if (!catalog.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Trendyol Go product response has no content array.");
        }

        var products = content.EnumerateArray().Select(ReadProduct).ToArray();
        var barcodes = products.Select(item => item.Barcode)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var mikro = await ReadMikroProductsAsync(
            store.WarehouseNo,
            barcodes,
            config.PriceListNo,
            config.PaymentPlanNo,
            cancellationToken);
        var duplicateBarcodes = products.GroupBy(product => product.Barcode, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        var rows = products.Select(product =>
        {
            var row = BuildRow(product, mikro.GetValueOrDefault(product.Barcode));
            return duplicateBarcodes.Contains(product.Barcode)
                ? row with { Status = "Skipped", Reason = "Trendyol sayfasinda barkod tekrari var." }
                : row;
        }).ToArray();
        var totalPages = ReadInt(catalog, "totalPages") ?? 0;
        var totalElements = ReadLong(catalog, "totalElements") ?? 0;
        var hashData = JsonSerializer.SerializeToUtf8Bytes(new { storeId, page, size, totalPages, totalElements, rows });
        var previewHash = Convert.ToHexString(SHA256.HashData(hashData));

        return new TrendyolGoPriceStockPreview(
            storeId, store.WarehouseNo, store.StoreName, page, size, totalPages, totalElements,
            previewHash, rows.Count(row => row.Status == "Ready"),
            rows.Count(row => row.Status == "Skipped"), rows);
    }

    public async Task<TrendyolGoPriceStockDispatch> DispatchAsync(
        long storeId, int page, int size, string previewHash,
        IReadOnlyCollection<string> barcodes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(previewHash) || barcodes.Count is < 1 or > 100 ||
            barcodes.Any(string.IsNullOrWhiteSpace) ||
            barcodes.Count != barcodes.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException("A preview hash and 1-100 distinct barcodes are required.");
        }

        var preview = await PreviewAsync(storeId, page, size, cancellationToken);
        if (!string.Equals(preview.PreviewHash, previewHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Price, stock or Trendyol product data changed. Refresh the preview before sending.");
        }

        var selected = barcodes.Select(barcode =>
        {
            var matching = preview.Items.Where(item => item.Barcode == barcode).ToArray();
            if (matching.Length != 1)
            {
                throw new ArgumentException($"Barcode {barcode} must appear exactly once on the preview page.");
            }

            return matching[0];
        }).ToArray();
        if (selected.Any(item => item.Status != "Ready"))
        {
            throw new ArgumentException("Only ready preview rows can be sent.");
        }

        var payload = JsonSerializer.SerializeToElement(new
        {
            items = selected.Select(item => new
            {
                barcode = item.Barcode,
                sellingPrice = item.MikroPrice!.Value,
                quantity = item.MikroQuantity!.Value,
                storeId
            }).ToArray()
        });
        var upstream = await trendyol.UpdatePriceAndInventoryAsync(payload, cancellationToken);
        var config = options.CurrentValue;
        var mikro = await ReadMikroProductsAsync(
            preview.WarehouseNo,
            selected.Select(item => item.Barcode).ToArray(),
            config.PriceListNo,
            config.PaymentPlanNo,
            cancellationToken);
        var branchPosItems = selected
            .Select(item => mikro.GetValueOrDefault(item.Barcode))
            .Where(item => item is not null)
            .Select(item => item!)
            .Where(item => item.StockCode is not null && item.Price is not null)
            .Select(item => new BranchPosPriceItem(
                item.StockCode!, item.Price!.Value, item.UnitPointer, item.UnitName,
                item.SalesBlocked, item.OrderBlocked, item.GoodsAcceptanceBlocked,
                item.PriceUpdatedAtUtc, item.WarehouseUpdatedAtUtc, config.PriceListNo))
            .ToArray();
        await branchPosPriceSync.EnqueueAsync(storeId, preview.WarehouseNo, branchPosItems, cancellationToken);
        return new TrendyolGoPriceStockDispatch(storeId, preview.WarehouseNo, selected.Length, upstream);
    }

    private async Task<Dictionary<string, MikroProduct>> ReadMikroProductsAsync(
        int warehouseNo,
        IReadOnlyCollection<string> barcodes,
        int priceListNo,
        int paymentPlanNo,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, MikroProduct>(StringComparer.Ordinal);
        if (barcodes.Count == 0)
        {
            return result;
        }

        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 300;
            var valueRows = barcodes.Select((_, index) => $"(@barcode{index})");
            command.CommandText = $"""
                SELECT requested.Barcode, stock.sto_kod AS StockCode, stock.sto_isim AS StockName,
                       price.Price, COALESCE(price.UnitPointer, barcode.bar_birimpntr, 1) AS UnitPointer,
                       COALESCE(unitDefinition.sto_birim_ad, '') AS UnitName,
                       COALESCE(detail.sdp_satisdursun, 0) AS SalesBlocked,
                       COALESCE(detail.sdp_sipdursun, 0) AS OrderBlocked,
                       COALESCE(detail.sdp_malkabuldursun, 0) AS GoodsAcceptanceBlocked,
                       price.PriceUpdatedAt, detail.sdp_lastup_date AS WarehouseUpdatedAt,
                       CASE WHEN stock.sto_kod IS NULL THEN NULL
                            ELSE dbo.fn_DepodakiMiktar(stock.sto_kod, @warehouseNo, CONVERT(date, GETDATE())) END AS Quantity,
                       CASE WHEN COALESCE(stock.sto_iptal, 0) = 1
                                      OR COALESCE(detail.sdp_Pasif_fl, stock.sto_pasif_fl, 0) = 1
                                      OR COALESCE(detail.sdp_satisdursun, stock.sto_satis_dursun, 0) <> 0
                            THEN 1 ELSE 0 END AS Blocked
                FROM (VALUES {string.Join(",", valueRows)}) AS requested(Barcode)
                LEFT JOIN dbo.BARKOD_TANIMLARI AS barcode
                  ON barcode.bar_kodu = requested.Barcode AND COALESCE(barcode.bar_iptal, 0) = 0
                LEFT JOIN dbo.STOKLAR AS stock ON stock.sto_kod = barcode.bar_stokkodu
                LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS detail
                  ON detail.sdp_depo_kod = stock.sto_kod AND detail.sdp_depo_no = @warehouseNo
                LEFT JOIN dbo.STOK_BIRIM_TANIMLARI_DIKEY AS unitDefinition
                  ON unitDefinition.sto_kod = stock.sto_kod
                 AND unitDefinition.sto_birimID = COALESCE(barcode.bar_birimpntr, 1)
                OUTER APPLY
                (
                    SELECT TOP (1) priceRow.sfiyat_fiyati AS Price,
                           priceRow.sfiyat_birim_pntr AS UnitPointer,
                           priceRow.sfiyat_lastup_date AS PriceUpdatedAt
                    FROM dbo.STOK_SATIS_FIYAT_LISTELERI AS priceRow
                    WHERE priceRow.sfiyat_stokkod = stock.sto_kod
                        AND priceRow.sfiyat_deposirano = @warehouseNo
                        AND priceRow.sfiyat_listesirano = @priceListNo
                        AND priceRow.sfiyat_odemeplan = @paymentPlanNo
                        AND priceRow.sfiyat_birim_pntr = COALESCE(barcode.bar_birimpntr, 1)
                        AND COALESCE(priceRow.sfiyat_iptal, 0) = 0
                        AND priceRow.sfiyat_fiyati IS NOT NULL
                    ORDER BY COALESCE(priceRow.sfiyat_lastup_date, priceRow.sfiyat_create_date) DESC
                ) AS price;
                """;
            AddParameter(command, "@warehouseNo", warehouseNo);
            AddParameter(command, "@priceListNo", priceListNo);
            AddParameter(command, "@paymentPlanNo", paymentPlanNo);
            var index = 0;
            foreach (var barcode in barcodes)
            {
                AddParameter(command, $"@barcode{index++}", barcode);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var barcode = Convert.ToString(reader["Barcode"], CultureInfo.InvariantCulture) ?? string.Empty;
                result[barcode] = new MikroProduct(
                    reader["StockCode"] is DBNull ? null : Convert.ToString(reader["StockCode"], CultureInfo.InvariantCulture),
                    reader["StockName"] is DBNull ? string.Empty : Convert.ToString(reader["StockName"], CultureInfo.InvariantCulture) ?? string.Empty,
                    reader["Price"] is DBNull ? null : Convert.ToDecimal(reader["Price"], CultureInfo.InvariantCulture),
                    reader["Quantity"] is DBNull ? null : Convert.ToDecimal(reader["Quantity"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["Blocked"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["UnitPointer"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["UnitName"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["SalesBlocked"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["OrderBlocked"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["GoodsAcceptanceBlocked"], CultureInfo.InvariantCulture),
                    reader["PriceUpdatedAt"] is DBNull ? null : Convert.ToDateTime(reader["PriceUpdatedAt"], CultureInfo.InvariantCulture),
                    reader["WarehouseUpdatedAt"] is DBNull ? null : Convert.ToDateTime(reader["WarehouseUpdatedAt"], CultureInfo.InvariantCulture));
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

    internal static TrendyolGoPriceStockRow BuildRow(TrendyolProduct product, MikroProduct? mikro)
    {
        var price = mikro?.Price;
        var quantity = mikro?.Quantity is decimal rawQuantity
            ? (int?)Math.Clamp(decimal.Floor(rawQuantity), 0, int.MaxValue)
            : null;
        var reason = string.IsNullOrWhiteSpace(product.Barcode) ? "Trendyol barkodu bos."
            : mikro?.StockCode is null ? "Mikro barkod eslesmesi yok."
            : mikro.Blocked ? "Urun Mikro'da pasif veya satisa kapali."
            : price is null or <= 0 ? "Mikro satis fiyati yok veya sifir."
            : quantity is null ? "Mikro stok miktari okunamadi."
            : null;
        var changed = reason is null && (product.Price != price || (product.Quantity ?? 0) != quantity);
        return new TrendyolGoPriceStockRow(
            product.Barcode, mikro?.StockCode, mikro?.StockName ?? product.Title,
            product.Price, product.Quantity, price, quantity,
            reason is not null ? "Skipped" : changed ? "Ready" : "Unchanged", reason);
    }

    private static TrendyolProduct ReadProduct(JsonElement product)
    {
        if (product.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Trendyol Go product row is invalid.");
        }

        return new TrendyolProduct(
            ReadString(product, "barcode") ?? string.Empty,
            ReadString(product, "title") ?? string.Empty,
            ReadDecimal(product, "sellingPrice"),
            ReadInt(product, "quantity"));
    }

    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private static decimal? ReadDecimal(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDecimal() : null;

    private static int? ReadInt(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32() : null;

    private static long? ReadLong(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt64() : null;

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    internal sealed record TrendyolProduct(string Barcode, string Title, decimal? Price, int? Quantity);
    internal sealed record MikroProduct(
        string? StockCode, string StockName, decimal? Price, decimal? Quantity, bool Blocked,
        int UnitPointer = 1, string UnitName = "", int SalesBlocked = 0, int OrderBlocked = 0,
        int GoodsAcceptanceBlocked = 0, DateTime? PriceUpdatedAtUtc = null,
        DateTime? WarehouseUpdatedAtUtc = null);
}
