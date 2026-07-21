using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.PromosyonRaporlari;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.PromosyonRaporlari;

public sealed class PromotionReportsUseCase(
    FurpaDbContext furpaDbContext,
    MikroDbContext mikroDbContext,
    IConfiguration configuration)
    : IPromotionReportsUseCase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 1000;

    private static readonly string[] PromotionCodeColumns =
    [
        "PromotionCode",
        "PromosyonKodu",
        "PROMOSYON_KODU",
        "PROMOSYONKODU",
        "ProCode",
        "ProKod",
        "ProKodu",
        "subeProKod",
        "pro_kod",
        "pro_kodu"
    ];

    private static readonly string[] BranchNoColumns =
    [
        "BranchNo",
        "SubeNo",
        "MagazaNo",
        "Sube",
        "sube_no",
        "magaza_no",
        "BranchCode",
        "branch_no",
        "subeProSubeKod",
        "WarehouseNo",
        "DepoNo"
    ];

    private static readonly string[] ExpirationDateColumns =
    [
        "ExpirationDate",
        "ExpireDate",
        "EndDate",
        "BitisTarihi",
        "SonTarih",
        "ProBitTarihi",
        "pro_bitis_tarihi"
    ];

    public async Task<IReadOnlyCollection<PromotionBulletinListItemDto>> GetBulletinsAsync(
        PromotionBulletinListRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var definitions = await ListPromotionDefinitionsAsync(normalized.WarehouseNo, cancellationToken);

        return definitions
            .Where(item => !normalized.OnlyActive || IsActive(item, normalized.ActiveOn))
            .Where(item => MatchesSearch(item, normalized.Search))
            .OrderByDescending(item => IsActive(item, normalized.ActiveOn))
            .ThenBy(item => item.EndDate ?? DateTime.MaxValue)
            .ThenBy(item => item.PromotionCode, StringComparer.OrdinalIgnoreCase)
            .Take(normalized.Take)
            .Select(item => new PromotionBulletinListItemDto(
                item.PromotionCode,
                item.PromotionName,
                item.PromotionType,
                item.Description,
                item.StartDate,
                item.EndDate,
                item.IsPassive,
                IsActive(item, normalized.ActiveOn),
                item.CustomerCode,
                item.PluNo,
                item.ProductPluNo,
                item.LimitAmount,
                item.DiscountRate,
                item.DiscountAmount,
                item.BranchNos))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PromotionBulletinOptionDto>> GetBulletinOptionsAsync(
        PromotionBulletinListRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var definitions = await ListPromotionDefinitionsAsync(normalized.WarehouseNo, cancellationToken);

        return definitions
            .Where(item => !normalized.OnlyActive || IsActive(item, normalized.ActiveOn))
            .Where(item => MatchesSearch(item, normalized.Search))
            .OrderByDescending(item => IsActive(item, normalized.ActiveOn))
            .ThenBy(item => item.EndDate ?? DateTime.MaxValue)
            .ThenBy(item => item.PromotionCode, StringComparer.OrdinalIgnoreCase)
            .Take(normalized.Take)
            .Select(item => new PromotionBulletinOptionDto(
                item.PromotionCode,
                item.PromotionName,
                item.PromotionType,
                IsActive(item, normalized.ActiveOn),
                item.StartDate,
                item.EndDate))
            .ToArray();
    }

    public async Task<PromotionPerformanceReportDto> GetPerformanceAsync(
        PromotionPerformanceRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var rows = await ListPromotionUsageRowsAsync(normalized, cancellationToken);
        var receiptCounts = await ListPromotionReceiptCountsAsync(normalized, cancellationToken);
        var definitions = await ListPromotionDefinitionsAsync(normalized.WarehouseNo, cancellationToken);
        var definitionByCode = CreateDefinitionLookup(definitions);
        var costs = await ListStandardCostsAsync(rows.Select(item => item.ProductCode), cancellationToken);

        var items = rows
            .Select(item => item.WithCost(costs.GetValueOrDefault(item.ProductCode)))
            .Where(item => MatchesSearch(item.PromotionCode, definitionByCode, normalized.Search))
            .GroupBy(item => item.PromotionCode, StringComparer.OrdinalIgnoreCase)
            .Select(grouped =>
            {
                definitionByCode.TryGetValue(grouped.Key, out var definition);
                var netSalesAmount = grouped.Sum(item => item.NetSalesAmount);
                var grossSalesAmount = grouped.Sum(item => item.GrossSalesAmount);
                var discountAmount = grouped.Sum(item => item.DiscountAmount);
                var estimatedCostAmount = grouped.Sum(item => item.EstimatedCostAmount);
                var marginAmount = netSalesAmount - estimatedCostAmount;
                var receiptCount = receiptCounts
                    .Where(item => string.Equals(item.PromotionCode, grouped.Key, StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.ReceiptCount);

                return new PromotionPerformanceItemDto(
                    grouped.Key,
                    definition?.PromotionName ?? string.Empty,
                    definition?.PromotionType ?? string.Empty,
                    definition?.Description ?? string.Empty,
                    grouped.Sum(item => item.UsageCount),
                    receiptCount,
                    Round(grouped.Sum(item => item.SoldQuantity)),
                    Round(netSalesAmount),
                    Round(grossSalesAmount),
                    Round(discountAmount),
                    Round(estimatedCostAmount),
                    Round(marginAmount),
                    Percent(marginAmount, netSalesAmount),
                    Percent(discountAmount, grossSalesAmount),
                    grouped.Min(item => item.FirstSaleDate),
                    grouped.Max(item => item.LastSaleDate));
            })
            .OrderByDescending(item => item.DiscountAmount)
            .ThenByDescending(item => item.GrossSalesAmount)
            .ThenBy(item => item.PromotionCode, StringComparer.OrdinalIgnoreCase)
            .Take(normalized.Take)
            .ToArray();

        var netSalesTotal = items.Sum(item => item.NetSalesAmount);
        var estimatedCostTotal = items.Sum(item => item.EstimatedCostAmount);
        var marginTotal = netSalesTotal - estimatedCostTotal;

        return new PromotionPerformanceReportDto(
            normalized.StartDate,
            normalized.EndDateInclusive,
            normalized.WarehouseNo,
            items.Length,
            items.Sum(item => item.UsageCount),
            items.Sum(item => item.ReceiptCount),
            Round(items.Sum(item => item.SoldQuantity)),
            Round(netSalesTotal),
            Round(items.Sum(item => item.GrossSalesAmount)),
            Round(items.Sum(item => item.DiscountAmount)),
            Round(estimatedCostTotal),
            Round(marginTotal),
            Percent(marginTotal, netSalesTotal),
            items);
    }

    public async Task<IReadOnlyCollection<PromotionBranchPerformanceItemDto>> GetBranchPerformanceAsync(
        PromotionPerformanceRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var rows = await ListPromotionUsageRowsAsync(normalized, cancellationToken);
        var receiptCounts = await ListPromotionReceiptCountsAsync(normalized, cancellationToken);
        var definitions = await ListPromotionDefinitionsAsync(normalized.WarehouseNo, cancellationToken);
        var definitionByCode = CreateDefinitionLookup(definitions);
        var costs = await ListStandardCostsAsync(rows.Select(item => item.ProductCode), cancellationToken);
        var branchNames = await ListBranchNamesAsync(rows.Select(item => item.BranchNo), cancellationToken);

        return rows
            .Select(item => item.WithCost(costs.GetValueOrDefault(item.ProductCode)))
            .Where(item => MatchesSearch(item.PromotionCode, definitionByCode, normalized.Search))
            .GroupBy(
                item => new PromotionBranchKey(item.BranchNo, item.PromotionCode),
                item => item)
            .Select(grouped =>
            {
                definitionByCode.TryGetValue(grouped.Key.PromotionCode, out var definition);
                branchNames.TryGetValue(grouped.Key.BranchNo, out var branchName);

                var netSalesAmount = grouped.Sum(item => item.NetSalesAmount);
                var grossSalesAmount = grouped.Sum(item => item.GrossSalesAmount);
                var estimatedCostAmount = grouped.Sum(item => item.EstimatedCostAmount);
                var marginAmount = netSalesAmount - estimatedCostAmount;
                var receiptCount = receiptCounts
                    .Where(item =>
                        item.BranchNo == grouped.Key.BranchNo &&
                        string.Equals(item.PromotionCode, grouped.Key.PromotionCode, StringComparison.OrdinalIgnoreCase))
                    .Sum(item => item.ReceiptCount);

                return new PromotionBranchPerformanceItemDto(
                    grouped.Key.BranchNo,
                    branchName ?? string.Empty,
                    grouped.Key.PromotionCode,
                    definition?.PromotionName ?? string.Empty,
                    definition?.PromotionType ?? string.Empty,
                    grouped.Sum(item => item.UsageCount),
                    receiptCount,
                    Round(grouped.Sum(item => item.SoldQuantity)),
                    Round(netSalesAmount),
                    Round(grossSalesAmount),
                    Round(grouped.Sum(item => item.DiscountAmount)),
                    Round(estimatedCostAmount),
                    Round(marginAmount),
                    Percent(marginAmount, netSalesAmount));
            })
            .OrderBy(item => item.BranchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.BranchNo)
            .ThenByDescending(item => item.DiscountAmount)
            .ThenBy(item => item.PromotionCode, StringComparer.OrdinalIgnoreCase)
            .Take(normalized.Take)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<PromotionDefinition>> ListPromotionDefinitionsAsync(
        int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var connectionString = GetRequiredConnectionString(
            "MaydayConnection",
            "MaydayMarketConnection",
            "MaydaYMarketConnection");

        var promotionRows = await ReadSqlRowsAsync(
            connectionString,
            "SELECT * FROM dbo.PROMOSYON_TANIMLARI WITH (NOLOCK)",
            _ => { },
            cancellationToken);
        var branchRows = await ReadSqlRowsAsync(
            connectionString,
            "SELECT * FROM dbo.PROMOSYON_SUBELER WITH (NOLOCK)",
            _ => { },
            cancellationToken);

        if (branchRows.Count > 0 &&
            (!RowsContainAnyColumn(branchRows, BranchNoColumns) ||
             !RowsContainAnyColumn(branchRows, PromotionCodeColumns)))
        {
            throw new InvalidOperationException(
                "PROMOSYON_SUBELER rows were found, but BranchNo/PromotionCode columns could not be mapped.");
        }

        var branchNosByPromotion = branchRows
            .Select(row => new
            {
                PromotionCode = ReadString(row, PromotionCodeColumns),
                BranchNo = ReadInt(row, BranchNoColumns)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.PromotionCode) && item.BranchNo.HasValue)
            .GroupBy(item => item.PromotionCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                grouped => grouped.Key,
                grouped => grouped
                    .Select(item => item.BranchNo!.Value)
                    .Distinct()
                    .OrderBy(item => item)
                    .ToArray() as IReadOnlyCollection<int>,
                StringComparer.OrdinalIgnoreCase);

        var filterByBranch = warehouseNo.HasValue && branchRows.Count > 0;
        var definitions = new List<PromotionDefinition>();

        foreach (var row in promotionRows)
        {
            var promotionCode = ReadString(row, PromotionCodeColumns);
            if (string.IsNullOrWhiteSpace(promotionCode))
            {
                continue;
            }

            branchNosByPromotion.TryGetValue(promotionCode, out var branchNos);
            branchNos ??= Array.Empty<int>();

            if (filterByBranch && !branchNos.Contains(warehouseNo!.Value))
            {
                continue;
            }

            definitions.Add(new PromotionDefinition(
                promotionCode,
                ReadString(row, "ProName", "Name", "PromosyonAdi", "ProBaslik", "ProPromosyonAciklama"),
                ReadString(row, "PromotionType", "ProType", "ProTypeCode", "ProTip", "ProTipi", "PromosyonTipi", "PromosyonTuru", "Tip").ToUpperInvariant(),
                ReadString(row, "ProDescription", "Description", "Aciklama", "ProAciklama", "ProPromosyonAciklama", "ProFisMesaj1"),
                ReadDate(row, "StartDate", "BaslangicTarihi", "StartTime", "ProBasTarihi", "pro_baslangic_tarihi"),
                ReadDate(row, ExpirationDateColumns),
                ReadInt(row, "ProPassive", "proPassive", "ProPasif", "pro_pasif", "Passive", "pasif") is > 0,
                ReadString(row, "PromotionCustomerCode", "CustomerCode", "MusteriKodu", "CariKodu", "PromMusteriKodu"),
                ReadInt(row, "PluNo", "PLUNo", "Plu", "ProPluNo", "ProUrunPluNo", "plu_no", "sto_plu_no"),
                ReadInt(row, "ProductPluNo", "ProductPLUNo", "UrunPluNo", "ProUygulanacakPluNo", "ProUygUrunPluNo", "ProUrunPluNo"),
                ReadDouble(row, "ProLimitAmount", "LimitAmount", "LimitTutar", "ProLimitTutar", "pro_limit_amount"),
                ReadDouble(row, "DiscountRate", "IndirimOrani", "ProIndirimOrani", "ProOzelKodIndirimOrani", "ProUygIndirimOrani", "discount_rate"),
                ReadDouble(row, "DiscountAmount", "IndirimTutari", "ProIndirimTutari", "ProUygIndirimTutari", "discount_amount"),
                branchNos));
        }

        return MergeDuplicateDefinitions(definitions);
    }

    private async Task<IReadOnlyCollection<PromotionUsageAggregateRow>> ListPromotionUsageRowsAsync(
        NormalizedPromotionPerformanceRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                LTRIM(RTRIM(COALESCE(promotion.PromosyonKod, ''))) AS PromotionCode,
                COALESCE(TRY_CONVERT(int, promotion.Sube), 0) AS BranchNo,
                LTRIM(RTRIM(COALESCE(NULLIF(promotion.UrunKod, ''), line.UrunKodu, ''))) AS ProductCode,
                COUNT(1) AS UsageCount,
                SUM(COALESCE(line.Miktar, 0)) AS SoldQuantity,
                SUM(COALESCE(line.NetTutar, 0)) AS NetSalesAmount,
                SUM(COALESCE(line.NetTutar, 0) + COALESCE(line.KdvTutari, 0)) AS GrossSalesAmount,
                SUM(COALESCE(promotion.Tutar, 0)) AS DiscountAmount,
                MIN(promotion.Tarih) AS FirstSaleDate,
                MAX(promotion.Tarih) AS LastSaleDate
            FROM dbo.PosFaturaPromosyons AS promotion WITH (NOLOCK)
            LEFT JOIN dbo.PosFaturaSatirs AS line WITH (NOLOCK)
                ON line.SatirGuid = promotion.SatirGuid
            WHERE promotion.Tarih >= @startDate
              AND promotion.Tarih < @endDateExclusive
              AND NULLIF(LTRIM(RTRIM(COALESCE(promotion.PromosyonKod, ''))), '') IS NOT NULL
              AND (@warehouseNo IS NULL OR TRY_CONVERT(int, promotion.Sube) = @warehouseNo)
              AND (@promotionCode IS NULL OR LTRIM(RTRIM(COALESCE(promotion.PromosyonKod, ''))) = @promotionCode)
            GROUP BY
                LTRIM(RTRIM(COALESCE(promotion.PromosyonKod, ''))),
                COALESCE(TRY_CONVERT(int, promotion.Sube), 0),
                LTRIM(RTRIM(COALESCE(NULLIF(promotion.UrunKod, ''), line.UrunKodu, '')));
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", request.StartDate);
                AddParameter(command, "@endDateExclusive", request.EndDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@promotionCode", request.PromotionCode);
            },
            reader => new PromotionUsageAggregateRow(
                ReadString(reader, "PromotionCode"),
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "ProductCode"),
                ReadInt(reader, "UsageCount"),
                ReadDouble(reader, "SoldQuantity"),
                ReadDouble(reader, "NetSalesAmount"),
                ReadDouble(reader, "GrossSalesAmount"),
                ReadDouble(reader, "DiscountAmount"),
                ReadNullableDateTime(reader, "FirstSaleDate"),
                ReadNullableDateTime(reader, "LastSaleDate")),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<PromotionReceiptCountRow>> ListPromotionReceiptCountsAsync(
        NormalizedPromotionPerformanceRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                LTRIM(RTRIM(COALESCE(PromosyonKod, ''))) AS PromotionCode,
                COALESCE(TRY_CONVERT(int, Sube), 0) AS BranchNo,
                COUNT(DISTINCT FaturaGuid) AS ReceiptCount
            FROM dbo.PosFaturaPromosyons WITH (NOLOCK)
            WHERE Tarih >= @startDate
              AND Tarih < @endDateExclusive
              AND NULLIF(LTRIM(RTRIM(COALESCE(PromosyonKod, ''))), '') IS NOT NULL
              AND (@warehouseNo IS NULL OR TRY_CONVERT(int, Sube) = @warehouseNo)
              AND (@promotionCode IS NULL OR LTRIM(RTRIM(COALESCE(PromosyonKod, ''))) = @promotionCode)
            GROUP BY
                LTRIM(RTRIM(COALESCE(PromosyonKod, ''))),
                COALESCE(TRY_CONVERT(int, Sube), 0);
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", request.StartDate);
                AddParameter(command, "@endDateExclusive", request.EndDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@promotionCode", request.PromotionCode);
            },
            reader => new PromotionReceiptCountRow(
                ReadString(reader, "PromotionCode"),
                ReadInt(reader, "BranchNo"),
                ReadInt(reader, "ReceiptCount")),
            cancellationToken);
    }

    private async Task<Dictionary<string, double>> ListStandardCostsAsync(
        IEnumerable<string> productCodes,
        CancellationToken cancellationToken)
    {
        var codes = productCodes
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var costs = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var chunk in codes.Chunk(800))
        {
            var parameterNames = chunk
                .Select((_, index) => $"@code{index}")
                .ToArray();
            var sql = $"""
                SELECT
                    sto_kod AS ProductCode,
                    COALESCE(sto_standartmaliyet, 0) AS StandardCost
                FROM dbo.STOKLAR WITH (NOLOCK)
                WHERE sto_kod IN ({string.Join(", ", parameterNames)});
                """;

            var rows = await ExecuteReaderAsync(
                mikroDbContext.Database.GetDbConnection(),
                sql,
                command =>
                {
                    for (var index = 0; index < chunk.Length; index++)
                    {
                        AddParameter(command, parameterNames[index], chunk[index]);
                    }
                },
                reader => new
                {
                    ProductCode = ReadString(reader, "ProductCode"),
                    StandardCost = ReadDouble(reader, "StandardCost")
                },
                cancellationToken);

            foreach (var row in rows)
            {
                costs[row.ProductCode] = row.StandardCost;
            }
        }

        return costs;
    }

    private async Task<Dictionary<int, string>> ListBranchNamesAsync(
        IEnumerable<int> branchNos,
        CancellationToken cancellationToken)
    {
        var numbers = branchNos
            .Where(item => item > 0)
            .Distinct()
            .ToArray();
        var names = new Dictionary<int, string>();

        foreach (var chunk in numbers.Chunk(800))
        {
            var parameterNames = chunk
                .Select((_, index) => $"@branch{index}")
                .ToArray();
            var sql = $"""
                SELECT
                    dep_no AS BranchNo,
                    COALESCE(dep_adi, '') AS BranchName
                FROM dbo.DEPOLAR WITH (NOLOCK)
                WHERE dep_no IN ({string.Join(", ", parameterNames)});
                """;

            var rows = await ExecuteReaderAsync(
                mikroDbContext.Database.GetDbConnection(),
                sql,
                command =>
                {
                    for (var index = 0; index < chunk.Length; index++)
                    {
                        AddParameter(command, parameterNames[index], chunk[index]);
                    }
                },
                reader => new
                {
                    BranchNo = ReadInt(reader, "BranchNo"),
                    BranchName = ReadString(reader, "BranchName")
                },
                cancellationToken);

            foreach (var row in rows)
            {
                names[row.BranchNo] = row.BranchName;
            }
        }

        return names;
    }

    private async Task<IReadOnlyCollection<Dictionary<string, object?>>> ReadSqlRowsAsync(
        string connectionString,
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 180;
        configureCommand(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                row[reader.GetName(ordinal)] = reader.IsDBNull(ordinal)
                    ? null
                    : reader.GetValue(ordinal);
            }

            rows.Add(row);
        }

        return rows;
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        DbConnection connection,
        string sql,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var items = new List<T>();
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

    private string GetRequiredConnectionString(params string[] keys)
    {
        foreach (var key in keys)
        {
            var connectionString = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                return connectionString;
            }
        }

        throw new InvalidOperationException(
            $"Required connection string was not found. Expected one of: {string.Join(", ", keys)}.");
    }

    private static NormalizedPromotionBulletinListRequest Normalize(PromotionBulletinListRequest request)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        return new NormalizedPromotionBulletinListRequest(
            request.WarehouseNo,
            request.ActiveOn?.Date ?? DateTime.Today,
            request.OnlyActive,
            NormalizeText(request.Search),
            NormalizeTake(request.Take));
    }

    private static NormalizedPromotionPerformanceRequest Normalize(PromotionPerformanceRequest request)
    {
        if (request.StartDate == default)
        {
            throw new ArgumentException("Start date is required.", nameof(request.StartDate));
        }

        if (request.EndDate == default)
        {
            throw new ArgumentException("End date is required.", nameof(request.EndDate));
        }

        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        var startDate = request.StartDate.Date;
        var endDateInclusive = request.EndDate.Date;
        if (endDateInclusive < startDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(request.EndDate));
        }

        return new NormalizedPromotionPerformanceRequest(
            request.WarehouseNo,
            startDate,
            endDateInclusive,
            endDateInclusive.AddDays(1),
            NormalizeText(request.PromotionCode),
            NormalizeText(request.Search),
            NormalizeTake(request.Take));
    }

    private static bool IsActive(PromotionDefinition definition, DateTime activeOn) =>
        !definition.IsPassive &&
        (!definition.StartDate.HasValue || definition.StartDate.Value.Date <= activeOn) &&
        (!definition.EndDate.HasValue || definition.EndDate.Value.Date >= activeOn);

    private static Dictionary<string, PromotionDefinition> CreateDefinitionLookup(
        IEnumerable<PromotionDefinition> definitions) =>
        MergeDuplicateDefinitions(definitions)
            .ToDictionary(
                item => item.PromotionCode,
                item => item,
                StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyCollection<PromotionDefinition> MergeDuplicateDefinitions(
        IEnumerable<PromotionDefinition> definitions) =>
        definitions
            .GroupBy(item => item.PromotionCode, StringComparer.OrdinalIgnoreCase)
            .Select(MergeDefinitionGroup)
            .ToArray();

    private static PromotionDefinition MergeDefinitionGroup(
        IGrouping<string, PromotionDefinition> grouped)
    {
        var items = grouped.ToArray();
        var startDates = items
            .Where(item => item.StartDate.HasValue)
            .Select(item => item.StartDate!.Value)
            .ToArray();
        var endDates = items
            .Where(item => item.EndDate.HasValue)
            .Select(item => item.EndDate!.Value)
            .ToArray();
        var primary = items
            .OrderBy(item => item.IsPassive)
            .ThenByDescending(item => item.EndDate ?? DateTime.MaxValue)
            .ThenByDescending(item => item.StartDate ?? DateTime.MinValue)
            .First();
        var branchNos = items
            .SelectMany(item => item.BranchNos)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        return primary with
        {
            PromotionName = FirstNonEmpty(items.Select(item => item.PromotionName)),
            PromotionType = FirstNonEmpty(items.Select(item => item.PromotionType)),
            Description = FirstNonEmpty(items.Select(item => item.Description)),
            StartDate = startDates.Length == 0 ? null : startDates.Min(),
            EndDate = endDates.Length == 0 ? null : endDates.Max(),
            IsPassive = items.All(item => item.IsPassive),
            CustomerCode = FirstNonEmpty(items.Select(item => item.CustomerCode)),
            PluNo = primary.PluNo ?? items.Select(item => item.PluNo).FirstOrDefault(item => item.HasValue),
            ProductPluNo = primary.ProductPluNo ?? items.Select(item => item.ProductPluNo).FirstOrDefault(item => item.HasValue),
            LimitAmount = primary.LimitAmount ?? items.Select(item => item.LimitAmount).FirstOrDefault(item => item.HasValue),
            DiscountRate = primary.DiscountRate ?? items.Select(item => item.DiscountRate).FirstOrDefault(item => item.HasValue),
            DiscountAmount = primary.DiscountAmount ?? items.Select(item => item.DiscountAmount).FirstOrDefault(item => item.HasValue),
            BranchNos = branchNos
        };
    }

    private static string FirstNonEmpty(IEnumerable<string> values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static bool MatchesSearch(PromotionDefinition definition, string? search) =>
        string.IsNullOrWhiteSpace(search) ||
        definition.PromotionCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        definition.PromotionName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        definition.PromotionType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        definition.CustomerCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        definition.Description.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSearch(
        string promotionCode,
        IReadOnlyDictionary<string, PromotionDefinition> definitions,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        if (promotionCode.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return definitions.TryGetValue(promotionCode, out var definition) &&
               MatchesSearch(definition, search);
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static int NormalizeTake(int take) =>
        Math.Clamp(take <= 0 ? DefaultTake : take, 1, MaxTake);

    private static bool RowsContainAnyColumn(
        IReadOnlyCollection<Dictionary<string, object?>> rows,
        IReadOnlyCollection<string> columnNames) =>
        rows.Count > 0 && columnNames.Any(rows.First().ContainsKey);

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
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

    private static double ReadDouble(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? 0d
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static string ReadString(
        IReadOnlyDictionary<string, object?> row,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetValue(name, out var value) && value is not null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static int? ReadInt(
        IReadOnlyDictionary<string, object?> row,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var doubleValue))
            {
                return Convert.ToInt32(doubleValue);
            }
        }

        return null;
    }

    private static double? ReadDouble(
        IReadOnlyDictionary<string, object?> row,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTime? ReadDate(
        IReadOnlyDictionary<string, object?> row,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            if (value is DateTime dateTime)
            {
                return dateTime.Date;
            }

            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }
        }

        return null;
    }

    private static double Round(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double Percent(double numerator, double denominator) =>
        Math.Abs(denominator) <= 0.000001d
            ? 0d
            : Round((numerator / denominator) * 100d);

    private sealed record NormalizedPromotionBulletinListRequest(
        int? WarehouseNo,
        DateTime ActiveOn,
        bool OnlyActive,
        string? Search,
        int Take);

    private sealed record NormalizedPromotionPerformanceRequest(
        int? WarehouseNo,
        DateTime StartDate,
        DateTime EndDateInclusive,
        DateTime EndDateExclusive,
        string? PromotionCode,
        string? Search,
        int Take);

    private sealed record PromotionDefinition(
        string PromotionCode,
        string PromotionName,
        string PromotionType,
        string Description,
        DateTime? StartDate,
        DateTime? EndDate,
        bool IsPassive,
        string CustomerCode,
        int? PluNo,
        int? ProductPluNo,
        double? LimitAmount,
        double? DiscountRate,
        double? DiscountAmount,
        IReadOnlyCollection<int> BranchNos);

    private sealed record PromotionUsageAggregateRow(
        string PromotionCode,
        int BranchNo,
        string ProductCode,
        int UsageCount,
        double SoldQuantity,
        double NetSalesAmount,
        double GrossSalesAmount,
        double DiscountAmount,
        DateTime? FirstSaleDate,
        DateTime? LastSaleDate)
    {
        public PromotionUsageCostRow WithCost(double standardCost) =>
            new(
                PromotionCode,
                BranchNo,
                ProductCode,
                UsageCount,
                SoldQuantity,
                NetSalesAmount,
                GrossSalesAmount,
                DiscountAmount,
                SoldQuantity * standardCost,
                FirstSaleDate,
                LastSaleDate);
    }

    private sealed record PromotionUsageCostRow(
        string PromotionCode,
        int BranchNo,
        string ProductCode,
        int UsageCount,
        double SoldQuantity,
        double NetSalesAmount,
        double GrossSalesAmount,
        double DiscountAmount,
        double EstimatedCostAmount,
        DateTime? FirstSaleDate,
        DateTime? LastSaleDate);

    private sealed record PromotionReceiptCountRow(
        string PromotionCode,
        int BranchNo,
        int ReceiptCount);

    private sealed record PromotionBranchKey(
        int BranchNo,
        string PromotionCode);
}
