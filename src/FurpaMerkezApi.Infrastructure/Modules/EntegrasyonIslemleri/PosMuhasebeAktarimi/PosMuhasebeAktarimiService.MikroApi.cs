using System.Globalization;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;

public sealed partial class PosMuhasebeAktarimiService
{
    private const string AccountingSlipSavePath = "/Api/apiMethods/MuhasebeFisKaydetV2";

    private async Task<AccountingSlipIdentity> WriteAccountingSlipMikroApiAsync(
        AccountingSlipDocument document,
        CancellationToken cancellationToken)
    {
        if (document.Lines.Count == 0)
        {
            throw new InvalidOperationException("Accounting slip has no lines.");
        }

        var existing = await TryReadBackAccountingSlipAsync(document, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var payload = CreateAccountingSlipMikroApiPayload(document);
        var apiResult = await mikroApiClient.PostWithMikroPayloadAsync<JsonElement>(
            AccountingSlipSavePath,
            payload,
            cancellationToken);
        apiResult.EnsureSuccess();

        mikroWriteDbContext.ChangeTracker.Clear();
        var recovered = await TryReadBackAccountingSlipAsync(document, cancellationToken)
            ?? throw new InvalidOperationException(
                "Mikro API returned success, but the accounting slip could not be verified by its commercial link and line totals.");

        await mikroApiClient.MarkRecoveredAsync(
            apiResult,
            $"{recovered.SlipNo}/{recovered.JournalNo}",
            document.SourceGuid,
            cancellationToken: cancellationToken);
        return recovered;
    }

    private async Task<AccountingSlipIdentity?> TryReadBackAccountingSlipAsync(
        AccountingSlipDocument document,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                fis_sira_no AS SlipNo,
                fis_yevmiye_no AS JournalNo,
                LTRIM(RTRIM(ISNULL(fis_hesap_kod, ''))) AS AccountCode,
                ISNULL(fis_meblag0, 0) AS Amount
            FROM dbo.MUHASEBE_FISLERI WITH (NOLOCK)
            WHERE fis_tarih >= @date
              AND fis_tarih < @dateEnd
              AND fis_ticari_tip = @commercialType
              AND fis_ticari_evraktip = @documentType
              AND fis_tic_evrak_sira = @documentNumber
              AND
              (
                  (@hasSourceGuid = 1 AND fis_ticari_uid = @sourceGuid)
                  OR
                  (@hasSourceGuid = 0
                   AND ISNULL(fis_tic_evrak_seri, '') = @documentSeries
                   AND ISNULL(fis_tic_belgeno, '') = @externalDocumentNo)
              )
            ORDER BY fis_sira_no DESC, fis_satir_no;
            """;
        var rows = new List<AccountingSlipReadBackRow>();
        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;
        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 300;
            command.Transaction = mikroWriteDbContext.Database.CurrentTransaction?.GetDbTransaction();
            AddParameter(command, "@date", document.DocumentDate.Date);
            AddParameter(command, "@dateEnd", document.DocumentDate.Date.AddDays(1));
            AddParameter(command, "@commercialType", CommercialAccountingType);
            AddParameter(command, "@documentType", CommercialDocumentType);
            AddParameter(command, "@documentNumber", document.DocumentNumber);
            AddParameter(command, "@hasSourceGuid", document.SourceGuid.HasValue ? 1 : 0);
            AddParameter(command, "@sourceGuid", document.SourceGuid ?? Guid.Empty);
            AddParameter(command, "@documentSeries", document.DocumentSeries);
            AddParameter(command, "@externalDocumentNo", document.ExternalDocumentNo);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new AccountingSlipReadBackRow(
                    reader.GetInt32(reader.GetOrdinal("SlipNo")),
                    reader.GetInt32(reader.GetOrdinal("JournalNo")),
                    reader.GetString(reader.GetOrdinal("AccountCode")),
                    Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("Amount")), CultureInfo.InvariantCulture)));
            }
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }

        if (rows.Count != document.Lines.Count)
        {
            return null;
        }

        var expected = document.Lines
            .OrderBy(line => line.AccountCode, StringComparer.Ordinal)
            .ThenBy(line => line.Amount)
            .ToArray();
        var actual = rows
            .OrderBy(row => row.AccountCode, StringComparer.Ordinal)
            .ThenBy(row => row.Amount)
            .ToArray();
        var matches = expected.Zip(actual).All(pair =>
            string.Equals(pair.First.AccountCode, pair.Second.AccountCode, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(pair.First.Amount - pair.Second.Amount) < 0.01m);
        if (!matches)
        {
            return null;
        }

        return new AccountingSlipIdentity(
            rows[0].SlipNo,
            rows[0].JournalNo);
    }

    private static object CreateAccountingSlipMikroApiPayload(AccountingSlipDocument document) =>
        new
        {
            evraklar = new[]
            {
                new
                {
                    satirlar = document.Lines.Select(line => new
                    {
                        fis_firmano = 0,
                        fis_subeno = 0,
                        fis_tarih = FormatAccountingDate(document.DocumentDate),
                        fis_tur = AccountingLineType,
                        fis_hesap_kod = line.AccountCode,
                        fis_aciklama1 = document.Description,
                        fis_meblag0 = line.Amount,
                        fis_sorumluluk_kodu = document.ResponsibilityCode,
                        fis_ticari_tip = CommercialAccountingType,
                        fis_ticari_uid = document.SourceGuid ?? Guid.Empty,
                        fis_kurfarkifl = 0,
                        fis_ticari_evraktip = CommercialDocumentType,
                        fis_tic_evrak_seri = document.DocumentSeries,
                        fis_tic_evrak_sira = document.DocumentNumber,
                        fis_tic_belgeno = document.ExternalDocumentNo,
                        fis_tic_belgetarihi = FormatAccountingDate(document.DocumentDate),
                        fis_katagori = AccountingCategory,
                        fis_fmahsup_tipi = document.OffsetType,
                        user_tablo = Array.Empty<object>()
                    }).ToArray(),
                    fis_detay = new[]
                    {
                        new
                        {
                            mfd_ticari_tip = CommercialAccountingType,
                            mfd_evraktip = CommercialDocumentType,
                            mfd_evrak_seri = document.DocumentSeries,
                            mfd_evrak_sira = document.DocumentNumber,
                            mfd_cariunvan = document.Customer.Name,
                            mfd_carivergidaireadi = document.Customer.TaxOffice,
                            mfd_carivergidaireno = document.Customer.TaxNo,
                            mfd_belgetarihi = FormatAccountingDate(document.DocumentDate),
                            mfd_tutarnereden = string.IsNullOrWhiteSpace(document.Customer.Code) ? 0 : 5,
                            mfd_caritipi = string.IsNullOrWhiteSpace(document.Customer.Code) ? 0 : 2,
                            mfd_carikodu = document.Customer.Code,
                            mfd_carimuhkodu = document.Customer.AccountingCode,
                            mfd_belgeno = document.ExternalDocumentNo,
                            mfd_kdvid = 0,
                            mfd_kdvtutar = document.TaxAmount,
                            mfd_caritutar = document.TotalAmount,
                            mfd_aciklama = document.Description,
                            mfd_kisaevraktipi = document.ShortDocumentType,
                            mfd_satistipi = document.SalesType,
                            mfd_alistipi = document.PurchaseType,
                            mfd_tahtedtipi = 0,
                            mfd_evraktur = 0,
                            mfd_e_belgemi = false
                        }
                    }
                }
            }
        };

    private static string FormatAccountingDate(DateTime value) =>
        value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private sealed record AccountingSlipReadBackRow(
        int SlipNo,
        int JournalNo,
        string AccountCode,
        decimal Amount);
}
