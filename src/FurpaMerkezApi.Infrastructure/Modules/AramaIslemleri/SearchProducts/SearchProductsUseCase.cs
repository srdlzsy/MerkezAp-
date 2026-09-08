using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.Common;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchProducts;

public sealed class SearchProductsUseCase(MikroDbContext mikroDbContext) : ISearchProductsUseCase
{
    private const int DefaultTake = 150;
    private const int MaxTake = 150;
    private const int MaxPartialBarcodeCandidates = 50;

    public async Task<IReadOnlyCollection<ProductLookupItemDto>> ExecuteAsync(
        ProductSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var requestedBarcode = NormalizeOrNull(request.Barcode);
        var barcodeLookup = requestedBarcode is null
            ? null
            : BarcodeLookupNormalizer.Normalize(requestedBarcode);
        var stockCode = NormalizeOrNull(request.StockCode);
        var stockName = NormalizeOrNull(request.StockName);
        var supplierCode = NormalizeOrNull(request.SupplierCode);

        if (barcodeLookup is null && stockCode is null && stockName is null && supplierCode is null)
        {
            throw new ArgumentException("At least one product search filter is required.");
        }

        if (stockName is { Length: < 2 })
        {
            throw new ArgumentException("Stock name search text must be at least 2 characters.", nameof(request.StockName));
        }

        var take = NormalizeTake(request.Take);
        var lookupTake = request.IncludeDelisted
            ? take
            : Math.Min(MaxTake, Math.Max(take, take * 3));
        var connection = mikroDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            IReadOnlyCollection<ProductLookupItemDto> products;

            if (barcodeLookup is not null)
            {
                products = await ReadProductsByBarcodeCandidatesAsync(
                    connection,
                    request.WarehouseNo,
                    barcodeLookup,
                    stockCode,
                    stockName,
                    supplierCode,
                    lookupTake,
                    allowPartialSuffix: true,
                    cancellationToken);
            }
            else if (stockCode is null && IsDigitsOnly(stockName))
            {
                products = await ReadProductsByUnifiedNumericInputAsync(
                    connection,
                    request.WarehouseNo,
                    stockName!,
                    supplierCode,
                    lookupTake,
                    cancellationToken);
            }
            else
            {
                products = await ReadProductsAsync(
                    connection,
                    request.WarehouseNo,
                    null,
                    stockCode,
                    stockName,
                    supplierCode,
                    lookupTake,
                    null,
                    cancellationToken);
            }

            if (products.Count == 0 &&
                barcodeLookup is null &&
                IsDigitsOnly(stockCode))
            {
                var fallbackBarcodeLookup = BarcodeLookupNormalizer.Normalize(stockCode!);
                products = await ReadProductsByBarcodeCandidatesAsync(
                    connection,
                    request.WarehouseNo,
                    fallbackBarcodeLookup,
                    null,
                    null,
                    supplierCode,
                    lookupTake,
                    allowPartialSuffix: true,
                    cancellationToken);
            }

            products = await ApplyDelistStateAsync(
                connection,
                products,
                request.WarehouseNo,
                request.IncludeDelisted,
                take,
                cancellationToken);

            products = await EnrichWithPurchasePricesAsync(
                connection,
                products,
                supplierCode,
                cancellationToken);

            return await EnrichWithProcurementSourcesAsync(
                connection,
                products,
                request.WarehouseNo,
                cancellationToken);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> ReadProductsByUnifiedNumericInputAsync(
        DbConnection connection,
        int warehouseNo,
        string input,
        string? supplierCode,
        int take,
        CancellationToken cancellationToken)
    {
        var stockCodeProducts = await ReadProductsAsync(
            connection,
            warehouseNo,
            null,
            input,
            null,
            supplierCode,
            take,
            null,
            cancellationToken);
        var barcodeLookup = BarcodeLookupNormalizer.Normalize(input);
        var exactBarcodeProducts = await ReadProductsByBarcodeCandidatesAsync(
            connection,
            warehouseNo,
            barcodeLookup,
            null,
            null,
            supplierCode,
            take,
            allowPartialSuffix: false,
            cancellationToken);
        var exactProducts = MergeDistinctProducts(stockCodeProducts, exactBarcodeProducts, take);

        if (exactProducts.Count > 0)
        {
            return exactProducts;
        }

        return await ReadProductsByPartialBarcodeSuffixAsync(
            connection,
            warehouseNo,
            barcodeLookup,
            null,
            null,
            supplierCode,
            take,
            cancellationToken);
    }

    private static IReadOnlyCollection<ProductLookupItemDto> MergeDistinctProducts(
        IReadOnlyCollection<ProductLookupItemDto> primary,
        IReadOnlyCollection<ProductLookupItemDto> secondary,
        int take)
    {
        var products = new List<ProductLookupItemDto>(Math.Min(take, primary.Count + secondary.Count));
        var seenStockCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var product in primary.Concat(secondary))
        {
            if (!seenStockCodes.Add(product.StockCode))
            {
                continue;
            }

            products.Add(product);
            if (products.Count >= take)
            {
                break;
            }
        }

        return products;
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> ReadProductsByBarcodeCandidatesAsync(
        DbConnection connection,
        int warehouseNo,
        BarcodeLookupInfo barcodeLookup,
        string? stockCode,
        string? stockName,
        string? supplierCode,
        int take,
        bool allowPartialSuffix,
        CancellationToken cancellationToken)
    {
        foreach (var barcode in BarcodeLookupNormalizer.GetLookupCandidates(barcodeLookup))
        {
            var products = await ReadProductsAsync(
                connection,
                warehouseNo,
                barcode,
                stockCode,
                stockName,
                supplierCode,
                take,
                barcodeLookup,
                cancellationToken);

            if (products.Count > 0)
            {
                return products;
            }
        }

        if (!allowPartialSuffix)
        {
            return Array.Empty<ProductLookupItemDto>();
        }

        return await ReadProductsByPartialBarcodeSuffixAsync(
            connection,
            warehouseNo,
            barcodeLookup,
            stockCode,
            stockName,
            supplierCode,
            take,
            cancellationToken);
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> ReadProductsByPartialBarcodeSuffixAsync(
        DbConnection connection,
        int warehouseNo,
        BarcodeLookupInfo barcodeLookup,
        string? stockCode,
        string? stockName,
        string? supplierCode,
        int take,
        CancellationToken cancellationToken)
    {
        if (!BarcodeLookupNormalizer.IsPartialSuffixCandidate(barcodeLookup.LookupBarcode))
        {
            return Array.Empty<ProductLookupItemDto>();
        }

        var exactCandidates = BarcodeLookupNormalizer
            .GetLookupCandidates(barcodeLookup)
            .ToHashSet(StringComparer.Ordinal);
        var suffixCandidates = await ReadPartialBarcodeSuffixCandidatesAsync(
            connection,
            barcodeLookup.LookupBarcode,
            Math.Min(Math.Max(take, DefaultTake), MaxPartialBarcodeCandidates),
            cancellationToken);
        var products = new List<ProductLookupItemDto>(take);
        var seenStockCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var barcode in suffixCandidates.Where(candidate => !exactCandidates.Contains(candidate)))
        {
            var candidateProducts = await ReadProductsAsync(
                connection,
                warehouseNo,
                barcode,
                stockCode,
                stockName,
                supplierCode,
                take,
                barcodeLookup,
                cancellationToken);

            foreach (var product in candidateProducts)
            {
                if (!string.IsNullOrWhiteSpace(product.StockCode) &&
                    !seenStockCodes.Add(product.StockCode))
                {
                    continue;
                }

                products.Add(product);

                if (products.Count >= take)
                {
                    return products;
                }
            }
        }

        return products;
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> ReadProductsAsync(
        DbConnection connection,
        int warehouseNo,
        string? barcode,
        string? stockCode,
        string? stockName,
        string? supplierCode,
        int take,
        BarcodeLookupInfo? barcodeLookup,
        CancellationToken cancellationToken)
    {
        var products = new List<ProductLookupItemDto>(take);

        using var command = connection.CreateCommand();
        command.CommandText = "dbo.__StokveFiyatArama_Gokhan";
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = 300;

        AddParameter(command, "@sfiyat_deposirano", warehouseNo);
        AddParameter(command, "@bar_kodu", barcode);
        AddParameter(command, "@sfiyat_stokkod", stockCode);
        AddParameter(command, "@sto_isim", stockName);
        AddParameter(command, "@tedarikci", supplierCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (products.Count < take && await reader.ReadAsync(cancellationToken))
        {
            var salesBlockCode = ReadNullableInt(reader, "SatisDursun");
            var orderBlockCode = ReadNullableInt(reader, "SipDursun");
            var goodsAcceptanceBlockCode = ReadNullableInt(reader, "MalKabulDursun");
            var primaryUnitMultiplier = ReadDouble(reader, "BirimKatsayisi");
            var secondaryUnitMultiplier = NormalizeUnitMultiplier(ReadDouble(reader, "BirimKatsayisi2"));

            products.Add(new ProductLookupItemDto(
                ReadInt(reader, "DepoNo"),
                ReadString(reader, "BarKodu"),
                ReadString(reader, "StokKod"),
                ReadString(reader, "StokIsim"),
                ReadDouble(reader, "Fiyati"),
                ReadInt(reader, "FiyatTipKodu"),
                ReadString(reader, "BirimAd"),
                secondaryUnitMultiplier > 0d ? secondaryUnitMultiplier : NormalizeUnitMultiplier(primaryUnitMultiplier, 1d),
                ReadString(reader, "BirimAd2"),
                secondaryUnitMultiplier,
                salesBlockCode,
                orderBlockCode,
                goodsAcceptanceBlockCode,
                IsBlocked(salesBlockCode),
                IsBlocked(orderBlockCode),
                IsBlocked(goodsAcceptanceBlockCode),
                ReadString(reader, "UrunSorumlusu"),
                barcodeLookup?.OriginalBarcode,
                barcodeLookup?.LookupBarcode,
                barcodeLookup?.IsVariableWeightBarcode ?? false,
                barcodeLookup?.EmbeddedQuantity,
                barcodeLookup?.EmbeddedQuantityUnit,
                barcodeLookup?.IsCheckDigitValid));
        }

        return products;
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> ApplyDelistStateAsync(
        DbConnection connection,
        IReadOnlyCollection<ProductLookupItemDto> products,
        int warehouseNo,
        bool includeDelisted,
        int take,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
        {
            return products;
        }

        var stockCodes = products
            .Select(product => NormalizeOrNull(product.StockCode))
            .Where(stockCode => stockCode is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTake)
            .ToArray();

        if (stockCodes.Length == 0)
        {
            return products.Take(take).ToArray();
        }

        var delistStates = await ReadDelistStatesAsync(
            connection,
            stockCodes,
            warehouseNo,
            cancellationToken);

        return products
            .Select(product =>
            {
                var stockCode = NormalizeOrNull(product.StockCode);
                var delistState = stockCode is not null && delistStates.TryGetValue(stockCode, out var state)
                    ? state
                    : DelistState.Active;

                return product with
                {
                    IsPassive = delistState.IsPassive,
                    IsDelisted = delistState.IsDelisted,
                    DelistReason = delistState.Reason
                };
            })
            .Where(product => includeDelisted || !product.IsDelisted)
            .Take(take)
            .ToArray();
    }

    private static async Task<Dictionary<string, DelistState>> ReadDelistStatesAsync(
        DbConnection connection,
        IReadOnlyCollection<string> stockCodes,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var delistStates = new Dictionary<string, DelistState>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        var stockCodeParameterNames = stockCodes
            .Select((stockCode, index) =>
            {
                var parameterName = $"@delistStockCode{index}";
                AddParameter(command, parameterName, stockCode);
                return parameterName;
            })
            .ToArray();

        command.CommandText = $"""
            SELECT
                LTRIM(RTRIM(stock.sto_kod)) AS StockCode,
                ISNULL(warehouseDetail.sdp_Pasif_fl, ISNULL(stock.sto_pasif_fl, 0)) AS IsPassive,
                LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, ''))) AS ModelCode,
                LTRIM(RTRIM(ISNULL(stock.sto_isim, ''))) AS StockName
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            LEFT JOIN dbo.STOK_DEPO_DETAYLARI AS warehouseDetail WITH (NOLOCK)
                ON warehouseDetail.sdp_depo_kod = stock.sto_kod
               AND warehouseDetail.sdp_depo_no = @warehouseNo
            WHERE stock.sto_kod IN ({string.Join(", ", stockCodeParameterNames)});
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;

        AddParameter(command, "@warehouseNo", warehouseNo);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var stockCode = ReadString(reader, "StockCode");
            if (string.IsNullOrWhiteSpace(stockCode))
            {
                continue;
            }

            var isPassive = ReadBool(reader, "IsPassive");
            var isDls = StartsWithDls(ReadString(reader, "StockName"));

            delistStates[stockCode] = new DelistState(
                isPassive,
                isDls,
                BuildDelistReason(isPassive, isDls));
        }

        return delistStates;
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> EnrichWithPurchasePricesAsync(
        DbConnection connection,
        IReadOnlyCollection<ProductLookupItemDto> products,
        string? supplierCode,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0 || supplierCode is null)
        {
            return products;
        }

        var stockCodes = products
            .Select(product => NormalizeOrNull(product.StockCode))
            .Where(stockCode => stockCode is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTake)
            .ToArray();

        if (stockCodes.Length == 0)
        {
            return products;
        }

        var purchasePrices = await ReadLatestPurchasePricesAsync(
            connection,
            stockCodes,
            supplierCode,
            cancellationToken);

        if (purchasePrices.Count == 0)
        {
            return products;
        }

        return products
            .Select(product =>
            {
                var stockCode = NormalizeOrNull(product.StockCode);
                return stockCode is not null && purchasePrices.TryGetValue(stockCode, out var purchasePrice)
                    ? product with
                    {
                        PurchasePrice = purchasePrice.NetPrice,
                        PurchaseGrossPrice = purchasePrice.GrossPrice,
                        PurchasePriceSource = "purchase-requirement",
                        PurchaseSupplierCode = purchasePrice.SupplierCode
                    }
                    : product;
            })
            .ToArray();
    }

    private static async Task<Dictionary<string, PurchasePriceSnapshot>> ReadLatestPurchasePricesAsync(
        DbConnection connection,
        IReadOnlyCollection<string> stockCodes,
        string? supplierCode,
        CancellationToken cancellationToken)
    {
        var purchasePrices = new Dictionary<string, PurchasePriceSnapshot>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        var stockCodeParameterNames = stockCodes
            .Select((stockCode, index) =>
            {
                var parameterName = $"@stockCode{index}";
                AddParameter(command, parameterName, stockCode);
                return parameterName;
            })
            .ToArray();

        command.CommandText = $"""
            WITH LatestPurchaseTerms AS
            (
                SELECT
                    LTRIM(RTRIM(sas_stok_kod)) AS StockCode,
                    LTRIM(RTRIM(sas_cari_kod)) AS SupplierCode,
                    ISNULL(sas_brut_fiyat, 0) AS GrossPrice,
                    ISNULL(sas_isk_yuzde1, 0) AS DiscountRate1,
                    ISNULL(sas_isk_yuzde2, 0) AS DiscountRate2,
                    ISNULL(sas_isk_yuzde3, 0) AS DiscountRate3,
                    ISNULL(sas_isk_yuzde4, 0) AS DiscountRate4,
                    ISNULL(sas_isk_yuzde5, 0) AS DiscountRate5,
                    ISNULL(sas_isk_yuzde6, 0) AS DiscountRate6,
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY LTRIM(RTRIM(sas_stok_kod))
                        ORDER BY
                            ISNULL(sas_belge_tarih, CONVERT(datetime, '19000101', 112)) DESC,
                            ISNULL(sas_create_date, CONVERT(datetime, '19000101', 112)) DESC,
                            sas_Guid DESC
                    ) AS RowNo
                FROM dbo.SATINALMA_SARTLARI WITH (NOLOCK)
                WHERE sas_stok_kod IS NOT NULL
                  AND LTRIM(RTRIM(sas_stok_kod)) IN ({string.Join(", ", stockCodeParameterNames)})
                  AND (@supplierCode IS NULL OR sas_cari_kod = @supplierCode)
            )
            SELECT
                StockCode,
                SupplierCode,
                GrossPrice,
                DiscountRate1,
                DiscountRate2,
                DiscountRate3,
                DiscountRate4,
                DiscountRate5,
                DiscountRate6
            FROM LatestPurchaseTerms
            WHERE RowNo = 1;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;

        AddParameter(command, "@supplierCode", supplierCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var stockCode = ReadString(reader, "StockCode");
            if (string.IsNullOrWhiteSpace(stockCode))
            {
                continue;
            }

            var grossPrice = ReadDouble(reader, "GrossPrice");
            purchasePrices[stockCode] = new PurchasePriceSnapshot(
                CalculateNetPurchasePrice(
                    grossPrice,
                    ReadDouble(reader, "DiscountRate1"),
                    ReadDouble(reader, "DiscountRate2"),
                    ReadDouble(reader, "DiscountRate3"),
                    ReadDouble(reader, "DiscountRate4"),
                    ReadDouble(reader, "DiscountRate5"),
                    ReadDouble(reader, "DiscountRate6")),
                grossPrice,
                NormalizeOrNull(ReadString(reader, "SupplierCode")));
        }

        return purchasePrices;
    }

    private static async Task<IReadOnlyCollection<ProductLookupItemDto>> EnrichWithProcurementSourcesAsync(
        DbConnection connection,
        IReadOnlyCollection<ProductLookupItemDto> products,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        if (products.Count == 0)
        {
            return products;
        }

        var stockCodes = products
            .Select(product => NormalizeOrNull(product.StockCode))
            .Where(stockCode => stockCode is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTake)
            .ToArray();

        if (stockCodes.Length == 0)
        {
            return products;
        }

        var procurementSources = await ReadProcurementSourcesAsync(
            connection,
            stockCodes,
            warehouseNo,
            cancellationToken);

        return products
            .Select(product =>
            {
                var stockCode = NormalizeOrNull(product.StockCode);
                var source = stockCode is not null && procurementSources.TryGetValue(stockCode, out var value)
                    ? value
                    : ProcurementSourceSnapshot.Empty;

                return product with
                {
                    ModelCode = source.ModelCode,
                    ProcurementType = ResolveProcurementType(
                        source.SourceWarehouses.Count > 0,
                        source.HasPurchaseRequirement),
                    SourceWarehouses = source.SourceWarehouses,
                    HasPurchaseRequirement = source.HasPurchaseRequirement
                };
            })
            .ToArray();
    }

    private static async Task<Dictionary<string, ProcurementSourceSnapshot>> ReadProcurementSourcesAsync(
        DbConnection connection,
        IReadOnlyCollection<string> stockCodes,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var sources = new Dictionary<string, ProcurementSourceAccumulator>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        var stockCodeParameterNames = stockCodes
            .Select((stockCode, index) =>
            {
                var parameterName = $"@procurementStockCode{index}";
                AddParameter(command, parameterName, stockCode);
                return parameterName;
            })
            .ToArray();

        command.CommandText = $"""
            SELECT
                LTRIM(RTRIM(stock.sto_kod)) AS StockCode,
                LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N''))) AS ModelCode,
                CASE
                    WHEN NULLIF(LTRIM(RTRIM(ISNULL(stock.sto_sat_cari_kod, N''))), N'') IS NOT NULL
                         OR EXISTS
                         (
                             SELECT 1
                             FROM dbo.SATINALMA_SARTLARI AS purchaseTerm WITH (NOLOCK)
                             WHERE purchaseTerm.sas_stok_kod = stock.sto_kod
                               AND ISNULL(purchaseTerm.sas_iptal, 0) = 0
                               AND NULLIF(LTRIM(RTRIM(ISNULL(purchaseTerm.sas_cari_kod, N''))), N'') IS NOT NULL
                               AND (purchaseTerm.sas_depo_no IS NULL OR purchaseTerm.sas_depo_no IN (0, @warehouseNo))
                               AND (purchaseTerm.sas_basla_tarih IS NULL OR purchaseTerm.sas_basla_tarih <= GETDATE())
                               AND
                               (
                                   purchaseTerm.sas_bitis_tarih IS NULL
                                   OR purchaseTerm.sas_bitis_tarih <= CONVERT(date, '19000101', 112)
                                   OR purchaseTerm.sas_bitis_tarih >= CONVERT(date, GETDATE())
                               )
                         )
                    THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END AS HasPurchaseRequirement,
                sourceWarehouse.dep_no AS SourceWarehouseNo,
                sourceWarehouse.dep_adi AS SourceWarehouseName
            FROM dbo.STOKLAR AS stock WITH (NOLOCK)
            OUTER APPLY
            (
                SELECT warehouse.dep_no, warehouse.dep_adi
                FROM dbo.DEPOLAR AS warehouse WITH (NOLOCK)
                CROSS APPLY STRING_SPLIT(
                    REPLACE(REPLACE(ISNULL(warehouse.dep_barkod_yazici_yolu, N''), N';', N','), N'|', N','),
                    N',') AS sourceModel
                WHERE LTRIM(RTRIM(sourceModel.value)) = LTRIM(RTRIM(ISNULL(stock.sto_model_kodu, N'')))
                  AND EXISTS
                  (
                      SELECT 1
                      FROM dbo.STOK_DEPO_DETAYLARI AS sourceDetail WITH (NOLOCK)
                      WHERE sourceDetail.sdp_depo_no = warehouse.dep_no
                        AND sourceDetail.sdp_depo_kod = stock.sto_kod
                        AND ISNULL(sourceDetail.sdp_sipdursun, 0) = 0
                  )
            ) AS sourceWarehouse
            WHERE stock.sto_kod IN ({string.Join(", ", stockCodeParameterNames)})
            ORDER BY stock.sto_kod, sourceWarehouse.dep_no;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;

        AddParameter(command, "@warehouseNo", warehouseNo);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var stockCode = ReadString(reader, "StockCode");
            if (string.IsNullOrWhiteSpace(stockCode))
            {
                continue;
            }

            if (!sources.TryGetValue(stockCode, out var source))
            {
                source = new ProcurementSourceAccumulator(
                    ReadString(reader, "ModelCode"),
                    ReadBool(reader, "HasPurchaseRequirement"));
                sources[stockCode] = source;
            }

            if (reader["SourceWarehouseNo"] is not DBNull)
            {
                source.AddWarehouse(new ProductSourceWarehouseDto(
                    Convert.ToInt32(reader["SourceWarehouseNo"]),
                    ReadString(reader, "SourceWarehouseName")));
            }
        }

        return sources.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToSnapshot(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveProcurementType(bool hasSourceWarehouse, bool hasPurchaseRequirement) =>
        (hasSourceWarehouse, hasPurchaseRequirement) switch
        {
            (true, true) => "Mixed",
            (true, false) => "Warehouse",
            (false, true) => "Company",
            _ => "Unassigned"
        };

    private static double CalculateNetPurchasePrice(
        double grossPrice,
        double discountRate1,
        double discountRate2,
        double discountRate3,
        double discountRate4,
        double discountRate5,
        double discountRate6)
    {
        var netPrice = grossPrice;
        foreach (var discountRate in new[]
                 {
                     discountRate1,
                     discountRate2,
                     discountRate3,
                     discountRate4,
                     discountRate5,
                     discountRate6
                 })
        {
            if (discountRate <= 0d)
            {
                continue;
            }

            netPrice -= netPrice * discountRate / 100d;
        }

        return Math.Round(netPrice, 4, MidpointRounding.AwayFromZero);
    }

    private static async Task<IReadOnlyCollection<string>> ReadPartialBarcodeSuffixCandidatesAsync(
        DbConnection connection,
        string suffix,
        int take,
        CancellationToken cancellationToken)
    {
        var barcodes = new List<string>(take);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (@take)
                LTRIM(RTRIM(bar_kodu)) AS Barcode
            FROM dbo.BARKOD_TANIMLARI WITH (NOLOCK)
            WHERE COALESCE(bar_iptal, 0) <> 1
              AND bar_kodu IS NOT NULL
              AND LTRIM(RTRIM(bar_kodu)) <> ''
              AND LTRIM(RTRIM(bar_kodu)) LIKE '%' + @suffix
            ORDER BY
                CASE WHEN LEN(LTRIM(RTRIM(bar_kodu))) = @suffixLength THEN 0 ELSE 1 END,
                LTRIM(RTRIM(bar_kodu));
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;

        AddParameter(command, "@suffix", suffix);
        AddParameter(command, "@suffixLength", suffix.Length);
        AddParameter(command, "@take", take);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var barcode = ReadString(reader, "Barcode");
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                barcodes.Add(barcode);
            }
        }

        return barcodes;
    }

    private static int NormalizeTake(int take) =>
        take <= 0 ? DefaultTake : Math.Min(take, MaxTake);

    private static string? NormalizeOrNull(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsDigitsOnly(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);

    private static bool IsBlocked(int? code) =>
        code.GetValueOrDefault() != 0;

    private static bool StartsWithDls(string? value) =>
        NormalizeOrNull(value)?.StartsWith("DLS", StringComparison.OrdinalIgnoreCase) == true;

    private static string? BuildDelistReason(bool isPassive, bool isDls) =>
        (isPassive, isDls) switch
        {
            (true, true) => "Pasif; DLS",
            (true, false) => "Pasif",
            (false, true) => "DLS",
            _ => null
        };

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string name) =>
        Convert.ToInt32(reader[name]);

    private static int? ReadNullableInt(DbDataReader reader, string name) =>
        reader[name] is DBNull ? null : Convert.ToInt32(reader[name]);

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader[name] is DBNull ? 0d : Convert.ToDouble(reader[name]);

    private static bool ReadBool(DbDataReader reader, string name) =>
        reader[name] is not DBNull && Convert.ToBoolean(reader[name]);

    private static double NormalizeUnitMultiplier(double value, double fallback = 0d)
    {
        var normalized = Math.Abs(value);
        return normalized > 0d ? normalized : fallback;
    }

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;

    private sealed record PurchasePriceSnapshot(
        double NetPrice,
        double GrossPrice,
        string? SupplierCode);

    private sealed record ProcurementSourceSnapshot(
        string ModelCode,
        bool HasPurchaseRequirement,
        IReadOnlyCollection<ProductSourceWarehouseDto> SourceWarehouses)
    {
        public static readonly ProcurementSourceSnapshot Empty = new(
            string.Empty,
            false,
            Array.Empty<ProductSourceWarehouseDto>());
    }

    private sealed class ProcurementSourceAccumulator(
        string modelCode,
        bool hasPurchaseRequirement)
    {
        private readonly Dictionary<int, ProductSourceWarehouseDto> sourceWarehouses = new();

        public void AddWarehouse(ProductSourceWarehouseDto warehouse) =>
            sourceWarehouses.TryAdd(warehouse.WarehouseNo, warehouse);

        public ProcurementSourceSnapshot ToSnapshot() => new(
            modelCode,
            hasPurchaseRequirement,
            sourceWarehouses.Values
                .OrderBy(warehouse => warehouse.WarehouseNo)
                .ToArray());
    }

    private sealed record DelistState(
        bool IsPassive,
        bool IsDls,
        string? Reason)
    {
        public static readonly DelistState Active = new(false, false, null);

        public bool IsDelisted => IsPassive || IsDls;
    }
}
