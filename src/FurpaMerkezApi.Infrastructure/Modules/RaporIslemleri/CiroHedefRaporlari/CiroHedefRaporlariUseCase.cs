using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.CiroHedefRaporlari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.CiroHedefRaporlari;

public sealed class CiroHedefRaporlariUseCase(MikroDbContext mikroDbContext) : ICiroHedefRaporlariUseCase
{
    public async Task<CiroHedefKarsilastirmaRaporDto> GetComparisonReportAsync(
        CiroHedefKarsilastirmaRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EndDate < request.StartDate)
        {
            throw new ArgumentException("Bitis tarihi baslangic tarihinden once olamaz.", nameof(request.EndDate));
        }

        var currentStart = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var currentEndExclusive = request.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);

        var prevStart = request.StartDate.AddYears(-1).ToDateTime(TimeOnly.MinValue);
        var prevEndExclusive = request.EndDate.AddYears(-1).ToDateTime(TimeOnly.MinValue).AddDays(1);

        var targetYear = request.EndDate.Year;
        var targetMonth = request.EndDate.Month;
        var goals = await GetGoalsInternalAsync(targetYear, cancellationToken);
        var activeGoal = goals
            .Where(g =>
                (g.Month == targetMonth || g.Month == request.StartDate.Month) &&
                (!request.WarehouseNo.HasValue || g.WarehouseNo == request.WarehouseNo || g.WarehouseNo is null))
            .OrderByDescending(g => g.Month == targetMonth)
            .ThenByDescending(g => request.WarehouseNo.HasValue && g.WarehouseNo == request.WarehouseNo)
            .FirstOrDefault()
            ?? goals.FirstOrDefault();

        var ciroGoalPercent = activeGoal?.CiroGoalPercent ?? 0m;
        var customerGoalPercent = activeGoal?.CustomerGoalPercent ?? 0m;

        var currentBranchData = await FetchPeriodTurnoversAsync(
            currentStart,
            currentEndExclusive,
            request.WarehouseNo,
            request.Region,
            cancellationToken);

        var prevBranchData = await FetchPeriodTurnoversAsync(
            prevStart,
            prevEndExclusive,
            request.WarehouseNo,
            request.Region,
            cancellationToken);

        var allBranchNos = currentBranchData.Keys
            .Union(prevBranchData.Keys)
            .OrderBy(b => b)
            .ToArray();

        var branches = new List<CiroHedefSubeKarsilastirmaDto>();

        foreach (var branchNo in allBranchNos)
        {
            currentBranchData.TryGetValue(branchNo, out var curr);
            prevBranchData.TryGetValue(branchNo, out var prev);

            var branchName = curr?.BranchName ?? prev?.BranchName ?? $"Şube {branchNo}";
            var region = curr?.Region ?? prev?.Region ?? string.Empty;

            var currCash = curr?.CashTotal ?? 0m;
            var currCredit = curr?.CreditTotal ?? 0m;
            var currGift = curr?.GiftTotal ?? 0m;
            var currFutures = curr?.FuturesTotal ?? 0m;
            var currExpense = curr?.ExpenseTotal ?? 0m;
            var currOverall = curr?.OverallTotal ?? (currCash + currCredit + currGift + currFutures);
            var currCust = curr?.CustomerCount ?? 0;
            var currAvgBasket = currCust > 0 ? Math.Round(currOverall / currCust, 2) : 0m;

            var prevCash = prev?.CashTotal ?? 0m;
            var prevCredit = prev?.CreditTotal ?? 0m;
            var prevGift = prev?.GiftTotal ?? 0m;
            var prevFutures = prev?.FuturesTotal ?? 0m;
            var prevExpense = prev?.ExpenseTotal ?? 0m;
            var prevOverall = prev?.OverallTotal ?? (prevCash + prevCredit + prevGift + prevFutures);
            var prevCust = prev?.CustomerCount ?? 0;
            var prevAvgBasket = prevCust > 0 ? Math.Round(prevOverall / prevCust, 2) : 0m;

            var branchGoal = goals
                .Where(g =>
                    (g.Month == targetMonth || g.Month == request.StartDate.Month) &&
                    (g.WarehouseNo == branchNo || g.WarehouseNo is null))
                .OrderByDescending(g => g.WarehouseNo == branchNo)
                .ThenByDescending(g => g.Month == targetMonth)
                .FirstOrDefault();

            var branchCiroGoalPercent = branchGoal?.CiroGoalPercent ?? ciroGoalPercent;
            var branchCustomerGoalPercent = branchGoal?.CustomerGoalPercent ?? customerGoalPercent;

            var targetTurnover = Math.Round(prevOverall * (1m + (branchCiroGoalPercent / 100m)), 2);
            var turnoverRate = targetTurnover > 0m
                ? Math.Round((currOverall / targetTurnover) * 100m, 2)
                : 0m;

            var targetCust = Math.Round(prevCust * (1m + (branchCustomerGoalPercent / 100m)), 0);
            var custRate = targetCust > 0m
                ? Math.Round((currCust / targetCust) * 100m, 2)
                : 0m;

            branches.Add(new CiroHedefSubeKarsilastirmaDto(
                branchNo,
                branchName,
                region,
                currCash,
                currCredit,
                currGift,
                currFutures,
                currExpense,
                currOverall,
                currCust,
                currAvgBasket,
                prevCash,
                prevCredit,
                prevGift,
                prevFutures,
                prevExpense,
                prevOverall,
                prevCust,
                prevAvgBasket,
                targetTurnover,
                turnoverRate,
                targetCust,
                custRate));
        }

        var totalCurrTurnover = branches.Sum(b => b.CurrentOverallTotal);
        var totalPrevTurnover = branches.Sum(b => b.PreviousOverallTotal);
        var totalTargetTurnover = Math.Round(totalPrevTurnover * (1m + (ciroGoalPercent / 100m)), 2);
        var overallTurnoverRate = totalTargetTurnover > 0m
            ? Math.Round((totalCurrTurnover / totalTargetTurnover) * 100m, 2)
            : 0m;

        var totalCurrCust = branches.Sum(b => b.CurrentCustomerCount);
        var totalPrevCust = branches.Sum(b => b.PreviousCustomerCount);
        var totalTargetCust = Math.Round(totalPrevCust * (1m + (customerGoalPercent / 100m)), 0);
        var overallCustRate = totalTargetCust > 0m
            ? Math.Round((totalCurrCust / totalTargetCust) * 100m, 2)
            : 0m;

        var currOverallAvgBasket = totalCurrCust > 0
            ? Math.Round(totalCurrTurnover / totalCurrCust, 2)
            : 0m;

        var prevOverallAvgBasket = totalPrevCust > 0
            ? Math.Round(totalPrevTurnover / totalPrevCust, 2)
            : 0m;

        return new CiroHedefKarsilastirmaRaporDto(
            request.StartDate,
            request.EndDate,
            ciroGoalPercent,
            customerGoalPercent,
            totalCurrTurnover,
            totalPrevTurnover,
            totalTargetTurnover,
            overallTurnoverRate,
            totalCurrCust,
            totalPrevCust,
            totalTargetCust,
            overallCustRate,
            currOverallAvgBasket,
            prevOverallAvgBasket,
            branches);
    }

    public async Task<IReadOnlyCollection<HedefTanimDto>> GetGoalsAsync(
        int? year,
        CancellationToken cancellationToken) =>
        await GetGoalsInternalAsync(year ?? DateTime.Today.Year, cancellationToken);

    private async Task<IReadOnlyList<HedefTanimDto>> GetGoalsInternalAsync(
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                Id,
                COALESCE(Yil, @year) AS [Year],
                COALESCE(Ay_Sira, 1) AS [Month],
                COALESCE(Ay_Isim, '') AS MonthName,
                CAST(COALESCE(HedeflenenCiroOrani_Yuzde, 0) AS decimal(18,2)) AS CiroGoalPercent,
                CAST(COALESCE(HedeflenenMusteriOrani_Yuzde, 0) AS decimal(18,2)) AS CustomerGoalPercent,
                CAST(COALESCE(HedeflenenGiderOrani_Yuzde, 0) AS decimal(18,2)) AS ExpenseGoalPercent,
                Sube_No AS SubeNo
            FROM dbo.HEDEF_TANIMLARI WITH (NOLOCK)
            WHERE Yil = @year
            ORDER BY [Year], [Month];
            """;

        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@year", year);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<HedefTanimDto>();

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new HedefTanimDto(
                    ReadInt(reader, "Id"),
                    ReadInt(reader, "Year"),
                    ReadInt(reader, "Month"),
                    ReadString(reader, "MonthName"),
                    ReadDecimal(reader, "CiroGoalPercent"),
                    ReadDecimal(reader, "CustomerGoalPercent"),
                    ReadDecimal(reader, "ExpenseGoalPercent"),
                    ReadNullableInt(reader, "SubeNo")));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<Dictionary<int, BranchTurnoverRaw>> FetchPeriodTurnoversAsync(
        DateTime startDate,
        DateTime endDateExclusive,
        int? warehouseNo,
        string? region,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH Totals AS (
                SELECT
                    tt.BranchNo,
                    SUM(COALESCE(tt.CustomerCount, 0)) AS CustomerCount,
                    SUM(COALESCE(tt.TurnoverOverallTotal, 0)) AS OverallTotal
                FROM dbo.TurnoverTotals AS tt WITH (NOLOCK)
                WHERE tt.TurnoverDate >= @startDate
                  AND tt.TurnoverDate < @endDateExclusive
                  AND (@warehouseNo IS NULL OR tt.BranchNo = @warehouseNo)
                GROUP BY tt.BranchNo
            ),
            Details AS (
                SELECT
                    tt.BranchNo,
                    SUM(COALESCE(td.CashTotal, 0)) AS CashTotal,
                    SUM(COALESCE(td.CreditTotal, 0)) AS CreditTotal,
                    SUM(COALESCE(td.GiftCardTotal, 0)) AS GiftTotal,
                    SUM(COALESCE(td.FuturesSalesTotal, 0)) AS FuturesTotal,
                    SUM(COALESCE(td.ExpenseNoteTotal, 0)) AS ExpenseTotal
                FROM dbo.TurnoverTotals AS tt WITH (NOLOCK)
                INNER JOIN dbo.TurnoverDetails AS td WITH (NOLOCK) ON tt.TurnoverId = td.TurnoverId
                WHERE tt.TurnoverDate >= @startDate
                  AND tt.TurnoverDate < @endDateExclusive
                  AND (@warehouseNo IS NULL OR tt.BranchNo = @warehouseNo)
                GROUP BY tt.BranchNo
            )
            SELECT
                t.BranchNo,
                COALESCE(w.dep_adi, 'Şube ' + CONVERT(varchar(20), t.BranchNo)) AS BranchName,
                COALESCE(CONVERT(varchar(50), w.dep_bolge_kodu), '') AS Region,
                t.CustomerCount,
                t.OverallTotal,
                COALESCE(d.CashTotal, 0) AS CashTotal,
                COALESCE(d.CreditTotal, 0) AS CreditTotal,
                COALESCE(d.GiftTotal, 0) AS GiftTotal,
                COALESCE(d.FuturesTotal, 0) AS FuturesTotal,
                COALESCE(d.ExpenseTotal, 0) AS ExpenseTotal
            FROM Totals AS t
            LEFT JOIN Details AS d ON t.BranchNo = d.BranchNo
            LEFT JOIN dbo.DEPOLAR AS w WITH (NOLOCK) ON t.BranchNo = w.dep_no
            WHERE (@region IS NULL OR CONVERT(varchar(50), w.dep_bolge_kodu) = @region)
            ORDER BY t.BranchNo;
            """;

        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@startDate", startDate);
            AddParameter(command, "@endDateExclusive", endDateExclusive);
            AddParameter(command, "@warehouseNo", warehouseNo);
            AddParameter(command, "@region", string.IsNullOrWhiteSpace(region) ? null : region);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var dict = new Dictionary<int, BranchTurnoverRaw>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var branchNo = ReadInt(reader, "BranchNo");
                dict[branchNo] = new BranchTurnoverRaw(
                    branchNo,
                    ReadString(reader, "BranchName"),
                    ReadString(reader, "Region"),
                    ReadInt(reader, "CustomerCount"),
                    ReadDecimal(reader, "OverallTotal"),
                    ReadDecimal(reader, "CashTotal"),
                    ReadDecimal(reader, "CreditTotal"),
                    ReadDecimal(reader, "GiftTotal"),
                    ReadDecimal(reader, "FuturesTotal"),
                    ReadDecimal(reader, "ExpenseTotal"));
            }

            return dict;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static int ReadInt(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? 0
            : Convert.ToInt32(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static int? ReadNullableInt(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : Convert.ToInt32(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static decimal ReadDecimal(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? 0m
            : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static string ReadString(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? string.Empty
            : Convert.ToString(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private sealed record BranchTurnoverRaw(
        int BranchNo,
        string BranchName,
        string Region,
        int CustomerCount,
        decimal OverallTotal,
        decimal CashTotal,
        decimal CreditTotal,
        decimal GiftTotal,
        decimal FuturesTotal,
        decimal ExpenseTotal);
}
