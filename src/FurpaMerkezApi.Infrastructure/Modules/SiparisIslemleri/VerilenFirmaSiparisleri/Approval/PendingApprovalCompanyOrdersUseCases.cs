using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Approval;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Approval;

public sealed class PendingApprovalCompanyOrdersUseCases(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext)
    : IListPendingApprovalCompanyOrdersUseCase, IApproveIssuedCompanyOrdersUseCase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 1000;

    public async Task<IReadOnlyCollection<PendingApprovalCompanyOrderItemDto>> ExecuteAsync(
        PendingApprovalCompanyOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var take = request.Take <= 0 ? DefaultTake : Math.Min(request.Take, MaxTake);
        DateTime? startDate = request.StartDate?.ToDateTime(TimeOnly.MinValue);
        DateTime? endDateExclusive = request.EndDate?.ToDateTime(TimeOnly.MinValue).AddDays(1);

        const string sql = """
            SELECT TOP (@take)
                ord.sip_evrakno_seri AS DocumentSerie,
                ord.sip_evrakno_sira AS DocumentOrderNo,
                ord.sip_tarih AS OrderDate,
                ord.sip_depono AS BranchNo,
                COALESCE(w.dep_adi, '') AS BranchName,
                ord.sip_musteri_kod AS CustomerCode,
                COALESCE(c.cari_unvan1, '') AS CustomerName,
                COALESCE(c.cari_unvan2, '') AS CustomerTitle,
                ord.sip_cagrilabilir_fl AS CanBeCalled,
                CASE
                    WHEN MIN(COALESCE(s.sto_kullanici_kodu, '')) = MAX(COALESCE(s.sto_kullanici_kodu, ''))
                        THEN MIN(COALESCE(s.sto_kullanici_kodu, ''))
                    ELSE 'KARMA'
                END AS PurchasingPersonCode,
                CASE
                    WHEN MIN(COALESCE(u.user_isim, '')) = MAX(COALESCE(u.user_isim, ''))
                        THEN MIN(COALESCE(u.user_isim, ''))
                    ELSE 'Karma satin alma sorumlusu'
                END AS PurchasingPersonName,
                COUNT(*) AS LineCount,
                SUM((COALESCE(ord.sip_tutar, 0) + COALESCE(ord.sip_vergi, 0))
                  - (COALESCE(ord.sip_iskonto1, 0) + COALESCE(ord.sip_iskonto2, 0) + COALESCE(ord.sip_iskonto3, 0)
                   + COALESCE(ord.sip_iskonto4, 0) + COALESCE(ord.sip_iskonto5, 0) + COALESCE(ord.sip_iskonto6, 0))) AS TotalAmount
            FROM dbo.SIPARISLER AS ord WITH (NOLOCK)
            INNER JOIN dbo.STOKLAR AS s WITH (NOLOCK) ON s.sto_kod = ord.sip_stok_kod
            LEFT JOIN dbo.DEPOLAR AS w WITH (NOLOCK) ON w.dep_no = ord.sip_depono
            LEFT JOIN dbo.CARI_HESAPLAR AS c WITH (NOLOCK) ON c.cari_kod = ord.sip_musteri_kod
            LEFT JOIN dbo.USER_DEFINITIONS AS u WITH (NOLOCK) ON u.user_kod = s.sto_kullanici_kodu
            WHERE ord.sip_tip = 0
              AND ord.sip_cagrilabilir_fl = 0
              AND COALESCE(ord.sip_kapat_fl, 0) = 0
              AND (@warehouseNo IS NULL OR ord.sip_depono = @warehouseNo)
              AND (@customerCode IS NULL OR ord.sip_musteri_kod = @customerCode)
              AND (
                  @userDefPerCode IS NULL
                  OR EXISTS (
                      SELECT 1
                      FROM dbo.SIPARISLER AS personOrder WITH (NOLOCK)
                      INNER JOIN dbo.STOKLAR AS personStock WITH (NOLOCK)
                          ON personStock.sto_kod = personOrder.sip_stok_kod
                      WHERE personOrder.sip_tip = ord.sip_tip
                        AND personOrder.sip_evrakno_seri = ord.sip_evrakno_seri
                        AND personOrder.sip_evrakno_sira = ord.sip_evrakno_sira
                        AND personOrder.sip_depono = ord.sip_depono
                        AND personStock.sto_kullanici_kodu = @userDefPerCode
                  )
              )
              AND (@startDate IS NULL OR ord.sip_tarih >= @startDate)
              AND (@endDateExclusive IS NULL OR ord.sip_tarih < @endDateExclusive)
            GROUP BY
                ord.sip_evrakno_seri,
                ord.sip_evrakno_sira,
                ord.sip_tarih,
                ord.sip_depono,
                w.dep_adi,
                ord.sip_musteri_kod,
                c.cari_unvan1,
                c.cari_unvan2,
                ord.sip_cagrilabilir_fl
            ORDER BY
                ord.sip_tarih DESC,
                ord.sip_evrakno_seri,
                ord.sip_evrakno_sira;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@take", take);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@customerCode", string.IsNullOrWhiteSpace(request.CustomerCode) ? null : request.CustomerCode);
                AddParameter(command, "@userDefPerCode", string.IsNullOrWhiteSpace(request.UserDefPerCode) ? null : request.UserDefPerCode);
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
            },
            reader => new PendingApprovalCompanyOrderItemDto(
                ReadString(reader, "DocumentSerie"),
                ReadInt(reader, "DocumentOrderNo"),
                ReadDateTime(reader, "OrderDate"),
                ReadInt(reader, "BranchNo"),
                ReadString(reader, "BranchName"),
                ReadString(reader, "CustomerCode"),
                ReadString(reader, "CustomerName"),
                ReadString(reader, "CustomerTitle"),
                Round(ReadDouble(reader, "TotalAmount")),
                ReadInt(reader, "LineCount"),
                ReadBool(reader, "CanBeCalled"),
                ReadNullableString(reader, "PurchasingPersonCode"),
                ReadNullableString(reader, "PurchasingPersonName")),
            cancellationToken);
    }

    public async Task<BatchApproveCompanyOrdersResultDto> ExecuteAsync(
        BatchApproveCompanyOrdersRequest request,
        CancellationToken cancellationToken)
    {
        var approvedKeys = new List<string>();
        var failedKeys = new List<string>();

        const string updateSql = """
            UPDATE dbo.SIPARISLER
            SET sip_cagrilabilir_fl = 1,
                sip_lastup_date = GETDATE()
            WHERE sip_tip = 0
              AND sip_evrakno_seri = @serie
              AND sip_evrakno_sira = @orderNo
              AND sip_depono = @warehouseNo
              AND sip_cagrilabilir_fl = 0;
            """;

        var orders = request.Orders
            .Select(order => order with { DocumentSerie = order.DocumentSerie.Trim() })
            .Distinct()
            .ToArray();

        // Mikro yazma islemleri her zaman yazma baglami uzerinden yapilir (Split profilde test veritabani).
        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var order in orders)
                {
                    var docKey = $"{order.DocumentSerie}-{order.DocumentOrderNo} (Depo {order.WarehouseNo})";
                    await using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = updateSql;
                    AddParameter(cmd, "@serie", order.DocumentSerie);
                    AddParameter(cmd, "@orderNo", order.DocumentOrderNo);
                    AddParameter(cmd, "@warehouseNo", order.WarehouseNo);

                    var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                    if (rowsAffected > 0)
                    {
                        approvedKeys.Add(docKey);
                    }
                    else
                    {
                        failedKeys.Add(docKey);
                    }
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            return new BatchApproveCompanyOrdersResultDto(
                orders.Length,
                approvedKeys.Count,
                approvedKeys,
                failedKeys);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        string sql,
        Action<DbCommand> configureParameters,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
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

    private static double ReadDouble(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? 0d
            : Convert.ToDouble(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static DateTime ReadDateTime(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? default
            : Convert.ToDateTime(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static bool ReadBool(DbDataReader reader, string columnName) =>
        !reader.IsDBNull(reader.GetOrdinal(columnName)) &&
        Convert.ToBoolean(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static string ReadString(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? string.Empty
            : Convert.ToString(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static string? ReadNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return null;
        var s = Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
