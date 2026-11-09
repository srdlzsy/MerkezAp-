using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.KasaRaporlari;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.KasaRaporlari;

public sealed class KasaRaporlariUseCase(
    FurpaDbContext furpaDbContext,
    MikroDbContext mikroDbContext,
    ILogger<KasaRaporlariUseCase> logger) : IKasaRaporlariUseCase
{
    public async Task<IReadOnlyCollection<ExpenseSlipZoneSummaryDto>> GetExpenseSlipHierarchyReportAsync(
        ExpenseSlipReportRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var startDate = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var endDateExclusive = request.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
        var warehouseNoStr = request.WarehouseNo?.ToString(CultureInfo.InvariantCulture);

        const string sql = """
            SELECT
                p.Sube AS BranchNo,
                p.KasaNo,
                CONVERT(varchar(50), p.KullaniciKodu) AS CashierCode,
                COALESCE(k.CashierName, CONVERT(varchar(50), p.KullaniciKodu)) AS CashierName,
                COUNT(*) AS SlipCount,
                SUM(COALESCE(p.Toplam, 0) + COALESCE(p.ToplamKdv, 0)) AS TotalExpenseAmount
            FROM dbo.PosFaturas AS p WITH (NOLOCK)
            LEFT JOIN dbo.Cashiers AS k WITH (NOLOCK)
                ON CONVERT(varchar(50), k.CashierCode) = LTRIM(RTRIM(p.KullaniciKodu))
            WHERE p.BelgeTuru = '4'
              AND p.Tarih >= @startDate
              AND p.Tarih < @endDateExclusive
              AND (@warehouseNoStr IS NULL OR p.Sube = @warehouseNoStr)
              AND (@cashRegisterNo IS NULL OR p.KasaNo = @cashRegisterNo)
              AND (@cashierCode IS NULL OR CONVERT(varchar(50), p.KullaniciKodu) = @cashierCode)
            GROUP BY
                p.Sube,
                p.KasaNo,
                p.KullaniciKodu,
                k.CashierName;
            """;

        var warehouses = await FetchWarehousesAsync(cancellationToken);

        var rawRows = await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNoStr", warehouseNoStr);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
                AddParameter(command, "@cashierCode", string.IsNullOrWhiteSpace(request.CashierCode) ? null : request.CashierCode);
            },
            reader =>
            {
                var branchNo = ReadIntFlexible(reader, "BranchNo");
                var branchName = warehouses.TryGetValue(branchNo, out var w) && !string.IsNullOrWhiteSpace(w.Name)
                    ? w.Name
                    : $"Şube {branchNo}";
                var zoneCode = warehouses.TryGetValue(branchNo, out var w2) && !string.IsNullOrWhiteSpace(w2.Region)
                    ? w2.Region
                    : "0";
                var zoneName = zoneCode == "0" ? "Diğer Bölge" : $"{zoneCode}. Bölge";

                return new ExpenseSlipRawRow(
                    zoneCode,
                    zoneName,
                    branchNo,
                    branchName,
                    ReadInt(reader, "KasaNo"),
                    ReadString(reader, "CashierCode"),
                    ReadString(reader, "CashierName"),
                    ReadInt(reader, "SlipCount"),
                    ReadDecimal(reader, "TotalExpenseAmount"));
            },
            cancellationToken);

        var zones = rawRows
            .GroupBy(r => new { r.ZoneCode, r.ZoneName })
            .Select(zg =>
            {
                var branches = zg
                    .GroupBy(r => new { r.BranchNo, r.BranchName, r.ZoneCode })
                    .Select(bg =>
                    {
                        var cashes = bg
                            .GroupBy(r => r.KasaNo)
                            .Select(cg =>
                            {
                                var cashiers = cg
                                    .Select(r => new ExpenseSlipCashierSummaryDto(
                                        r.CashierCode,
                                        r.CashierName,
                                        r.TotalExpenseAmount,
                                        r.SlipCount))
                                    .OrderByDescending(c => c.TotalExpenseAmount)
                                    .ToArray();

                                return new ExpenseSlipCashRegisterSummaryDto(
                                    cg.Key,
                                    cg.Sum(c => c.TotalExpenseAmount),
                                    cg.Sum(c => c.SlipCount),
                                    cashiers);
                            })
                            .OrderByDescending(c => c.TotalExpenseAmount)
                            .ToArray();

                        return new ExpenseSlipBranchSummaryDto(
                            bg.Key.BranchNo,
                            bg.Key.BranchName,
                            bg.Key.ZoneCode,
                            bg.Sum(c => c.TotalExpenseAmount),
                            bg.Sum(c => c.SlipCount),
                            cashes);
                    })
                    .OrderByDescending(b => b.TotalExpenseAmount)
                    .ToArray();

                return new ExpenseSlipZoneSummaryDto(
                    zg.Key.ZoneCode,
                    zg.Key.ZoneName,
                    zg.Sum(b => b.TotalExpenseAmount),
                    zg.Sum(b => b.SlipCount),
                    branches);
            })
            .OrderByDescending(z => z.TotalExpenseAmount)
            .ToArray();

        return zones;
    }

    public async Task<IReadOnlyCollection<ExpenseSlipDetailItemDto>> GetExpenseSlipDetailsAsync(
        ExpenseSlipReportRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var startDate = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var endDateExclusive = request.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
        var warehouseNoStr = request.WarehouseNo?.ToString(CultureInfo.InvariantCulture);

        const string sql = """
            SELECT
                p.Sube AS BranchNo,
                p.KasaNo,
                CONVERT(varchar(50), p.KullaniciKodu) AS CashierCode,
                COALESCE(k.CashierName, CONVERT(varchar(50), p.KullaniciKodu)) AS CashierName,
                p.Tarih AS [Date],
                COALESCE(CONVERT(varchar(20), p.Saat), '') AS [Time],
                COALESCE(p.FisNo, 0) AS SlipNo,
                p.FaturaGuid AS DocumentGuid,
                (COALESCE(p.Toplam, 0) + COALESCE(p.ToplamKdv, 0)) AS Amount,
                COALESCE(p.KasaMaliBellekKodu, '') AS Description
            FROM dbo.PosFaturas AS p WITH (NOLOCK)
            LEFT JOIN dbo.Cashiers AS k WITH (NOLOCK)
                ON CONVERT(varchar(50), k.CashierCode) = LTRIM(RTRIM(p.KullaniciKodu))
            WHERE p.BelgeTuru = '4'
              AND p.Tarih >= @startDate
              AND p.Tarih < @endDateExclusive
              AND (@warehouseNoStr IS NULL OR p.Sube = @warehouseNoStr)
              AND (@cashRegisterNo IS NULL OR p.KasaNo = @cashRegisterNo)
              AND (@cashierCode IS NULL OR CONVERT(varchar(50), p.KullaniciKodu) = @cashierCode)
            ORDER BY
                p.Tarih DESC,
                p.Saat DESC,
                p.Sube,
                p.KasaNo;
            """;

        var warehouses = await FetchWarehousesAsync(cancellationToken);

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNoStr", warehouseNoStr);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
                AddParameter(command, "@cashierCode", string.IsNullOrWhiteSpace(request.CashierCode) ? null : request.CashierCode);
            },
            reader =>
            {
                var branchNo = ReadIntFlexible(reader, "BranchNo");
                var branchName = warehouses.TryGetValue(branchNo, out var w) && !string.IsNullOrWhiteSpace(w.Name)
                    ? w.Name
                    : $"Şube {branchNo}";

                return new ExpenseSlipDetailItemDto(
                    branchNo,
                    branchName,
                    ReadInt(reader, "KasaNo"),
                    ReadString(reader, "CashierCode"),
                    ReadString(reader, "CashierName"),
                    ReadDateTime(reader, "Date"),
                    ReadString(reader, "Time"),
                    ReadInt(reader, "SlipNo"),
                    ReadGuid(reader, "DocumentGuid"),
                    ReadDecimal(reader, "Amount"),
                    ReadString(reader, "Description"));
            },
            cancellationToken);
    }

    private async Task<Dictionary<int, (string Name, string Region)>> FetchWarehousesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                dep_no,
                COALESCE(dep_adi, '') AS dep_adi,
                COALESCE(CONVERT(varchar(50), dep_bolge_kodu), '') AS dep_bolge_kodu
            FROM dbo.DEPOLAR WITH (NOLOCK);
            """;

        var dict = new Dictionary<int, (string Name, string Region)>();
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
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var no = ReadInt(reader, "dep_no");
                var name = ReadString(reader, "dep_adi");
                var region = ReadString(reader, "dep_bolge_kodu");
                dict[no] = (name, region);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Kasa raporu icin Mikro depo adlari okunamadi; sube numaralari kullanilacak.");
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return dict;
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        string sql,
        Action<DbCommand> configureParameters,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var connection = furpaDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            configureParameters(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<T>();

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(map(reader));
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

    private static int ReadIntFlexible(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return 0;
        var val = reader.GetValue(ordinal);
        return int.TryParse(Convert.ToString(val, CultureInfo.InvariantCulture), out var parsed) ? parsed : 0;
    }

    private static decimal ReadDecimal(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? 0m
            : Convert.ToDecimal(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static DateTime ReadDateTime(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? default
            : Convert.ToDateTime(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static Guid ReadGuid(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return Guid.Empty;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            Guid guid => guid,
            _ when Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed) => parsed,
            _ => Guid.Empty
        };
    }

    private static string ReadString(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? string.Empty
            : Convert.ToString(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static void ValidateDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Bitis tarihi baslangic tarihinden once olamaz.", nameof(endDate));
        }
    }

    private sealed record ExpenseSlipRawRow(
        string ZoneCode,
        string ZoneName,
        int BranchNo,
        string BranchName,
        int KasaNo,
        string CashierCode,
        string CashierName,
        int SlipCount,
        decimal TotalExpenseAmount);
}
