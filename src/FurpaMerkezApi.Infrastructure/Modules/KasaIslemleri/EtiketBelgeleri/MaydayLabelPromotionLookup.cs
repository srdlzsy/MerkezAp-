using System.Data;
using System.Globalization;
using System.Text.Json;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class MaydayLabelPromotionLookup(
    IConfiguration configuration,
    ILogger<MaydayLabelPromotionLookup> logger) : ILabelPromotionLookup
{
    private const int CommandTimeoutSeconds = 30;

    public async Task<IReadOnlyDictionary<int, LabelPromotionDto>> GetActiveCardPromotionsAsync(
        int warehouseNo,
        IReadOnlyDictionary<int, double> pricesByPlu,
        CancellationToken cancellationToken)
    {
        if (warehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(warehouseNo));
        }

        var validPrices = pricesByPlu
            .Where(item => item.Key > 0)
            .GroupBy(item => item.Key)
            .ToDictionary(group => group.Key, group => group.First().Value);

        if (validPrices.Count == 0)
        {
            return new Dictionary<int, LabelPromotionDto>();
        }

        var connectionString = configuration.GetConnectionString("MaydayConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning(
                "Label promotion lookup was skipped because MaydayConnection is not configured. WarehouseNo={WarehouseNo}",
                warehouseNo);
            return new Dictionary<int, LabelPromotionDto>();
        }

        try
        {
            return await QueryPromotionsAsync(
                connectionString,
                warehouseNo,
                validPrices,
                cancellationToken);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            logger.LogError(
                exception,
                "Active label promotions could not be read from Mayday. WarehouseNo={WarehouseNo}, PluCount={PluCount}",
                warehouseNo,
                validPrices.Count);
            return new Dictionary<int, LabelPromotionDto>();
        }
    }

    private static async Task<IReadOnlyDictionary<int, LabelPromotionDto>> QueryPromotionsAsync(
        string connectionString,
        int warehouseNo,
        IReadOnlyDictionary<int, double> pricesByPlu,
        CancellationToken cancellationToken)
    {
        var requestedPluJson = JsonSerializer.Serialize(
            pricesByPlu.Keys.Select(pluNo => new
            {
                pluNo = pluNo.ToString(CultureInfo.InvariantCulture)
            }));
        var now = DateTime.Now;
        var candidates = new List<PromotionCandidate>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = CommandTimeoutSeconds;
        command.CommandText =
            """
            SELECT DISTINCT
                TRY_CONVERT(int, requested.[pluNo]) AS [PluNo],
                CONVERT(nvarchar(50), promotion.[ProKodu]) AS [PromotionCode],
                LTRIM(RTRIM(COALESCE(promotion.[ProTip], ''))) AS [PromotionType],
                LTRIM(RTRIM(COALESCE(promotion.[ProBaslik], ''))) AS [PromotionName],
                LTRIM(RTRIM(COALESCE(promotion.[ProPromosyonAciklama], ''))) AS [Description],
                TRY_CONVERT(float, promotion.[ProİndirimOrani]) AS [DiscountRate],
                TRY_CONVERT(float, promotion.[ProİndirimTutari]) AS [DiscountAmount],
                TRY_CONVERT(datetime2, promotion.[ProBasTarihi]) AS [StartDate],
                TRY_CONVERT(datetime2, promotion.[ProBitTarihi]) AS [ExpirationDate]
            FROM dbo.[PROMOSYON_TANIMLARI] AS promotion WITH (NOLOCK)
            INNER JOIN dbo.[PROMOSYON_SUBELER] AS branch WITH (NOLOCK)
                ON branch.[subeProKod] = promotion.[ProKodu]
            INNER JOIN OPENJSON(@requestedPluJson)
                WITH ([pluNo] nvarchar(25) '$.pluNo') AS requested
                ON requested.[pluNo] = promotion.[ProUrunPluNo]
            WHERE branch.[subeProSubeKod] = @warehouseNo
              AND LTRIM(RTRIM(COALESCE(promotion.[ProTip], ''))) = N'P2'
              AND LTRIM(RTRIM(COALESCE(promotion.[PromMusteriKodu], ''))) = N'2012'
              AND COALESCE(promotion.[ProPasif], 0) = 0
              AND (promotion.[ProBasTarihi] IS NULL OR promotion.[ProBasTarihi] <= @now)
              AND (promotion.[ProBitTarihi] IS NULL OR promotion.[ProBitTarihi] >= @now);
            """;
        command.Parameters.Add(new SqlParameter("@requestedPluJson", SqlDbType.NVarChar, -1)
        {
            Value = requestedPluJson
        });
        command.Parameters.Add(new SqlParameter("@warehouseNo", SqlDbType.Int)
        {
            Value = warehouseNo
        });
        command.Parameters.Add(new SqlParameter("@now", SqlDbType.DateTime2)
        {
            Value = now
        });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new PromotionCandidate(
                reader.GetInt32(reader.GetOrdinal("PluNo")),
                ReadString(reader, "PromotionCode"),
                ReadString(reader, "PromotionType"),
                ReadString(reader, "PromotionName"),
                ReadString(reader, "Description"),
                ReadDouble(reader, "DiscountRate"),
                ReadDouble(reader, "DiscountAmount"),
                ReadNullableDateTime(reader, "StartDate"),
                ReadNullableDateTime(reader, "ExpirationDate")));
        }

        return candidates
            .Where(candidate => pricesByPlu.ContainsKey(candidate.PluNo))
            .Select(candidate =>
            {
                var normalPrice = pricesByPlu[candidate.PluNo];
                var promotionPrice = CalculatePromotionPrice(
                    normalPrice,
                    candidate.DiscountRate,
                    candidate.DiscountAmount);

                return new
                {
                    candidate.PluNo,
                    Promotion = new LabelPromotionDto
                    {
                        IsActive = true,
                        PromotionCode = candidate.PromotionCode,
                        PromotionType = candidate.PromotionType,
                        PromotionName = candidate.PromotionName,
                        Description = candidate.Description,
                        NormalPrice = normalPrice,
                        PromotionPrice = promotionPrice,
                        DiscountRate = candidate.DiscountRate,
                        DiscountAmount = candidate.DiscountAmount,
                        StartDate = candidate.StartDate,
                        ExpirationDate = candidate.ExpirationDate
                    }
                };
            })
            .GroupBy(item => item.PluNo)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Promotion.PromotionPrice)
                    .ThenBy(item => item.Promotion.ExpirationDate ?? DateTime.MaxValue)
                    .ThenBy(item => item.Promotion.PromotionCode, StringComparer.OrdinalIgnoreCase)
                    .First()
                    .Promotion);
    }

    internal static double CalculatePromotionPrice(
        double normalPrice,
        double discountRate,
        double discountAmount)
    {
        var calculatedPrice = discountAmount != 0d
            ? normalPrice - discountAmount
            : normalPrice - (normalPrice * discountRate / 100d);

        return Math.Round(Math.Max(0d, calculatedPrice), 2, MidpointRounding.AwayFromZero);
    }

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
        int PluNo,
        string PromotionCode,
        string PromotionType,
        string PromotionName,
        string Description,
        double DiscountRate,
        double DiscountAmount,
        DateTime? StartDate,
        DateTime? ExpirationDate);
}
