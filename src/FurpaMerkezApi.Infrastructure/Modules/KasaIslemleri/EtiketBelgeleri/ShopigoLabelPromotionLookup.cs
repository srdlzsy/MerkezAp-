using System.Data;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class ShopigoLabelPromotionLookup(
    IConfiguration configuration,
    ILogger<ShopigoLabelPromotionLookup> logger) : ILabelPromotionLookup
{
    private const int CommandTimeoutSeconds = 30;

    public async Task<IReadOnlyDictionary<string, LabelPromotionDto>> GetActiveProductPromotionsAsync(
        int warehouseNo,
        IReadOnlyDictionary<string, double> pricesByStockCode,
        CancellationToken cancellationToken)
    {
        var normalizedPrices = pricesByStockCode
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.OrdinalIgnoreCase);

        if (normalizedPrices.Count == 0)
        {
            return EmptyResult();
        }

        var candidates = await QueryCandidatesSafelyAsync(warehouseNo, cancellationToken);

        return candidates
            .Where(candidate => normalizedPrices.ContainsKey(candidate.ProductCode))
            .Select(candidate => new
            {
                candidate.ProductCode,
                Promotion = LabelPromotionPriceCalculator.ApplyNormalPrice(
                    candidate.Promotion,
                    normalizedPrices[candidate.ProductCode])
            })
            .GroupBy(item => item.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Promotion.EffectiveUnitPrice ?? item.Promotion.NormalPrice)
                    .ThenBy(item => item.Promotion.ExpirationDate ?? DateTime.MaxValue)
                    .ThenBy(item => item.Promotion.PromotionCode, StringComparer.OrdinalIgnoreCase)
                    .First()
                    .Promotion,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyCollection<string>> GetActiveProductCodesAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var candidates = await QueryCandidatesSafelyAsync(warehouseNo, cancellationToken);

        return candidates
            .Select(candidate => candidate.ProductCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<PromotionCandidate>> QueryCandidatesSafelyAsync(
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var connectionString = configuration.GetConnectionString("ShopigoCiroConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning(
                "Label promotion lookup was skipped because ShopigoCiroConnection is not configured. WarehouseNo={WarehouseNo}",
                warehouseNo);
            return Array.Empty<PromotionCandidate>();
        }

        try
        {
            return await QueryCandidatesAsync(
                connectionString,
                warehouseNo,
                cancellationToken);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            logger.LogError(
                exception,
                "Active label promotions could not be read from Shopigo. WarehouseNo={WarehouseNo}",
                warehouseNo);
            return Array.Empty<PromotionCandidate>();
        }
    }

    private static async Task<IReadOnlyCollection<PromotionCandidate>> QueryCandidatesAsync(
        string connectionString,
        int warehouseNo,
        CancellationToken cancellationToken)
    {
        var candidates = new List<PromotionCandidate>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            WITH parsed_promotions AS
            (
                SELECT
                    promotion.[id],
                    LTRIM(RTRIM(promotion.[name])) AS [PromotionName],
                    promotion_type.[code] AS [PromotionType],
                    promotion_type.[description] AS [Description],
                    promotion.[start_date] AS [StartDate],
                    promotion.[end_date] AS [ExpirationDate],
                    LTRIM(RTRIM(parsed_rule.[required_inventory_code])) AS [RequiredProductCode],
                    COALESCE(parsed_rule.[required_quantity], 0) AS [RequiredQuantity],
                    LTRIM(RTRIM(parsed_rule.[discounted_inventory_code])) AS [DiscountedProductCode],
                    COALESCE(parsed_rule.[discounted_quantity], 0) AS [DiscountedQuantity],
                    LTRIM(RTRIM(COALESCE(parsed_rule.[discount_type], ''))) AS [DiscountType],
                    COALESCE(parsed_rule.[discount_value], 0) AS [DiscountValue]
                FROM dbo.[promotions] AS promotion WITH (NOLOCK)
                INNER JOIN dbo.[promotion_types] AS promotion_type WITH (NOLOCK)
                    ON promotion_type.[id] = promotion.[promotion_type_id]
                CROSS APPLY OPENJSON(
                    CASE WHEN ISJSON(promotion.[parameters]) = 1 THEN promotion.[parameters] ELSE N'{}' END,
                    '$.product_discounts')
                WITH
                (
                    [required_inventory_code] nvarchar(25) '$.required_inventory_code',
                    [required_quantity] float '$.required_quantity',
                    [discounted_inventory_code] nvarchar(25) '$.discounted_inventory_code',
                    [discounted_quantity] float '$.discounted_quantity',
                    [discount_type] nvarchar(30) '$.discount_type',
                    [discount_value] float '$.value'
                ) AS parsed_rule
                WHERE promotion_type.[code] = N'PUF1'
                  AND promotion.[is_active] = 1
                  AND promotion.[deleted_at] IS NULL
                  AND promotion.[start_date] <= @now
                  AND promotion.[end_date] > @now
                  AND
                  (
                      NOT EXISTS
                      (
                          SELECT 1
                          FROM dbo.[promotion_branches] AS branch_scope WITH (NOLOCK)
                          WHERE branch_scope.[promotion_id] = promotion.[id]
                      )
                      OR EXISTS
                      (
                          SELECT 1
                          FROM dbo.[promotion_branches] AS branch_scope WITH (NOLOCK)
                          INNER JOIN dbo.[branches] AS branch WITH (NOLOCK)
                              ON branch.[id] = branch_scope.[branch_id]
                          WHERE branch_scope.[promotion_id] = promotion.[id]
                            AND branch.[depo_id] = @warehouseNo
                            AND branch.[deleted_at] IS NULL
                      )
                  )
            ),
            promotion_products AS
            (
                SELECT
                    parsed.*,
                    parsed.[RequiredProductCode] AS [ProductCode],
                    CASE
                        WHEN parsed.[RequiredProductCode] = parsed.[DiscountedProductCode] THEN N'Both'
                        ELSE N'Required'
                    END AS [ProductRole]
                FROM parsed_promotions AS parsed

                UNION ALL

                SELECT
                    parsed.*,
                    parsed.[DiscountedProductCode] AS [ProductCode],
                    N'Discounted' AS [ProductRole]
                FROM parsed_promotions AS parsed
                WHERE parsed.[DiscountedProductCode] <> parsed.[RequiredProductCode]
            )
            SELECT DISTINCT
                product.[ProductCode],
                product.[ProductRole],
                product.[id] AS [PromotionId],
                product.[PromotionType],
                product.[PromotionName],
                product.[Description],
                product.[StartDate],
                product.[ExpirationDate],
                product.[RequiredProductCode],
                product.[RequiredQuantity],
                product.[DiscountedProductCode],
                product.[DiscountedQuantity],
                product.[DiscountType],
                product.[DiscountValue]
            FROM promotion_products AS product
            INNER JOIN dbo.[inventory] AS inventory WITH (NOLOCK)
                ON inventory.[inventory_code] = product.[ProductCode]
            WHERE product.[ProductCode] <> N''
              AND inventory.[is_promotable] = 1
              AND inventory.[deleted_at] IS NULL
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.[promosyon_yapilmayacak_urunler] AS excluded WITH (NOLOCK)
                  WHERE excluded.[inventory_code] = product.[ProductCode]
              );
            """;
        command.Parameters.Add(new SqlParameter("@warehouseNo", SqlDbType.Int)
        {
            Value = warehouseNo
        });
        command.Parameters.Add(new SqlParameter("@now", SqlDbType.DateTime2)
        {
            Value = DateTime.Now
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var discountType = ReadString(reader, "DiscountType");
            var discountValue = ReadDouble(reader, "DiscountValue");

            candidates.Add(new PromotionCandidate(
                ReadString(reader, "ProductCode"),
                new LabelPromotionDto
                {
                    Source = "Shopigo",
                    IsActive = true,
                    PromotionCode = reader.GetInt32(reader.GetOrdinal("PromotionId")).ToString(),
                    PromotionType = ReadString(reader, "PromotionType"),
                    PromotionName = ReadString(reader, "PromotionName"),
                    Description = ReadString(reader, "Description"),
                    CampaignText = ReadString(reader, "PromotionName"),
                    ProductRole = ReadString(reader, "ProductRole"),
                    RequiredProductCode = ReadString(reader, "RequiredProductCode"),
                    RequiredQuantity = ReadDouble(reader, "RequiredQuantity"),
                    DiscountedProductCode = ReadString(reader, "DiscountedProductCode"),
                    DiscountedQuantity = ReadDouble(reader, "DiscountedQuantity"),
                    DiscountType = discountType,
                    DiscountValue = discountValue,
                    DiscountRate = string.Equals(discountType, "PERCENTAGE", StringComparison.OrdinalIgnoreCase)
                        ? discountValue
                        : 0d,
                    DiscountAmount = 0d,
                    StartDate = ReadNullableDateTime(reader, "StartDate"),
                    ExpirationDate = ReadNullableDateTime(reader, "ExpirationDate")
                }));
        }

        return candidates;
    }

    private static IReadOnlyDictionary<string, LabelPromotionDto> EmptyResult() =>
        new Dictionary<string, LabelPromotionDto>(StringComparer.OrdinalIgnoreCase);

    private static string ReadString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal).Trim();
    }

    private static double ReadDouble(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? 0d : Convert.ToDouble(reader.GetValue(ordinal));
    }

    private static DateTime? ReadNullableDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private sealed record PromotionCandidate(
        string ProductCode,
        LabelPromotionDto Promotion);
}
