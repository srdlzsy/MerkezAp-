using System.Data;
using System.Data.Common;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.ProductCases;

public sealed class SqlGreenGrocerLabelStatsReader(FurpaDbContext furpaDbContext)
    : IGreenGrocerLabelStatsReader
{
    public async Task<IReadOnlyList<GreenGrocerLabelObservation>> ReadObservationsAsync(
        string stockCode,
        DateTime orderDate,
        int windowDays,
        CancellationToken cancellationToken)
    {
        var effectiveWindowDays = Math.Clamp(windowDays, 7, 365);
        var connection = furpaDbContext.Database.GetDbConnection();
        var wasClosed = connection.State == ConnectionState.Closed;

        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SET NOCOUNT ON;

                DECLARE @EndDateExclusive date = DATEADD(day, 1, @OrderDate);
                DECLARE @StartDate date = DATEADD(day, -1 * @WindowDays, @EndDateExclusive);

                SELECT
                    Olusturma_Tarihi AS CreatedAt,
                    CAST(ISNULL([Alınan_Net_Miktar], 0) AS float) AS NetKg,
                    CAST(ISNULL(Kasa_Sayisi, 0) AS float) AS CaseCount
                FROM dbo.Manav_Depo_Mal_Kabul_Etiket WITH (NOLOCK)
                WHERE Stok_Kod = @StockCode
                  AND Olusturma_Tarihi >= @StartDate
                  AND Olusturma_Tarihi < @EndDateExclusive
                  AND ISNULL(Kasa_Sayisi, 0) > 0
                  AND ISNULL([Alınan_Net_Miktar], 0) > 0
                ORDER BY Olusturma_Tarihi DESC;
                """;
            AddParameter(command, "@StockCode", stockCode);
            AddParameter(command, "@OrderDate", orderDate.Date);
            AddParameter(command, "@WindowDays", effectiveWindowDays);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var observations = new List<GreenGrocerLabelObservation>();

            var createdAtOrdinal = reader.GetOrdinal("CreatedAt");
            var netKgOrdinal = reader.GetOrdinal("NetKg");
            var caseCountOrdinal = reader.GetOrdinal("CaseCount");

            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(createdAtOrdinal) ||
                    reader.IsDBNull(netKgOrdinal) ||
                    reader.IsDBNull(caseCountOrdinal))
                {
                    continue;
                }

                var netKg = Convert.ToDouble(reader.GetValue(netKgOrdinal));
                var caseCount = Convert.ToDouble(reader.GetValue(caseCountOrdinal));
                if (netKg <= 0d || caseCount <= 0d)
                {
                    continue;
                }

                observations.Add(new GreenGrocerLabelObservation(
                    reader.GetDateTime(createdAtOrdinal),
                    netKg,
                    caseCount));
            }

            return observations;
        }
        finally
        {
            if (wasClosed)
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
}
