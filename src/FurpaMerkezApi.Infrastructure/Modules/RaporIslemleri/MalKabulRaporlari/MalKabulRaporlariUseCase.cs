using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.MalKabulRaporlari;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.MalKabulRaporlari;

public sealed class MalKabulRaporlariUseCase(MikroDbContext mikroDbContext) : IMalKabulRaporlariUseCase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 1000;

    public async Task<IReadOnlyCollection<LazyBranchReceivingItemDto>> GetLazyBranchesAsync(
        LazyBranchesWarehouseReceivingRequest request,
        CancellationToken cancellationToken)
    {
        ValidateOptionalDateRange(request.StartDate, request.EndDate);

        DateTime? startDate = request.StartDate?.ToDateTime(TimeOnly.MinValue);
        DateTime? endDateExclusive = request.EndDate?.ToDateTime(TimeOnly.MinValue).AddDays(1);

        const string sql = """
            SELECT
                COALESCE(CONVERT(varchar(50), w.dep_bolge_kodu), '') AS Region,
                w.dep_no AS WarehouseNo,
                COALESCE(w.dep_adi, 'Şube ' + CONVERT(varchar(20), w.dep_no)) AS WarehouseName,
                COUNT(DISTINCT sth.sth_evrakno_seri + '-' + CONVERT(varchar(20), sth.sth_evrakno_sira)) AS NotAcceptedCount,
                MIN(sth.sth_tarih) AS OldestShipmentDate,
                MAX(sth.sth_tarih) AS LatestShipmentDate,
                SUM(COALESCE(sth.sth_miktar, 0)) AS TotalQuantity
            FROM dbo.STOK_HAREKETLERI AS sth WITH (NOLOCK)
            INNER JOIN dbo.DEPOLAR AS w WITH (NOLOCK)
                ON sth.sth_nakliyedeposu = w.dep_no
            WHERE sth.sth_evraktip = 17
              AND sth.sth_nakliyedurumu = 0
              AND COALESCE(sth.sth_iptal, 0) = 0
              AND sth.sth_belge_no LIKE 'FRM%'
              AND (@startDate IS NULL OR sth.sth_tarih >= @startDate)
              AND (@endDateExclusive IS NULL OR sth.sth_tarih < @endDateExclusive)
              AND (@warehouseNo IS NULL OR w.dep_no = @warehouseNo)
              AND (@region IS NULL OR CONVERT(varchar(50), w.dep_bolge_kodu) = @region)
            GROUP BY
                w.dep_bolge_kodu,
                w.dep_no,
                w.dep_adi
            ORDER BY
                NotAcceptedCount DESC,
                WarehouseName;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@region", string.IsNullOrWhiteSpace(request.Region) ? null : request.Region);
            },
            reader => new LazyBranchReceivingItemDto(
                ReadString(reader, "Region"),
                ReadInt(reader, "WarehouseNo"),
                ReadString(reader, "WarehouseName"),
                ReadInt(reader, "NotAcceptedCount"),
                ReadNullableDateTime(reader, "OldestShipmentDate"),
                ReadNullableDateTime(reader, "LatestShipmentDate"),
                Round(ReadDouble(reader, "TotalQuantity"))),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<LazyBranchDocumentDetailDto>> GetLazyBranchDocumentsAsync(
        LazyBranchesWarehouseReceivingRequest request,
        CancellationToken cancellationToken)
    {
        ValidateOptionalDateRange(request.StartDate, request.EndDate);

        DateTime? startDate = request.StartDate?.ToDateTime(TimeOnly.MinValue);
        DateTime? endDateExclusive = request.EndDate?.ToDateTime(TimeOnly.MinValue).AddDays(1);

        const string sql = """
            SELECT
                COALESCE(CONVERT(varchar(50), w.dep_bolge_kodu), '') AS Region,
                w.dep_no AS WarehouseNo,
                COALESCE(w.dep_adi, 'Şube ' + CONVERT(varchar(20), w.dep_no)) AS WarehouseName,
                sth.sth_evrakno_seri AS DocumentSerie,
                sth.sth_evrakno_sira AS DocumentOrderNo,
                COALESCE(sth.sth_belge_no, '') AS DocumentNo,
                sth.sth_tarih AS DocumentDate,
                COUNT(*) AS LineCount,
                SUM(COALESCE(sth.sth_miktar, 0)) AS TotalQuantity
            FROM dbo.STOK_HAREKETLERI AS sth WITH (NOLOCK)
            INNER JOIN dbo.DEPOLAR AS w WITH (NOLOCK)
                ON sth.sth_nakliyedeposu = w.dep_no
            WHERE sth.sth_evraktip = 17
              AND sth.sth_nakliyedurumu = 0
              AND COALESCE(sth.sth_iptal, 0) = 0
              AND sth.sth_belge_no LIKE 'FRM%'
              AND (@startDate IS NULL OR sth.sth_tarih >= @startDate)
              AND (@endDateExclusive IS NULL OR sth.sth_tarih < @endDateExclusive)
              AND (@warehouseNo IS NULL OR w.dep_no = @warehouseNo)
              AND (@region IS NULL OR CONVERT(varchar(50), w.dep_bolge_kodu) = @region)
            GROUP BY
                w.dep_bolge_kodu,
                w.dep_no,
                w.dep_adi,
                sth.sth_evrakno_seri,
                sth.sth_evrakno_sira,
                sth.sth_belge_no,
                sth.sth_tarih
            ORDER BY
                sth.sth_tarih,
                w.dep_adi,
                sth.sth_evrakno_seri,
                sth.sth_evrakno_sira;
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@region", string.IsNullOrWhiteSpace(request.Region) ? null : request.Region);
            },
            reader => new LazyBranchDocumentDetailDto(
                ReadString(reader, "Region"),
                ReadInt(reader, "WarehouseNo"),
                ReadString(reader, "WarehouseName"),
                ReadString(reader, "DocumentSerie"),
                ReadInt(reader, "DocumentOrderNo"),
                ReadString(reader, "DocumentNo"),
                ReadDateTime(reader, "DocumentDate"),
                ReadInt(reader, "LineCount"),
                Round(ReadDouble(reader, "TotalQuantity"))),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<CompanyDocumentByCustomerItemDto>> GetCompanyDocumentByCustomerAsync(
        CompanyDocumentByCustomerRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDateRange(request.StartDate, request.EndDate);

        var startDate = request.StartDate.ToDateTime(TimeOnly.MinValue);
        var endDateExclusive = request.EndDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
        var take = NormalizeTake(request.Take);

        const string sql = """
            WITH CustomerTotals AS
            (
                SELECT
                    sth.sth_cari_kodu AS CustomerCode,
                    COUNT(DISTINCT sth.sth_evrakno_seri + '-' + CONVERT(varchar(20), sth.sth_evrakno_sira)) AS DocumentCount,
                    SUM(COALESCE(sth.sth_tutar, 0)) AS TotalAmount,
                    SUM(COALESCE(sth.sth_vergi, 0)) AS TotalTaxAmount,
                    SUM(COALESCE(sth.sth_iskonto1, 0) + COALESCE(sth.sth_iskonto2, 0) + COALESCE(sth.sth_iskonto3, 0)
                      + COALESCE(sth.sth_iskonto4, 0) + COALESCE(sth.sth_iskonto5, 0) + COALESCE(sth.sth_iskonto6, 0)) AS TotalDiscountAmount,
                    SUM((COALESCE(sth.sth_tutar, 0) + COALESCE(sth.sth_vergi, 0))
                      - (COALESCE(sth.sth_iskonto1, 0) + COALESCE(sth.sth_iskonto2, 0) + COALESCE(sth.sth_iskonto3, 0)
                       + COALESCE(sth.sth_iskonto4, 0) + COALESCE(sth.sth_iskonto5, 0) + COALESCE(sth.sth_iskonto6, 0))) AS NetAmount
                FROM dbo.STOK_HAREKETLERI AS sth WITH (NOLOCK)
                WHERE sth.sth_tip = 0
                  AND sth.sth_evraktip = 13
                  AND sth.sth_normal_iade = 0
                  AND (sth.sth_iptal = 0 OR sth.sth_iptal IS NULL)
                  AND sth.sth_giris_depo_no > 100
                  AND sth.sth_tarih >= @startDate
                  AND sth.sth_tarih < @endDateExclusive
                  AND (@warehouseNo IS NULL OR sth.sth_giris_depo_no = @warehouseNo)
                GROUP BY sth.sth_cari_kodu
            )
            SELECT TOP (@take)
                totals.CustomerCode,
                COALESCE(c.cari_unvan1, '') AS CustomerName,
                COALESCE(c.cari_unvan2, '') AS CustomerTitle,
                totals.DocumentCount,
                totals.TotalAmount,
                totals.TotalTaxAmount,
                totals.TotalDiscountAmount,
                totals.NetAmount
            FROM CustomerTotals AS totals
            INNER JOIN dbo.CARI_HESAPLAR AS c WITH (NOLOCK)
                ON totals.CustomerCode = c.cari_kod
            ORDER BY
                totals.NetAmount DESC,
                CustomerName
            OPTION (RECOMPILE);
            """;

        return await ExecuteReaderAsync(
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDateExclusive);
                AddParameter(command, "@warehouseNo", request.WarehouseNo);
                AddParameter(command, "@take", take);
            },
            reader => new CompanyDocumentByCustomerItemDto(
                ReadString(reader, "CustomerCode"),
                ReadString(reader, "CustomerName"),
                ReadString(reader, "CustomerTitle"),
                ReadInt(reader, "DocumentCount"),
                Round(ReadDouble(reader, "TotalAmount")),
                Round(ReadDouble(reader, "TotalTaxAmount")),
                Round(ReadDouble(reader, "TotalDiscountAmount")),
                Round(ReadDouble(reader, "NetAmount"))),
            cancellationToken);
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
            command.CommandTimeout = 120;
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

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? null
            : Convert.ToDateTime(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture);

    private static string ReadString(DbDataReader reader, string columnName) =>
        reader.IsDBNull(reader.GetOrdinal(columnName))
            ? string.Empty
            : Convert.ToString(reader.GetValue(reader.GetOrdinal(columnName)), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static double Round(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static int NormalizeTake(int take) => take switch
    {
        <= 0 => DefaultTake,
        > MaxTake => MaxTake,
        _ => take
    };

    private static void ValidateOptionalDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate.HasValue && endDate.HasValue)
        {
            ValidateDateRange(startDate.Value, endDate.Value);
        }
    }

    private static void ValidateDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Bitis tarihi baslangic tarihinden once olamaz.", nameof(endDate));
        }
    }
}
