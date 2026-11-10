using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using FurpaMerkezApi.Application.Common.Errors;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaHareketAktarimi;

public sealed class KasaHareketAktarimiService(
    FurpaDbContext furpaDbContext,
    MikroDbContext mikroDbContext,
    IConfiguration configuration,
    IDocumentFlowService documentFlowService)
    : IKasaHareketAktarimiService
{
    private const string DefaultFileRootPath = @"\\10.0.0.55\kasa\";
    private const string NormalPrefix = "HR";
    private const string CancelPrefix = "IP";
    private const int PosRetailStockDocumentType = 4;
    private const int PosExpenseStockDocumentType = 16;
    private const int PosExpenseCustomerDocumentType = 61;
    private const int PosSalesCustomerDocumentType = 63;
    private const string StockMovementWriteProcedureName = "StokHareketYaz";
    private const string MikroMutationLockResource = "FurpaMerkezApi:KasaHareketAktarimi:MikroMutation";
    private const int MikroMutationLockTimeoutMilliseconds = 30000;
    private const int DefaultProcedureCommandTimeoutSeconds = 300;
    private const int StockMovementWriteCommandTimeoutSeconds = 600;

    private readonly Dictionary<string, string?> barcodeCache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyCollection<KasaHareketBranchDto>> ListBranchesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(item => item.dep_no.HasValue && item.dep_no.Value > 100)
            .OrderBy(item => item.dep_no)
            .Select(item => new
            {
                BranchNo = item.dep_no ?? 0,
                BranchName = item.dep_adi ?? string.Empty,
                Region = item.dep_bolge_kodu ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(item => new KasaHareketBranchDto(item.BranchNo, item.BranchName, item.Region))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<KasaHareketCashRegisterDto>> ListCashRegistersAsync(
        int branchNo,
        CancellationToken cancellationToken)
    {
        if (branchNo <= 0)
        {
            throw new ArgumentException("Branch no must be greater than zero.", nameof(branchNo));
        }

        var cashRegisters = await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == branchNo)
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new
            {
                item.BranchNo,
                item.CashRegisterNo,
                item.CashRegisterType
            })
            .ToArrayAsync(cancellationToken);

        return cashRegisters
            .Select(item =>
            {
                var option = SettingsTypeCatalog.ResolveCashTypeOption(item.CashRegisterType);
                return new KasaHareketCashRegisterDto(
                    item.BranchNo,
                    item.CashRegisterNo,
                    item.CashRegisterType,
                    option.Name,
                    option.Description);
            })
            .ToArray();
    }

    public Task<KasaHareketImportResultDto> ImportMovementsAsync(
        KasaHareketImportRequest request,
        CancellationToken cancellationToken) =>
        ExecuteCombinedImportAsync(request, "normal", cancellationToken);

    public Task<KasaHareketImportResultDto> ImportCancelMovementsAsync(
        KasaHareketImportRequest request,
        CancellationToken cancellationToken) =>
        ExecuteImportAsync(request, MovementImportKind.Cancel, cancellationToken);

    public async Task<KasaHareketImportResultDto> RunScheduledImportAsync(
        KasaHareketScheduledImportRequest request,
        CancellationToken cancellationToken)
    {
        var addDay = request.AddDay ?? GetConfiguredScheduledAddDay();
        var importDate = (request.Date ?? DateTime.Today.AddDays(addDay)).Date;
        var importRequest = new KasaHareketImportRequest(
            importDate,
            importDate,
            null,
            null,
            request.FileRootPath,
            request.SkipExisting,
            request.DryRun);

        return await ExecuteCombinedImportAsync(importRequest, "scheduled", cancellationToken);
    }

    private async Task<KasaHareketImportResultDto> ExecuteCombinedImportAsync(
        KasaHareketImportRequest request,
        string mode,
        CancellationToken cancellationToken)
    {
        var normalResult = await ExecuteImportAsync(
            request,
            MovementImportKind.Normal,
            cancellationToken);
        var cancelResult = await ExecuteImportAsync(
            request,
            MovementImportKind.Cancel,
            cancellationToken);

        return MergeImportResults(mode, request.StartDate, request.DryRun, normalResult, cancelResult);
    }

    internal static KasaHareketImportResultDto MergeImportResults(
        string mode,
        DateTime importDate,
        bool dryRun,
        KasaHareketImportResultDto normalResult,
        KasaHareketImportResultDto cancelResult) =>
        new(
            CreateRunId(mode, importDate),
            mode,
            ResolveStatus(dryRun, normalResult.Errors.Count + cancelResult.Errors.Count),
            normalResult.ProcessedFiles + cancelResult.ProcessedFiles,
            normalResult.ProcessedInvoices + cancelResult.ProcessedInvoices,
            normalResult.SkippedExistingInvoices + cancelResult.SkippedExistingInvoices,
            normalResult.InsertedLines + cancelResult.InsertedLines,
            normalResult.InsertedPayments + cancelResult.InsertedPayments,
            normalResult.InsertedPromotions + cancelResult.InsertedPromotions,
            normalResult.Warnings.Concat(cancelResult.Warnings).ToArray(),
            normalResult.Errors.Concat(cancelResult.Errors).ToArray());

    public async Task<KasaHareketProcedureResultDto> DeleteStagingMovementsAsync(
        KasaHareketDeleteStagingRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));

        var message = await ExecuteProcedureAsync(
            "HareketSil",
            cancellationToken,
            ("@Tarih", request.Date.Date),
            ("@Sube", request.BranchNo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            ("@KasaNo", request.CashRegisterNo ?? 0));

        return new KasaHareketProcedureResultDto(
            "HareketSil",
            message,
            request.Date.Date,
            request.BranchNo,
            request.CashRegisterNo);
    }

    public async Task<KasaHareketProcedureResultDto> TransferMovementsToMikroAsync(
        KasaHareketMikroTransferRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));

        await PreparePendingMikroTransferAsync(
            request.Date.Date,
            request.Date.Date,
            request.BranchNo,
            requireExclusiveBranch: false,
            cancellationToken: cancellationToken);

        var before = await GetMikroPosDocumentsAsync(
            request.Date.Date,
            request.Date.Date,
            request.BranchNo,
            cancellationToken);

        var message = await ExecuteProcedureAsync(
            "StokHareketYaz",
            cancellationToken,
            ("@Tarih", request.Date.Date),
            ("@Sube", request.BranchNo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));

        var after = await GetMikroPosDocumentsAsync(
            request.Date.Date,
            request.Date.Date,
            request.BranchNo,
            cancellationToken);
        await RecordMikroDocumentChangesAsync(
            FindDocumentDifference(after, before),
            DocumentFlowStep.DocumentCreated,
            "Kasa hareket aktarimi ile Mikro POS evragi olusturuldu.",
            cancellationToken);

        return new KasaHareketProcedureResultDto(
            "StokHareketYaz",
            message,
            request.Date.Date,
            request.BranchNo,
            null);
    }

    public async Task<KasaHareketProcedureResultDto> DeleteMovementsFromMikroAsync(
        KasaHareketMikroTransferRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));
        if (request.BranchNo is not > 0)
        {
            throw new ArgumentException(
                "Mikro hareket silme islemi icin sube numarasi zorunludur.",
                nameof(request.BranchNo));
        }

        var branchParam = request.BranchNo.Value.ToString(CultureInfo.InvariantCulture);

        var requestedEndDate = request.EndDate?.Date ?? request.Date.Date;
        var before = await GetMikroPosDocumentsAsync(
            request.Date.Date,
            requestedEndDate,
            request.BranchNo,
            cancellationToken);

        if (request.EndDate.HasValue && request.EndDate.Value.Date != request.Date.Date)
        {
            var (startDate, endDate) = NormalizeDateRange(request.Date, request.EndDate.Value);
            var rangeMessage = await ExecuteProcedureAsync(
                "StokHareketSilTarihAralikli",
                cancellationToken,
                ("@Tarih1", startDate),
                ("@Tarih2", endDate),
                ("@Sube", branchParam));

            var after = await GetMikroPosDocumentsAsync(
                startDate,
                endDate,
                request.BranchNo,
                cancellationToken);
            await RecordMikroDocumentChangesAsync(
                FindDocumentDifference(before, after),
                DocumentFlowStep.DocumentDeleted,
                "Kasa hareket geri alma islemi ile Mikro POS evragi silindi.",
                cancellationToken);

            return new KasaHareketProcedureResultDto(
                "StokHareketSilTarihAralikli",
                rangeMessage,
                startDate,
                request.BranchNo,
                null);
        }

        var message = await ExecuteProcedureAsync(
            "StokHareketSil",
            cancellationToken,
            ("@Tarih", request.Date.Date),
            ("@Sube", branchParam));

        var remaining = await GetMikroPosDocumentsAsync(
            request.Date.Date,
            request.Date.Date,
            request.BranchNo,
            cancellationToken);
        await RecordMikroDocumentChangesAsync(
            FindDocumentDifference(before, remaining),
            DocumentFlowStep.DocumentDeleted,
            "Kasa hareket geri alma islemi ile Mikro POS evragi silindi.",
            cancellationToken);

        return new KasaHareketProcedureResultDto(
            "StokHareketSil",
            message,
            request.Date.Date,
            request.BranchNo,
            null);
    }

    public async Task<KasaHareketProcedureResultDto> TransferMovementRangeToMikroAsync(
        KasaHareketMikroTransferRangeRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDate) = NormalizeDateRange(request.StartDate, request.EndDate);

        await PreparePendingMikroTransferAsync(
            startDate,
            endDate,
            branchNo: null,
            requireExclusiveBranch: false,
            cancellationToken: cancellationToken);

        var before = await GetMikroPosDocumentsAsync(
            startDate,
            endDate,
            null,
            cancellationToken);

        foreach (var date in EnumerateDates(startDate, endDate))
        {
            await ExecuteProcedureAsync(
                "StokHareketYaz",
                cancellationToken,
                ("@Tarih", date),
                ("@Sube", string.Empty));
        }

        var message = $"{startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy} tarih araligindaki " +
                      "hareketler Mikro'ya islendi.";

        var after = await GetMikroPosDocumentsAsync(
            startDate,
            endDate,
            null,
            cancellationToken);
        await RecordMikroDocumentChangesAsync(
            FindDocumentDifference(after, before),
            DocumentFlowStep.DocumentCreated,
            "Tarih aralikli kasa hareket aktarimi ile Mikro POS evragi olusturuldu.",
            cancellationToken);

        return new KasaHareketProcedureResultDto(
            "StokHareketYaz",
            message,
            startDate,
            null,
            null);
    }

    public async Task<KasaHareketTransferStatusDto> GetTransferStatusAsync(
        KasaHareketTransferStatusRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDate) = NormalizeDateRange(request.StartDate, request.EndDate);
        if (request.BranchNo is <= 0)
        {
            throw new ArgumentException("Sube numarasi sifirdan buyuk olmalidir.", nameof(request.BranchNo));
        }

        var cashRegisterNos = NormalizeCashRegisterNos(request.CashRegisterNo, request.CashRegisterNos);
        var cashRegisterNosParameter = cashRegisterNos.Length == 0
            ? null
            : string.Join(',', cashRegisterNos);

        const string sql = """
            SELECT
                COUNT_BIG(1) AS TotalReceiptCount,
                SUM(CASE WHEN ISNULL(invoice.MikroAktarimDurumu, 0) = 1 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END)
                    AS TransferredReceiptCount,
                SUM(CASE WHEN ISNULL(invoice.MikroAktarimDurumu, 0) <> 1 THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END)
                    AS PendingReceiptCount,
                (
                    SELECT COUNT_BIG(1)
                    FROM dbo.PosFaturaIptals AS canceled WITH (NOLOCK)
                    WHERE canceled.Tarih >= @startDate
                      AND canceled.Tarih < @endDateExclusive
                      AND (@branchNo IS NULL OR TRY_CONVERT(int, canceled.Sube) = @branchNo)
                      AND
                      (
                          @cashRegisterNos IS NULL
                          OR EXISTS
                          (
                              SELECT 1
                              FROM STRING_SPLIT(@cashRegisterNos, ',') AS selectedCashRegister
                              WHERE TRY_CONVERT(int, selectedCashRegister.[value]) = TRY_CONVERT(int, canceled.KasaNo)
                          )
                      )
                ) AS CanceledReceiptCount
            FROM dbo.PosFaturas AS invoice WITH (NOLOCK)
            WHERE invoice.Tarih >= @startDate
              AND invoice.Tarih < @endDateExclusive
              AND (@branchNo IS NULL OR TRY_CONVERT(int, invoice.Sube) = @branchNo)
              AND
              (
                  @cashRegisterNos IS NULL
                  OR EXISTS
                  (
                      SELECT 1
                      FROM STRING_SPLIT(@cashRegisterNos, ',') AS selectedCashRegister
                      WHERE TRY_CONVERT(int, selectedCashRegister.[value]) = TRY_CONVERT(int, invoice.KasaNo)
                  )
              );
            """;

        var rows = await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDateExclusive", endDate.AddDays(1));
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNos", cashRegisterNosParameter);
            },
            reader => new
            {
                Total = checked((int)ReadInt64(reader, "TotalReceiptCount")),
                Transferred = checked((int)ReadInt64(reader, "TransferredReceiptCount")),
                Pending = checked((int)ReadInt64(reader, "PendingReceiptCount")),
                Canceled = checked((int)ReadInt64(reader, "CanceledReceiptCount"))
            },
            cancellationToken);

        var row = rows.FirstOrDefault();
        return new KasaHareketTransferStatusDto(
            startDate,
            endDate,
            request.BranchNo,
            cashRegisterNos.Length == 1 ? cashRegisterNos[0] : null,
            row?.Total ?? 0,
            row?.Transferred ?? 0,
            row?.Pending ?? 0,
            row?.Canceled ?? 0,
            DateTime.UtcNow);
    }

    public async Task<IReadOnlyCollection<KasaHareketReportRowDto>> GetReportAsync(
        KasaHareketReportRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));

        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            WITH InvoiceTotals AS (
                SELECT
                    CAST(invoice.Tarih AS date) AS BusinessDate,
                    TRY_CONVERT(int, invoice.Sube) AS BranchNo,
                    TRY_CONVERT(int, invoice.KasaNo) AS CashRegisterNo,
                    SUM(CASE
                        WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN 0
                        ELSE ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                    END) AS NetAmount,
                    SUM(CASE
                        WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                        ELSE 0
                    END) AS Expense
                FROM dbo.PosFaturas AS invoice WITH (NOLOCK)
                WHERE invoice.Tarih >= @date
                  AND invoice.Tarih < @nextDate
                  AND (@branchNo IS NULL OR TRY_CONVERT(int, invoice.Sube) = @branchNo)
                  AND (@cashRegisterNo IS NULL OR TRY_CONVERT(int, invoice.KasaNo) = @cashRegisterNo)
                GROUP BY
                    CAST(invoice.Tarih AS date),
                    TRY_CONVERT(int, invoice.Sube),
                    TRY_CONVERT(int, invoice.KasaNo)
            ),
            CheckTotals AS (
                SELECT
                    CAST(payment.Tarih AS date) AS BusinessDate,
                    TRY_CONVERT(int, payment.Sube) AS BranchNo,
                    TRY_CONVERT(int, payment.KasaKodu) AS CashRegisterNo,
                    SUM(CASE WHEN ISNULL(payment.OdemeTipi, 0) = 4 THEN ISNULL(payment.Tutar, 0) ELSE 0 END) AS CheckAmount
                FROM dbo.PosFaturaOdemes AS payment WITH (NOLOCK)
                WHERE payment.Tarih >= @date
                  AND payment.Tarih < @nextDate
                  AND (@branchNo IS NULL OR TRY_CONVERT(int, payment.Sube) = @branchNo)
                  AND (@cashRegisterNo IS NULL OR TRY_CONVERT(int, payment.KasaKodu) = @cashRegisterNo)
                GROUP BY
                    CAST(payment.Tarih AS date),
                    TRY_CONVERT(int, payment.Sube),
                    TRY_CONVERT(int, payment.KasaKodu)
            )
            SELECT
                COALESCE(invoice.BusinessDate, checks.BusinessDate) AS BusinessDate,
                COALESCE(invoice.BranchNo, checks.BranchNo, 0) AS BranchNo,
                COALESCE(invoice.CashRegisterNo, checks.CashRegisterNo, 0) AS CashRegisterNo,
                COALESCE(invoice.NetAmount, 0) AS NetAmount,
                COALESCE(invoice.Expense, 0) AS Expense,
                COALESCE(checks.CheckAmount, 0) AS CheckAmount,
                COALESCE(invoice.NetAmount, 0) - COALESCE(invoice.Expense, 0) - COALESCE(checks.CheckAmount, 0) AS Difference
            FROM InvoiceTotals AS invoice
            FULL JOIN CheckTotals AS checks
                ON invoice.BusinessDate = checks.BusinessDate
               AND invoice.BranchNo = checks.BranchNo
               AND invoice.CashRegisterNo = checks.CashRegisterNo
            ORDER BY BranchNo, CashRegisterNo;
            """;

        var rows = await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
            },
            reader => new ReportSqlRow(
                ReadDateTime(reader, "BusinessDate"),
                ReadInt(reader, "BranchNo"),
                ReadInt(reader, "CashRegisterNo"),
                ReadDecimal(reader, "NetAmount"),
                ReadDecimal(reader, "Expense"),
                ReadDecimal(reader, "CheckAmount"),
                ReadDecimal(reader, "Difference")),
            cancellationToken);

        var branchNames = await GetBranchNamesAsync(
            rows.Select(row => row.BranchNo),
            cancellationToken);

        return rows
            .Select(row => new KasaHareketReportRowDto(
                row.Date,
                row.BranchNo,
                branchNames.TryGetValue(row.BranchNo, out var branchName) ? branchName : string.Empty,
                row.CashRegisterNo,
                row.NetAmount,
                row.Expense,
                row.CheckAmount,
                row.Difference))
            .ToArray();
    }

    public async Task<KasaHareketReportSummaryDto> GetReportSummaryAsync(
        KasaHareketReportRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await GetReportAsync(request, cancellationToken);

        return new KasaHareketReportSummaryDto(
            request.Date.Date,
            request.BranchNo,
            request.CashRegisterNo,
            rows.Count,
            Round(rows.Sum(row => row.NetAmount)),
            Round(rows.Sum(row => row.Expense)),
            Round(rows.Sum(row => row.CheckAmount)),
            Round(rows.Sum(row => row.Difference)));
    }

    public async Task<KasaHareketSalesComparisonDto> GetSalesComparisonAsync(
        KasaHareketSalesComparisonRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));
        if (request.BranchNo is <= 0)
        {
            throw new ArgumentException("Sube numarasi sifirdan buyuk olmalidir.", nameof(request.BranchNo));
        }

        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const decimal tolerance = 0.01m;

        const string mikroSql = """
            WITH StockTotals AS (
                SELECT
                    ISNULL(movement.sth_cikis_depo_no, 0) AS BranchNo,
                    SUM(CASE
                        WHEN movement.sth_evrakno_seri LIKE 'PS%'
                         AND movement.sth_evrakno_seri NOT LIKE 'PSF%'
                         AND movement.sth_tip = 1
                         AND movement.sth_cins = 1
                         AND movement.sth_evraktip = 4 THEN
                            ISNULL(movement.sth_tutar, 0)
                          - ISNULL(movement.sth_iskonto1, 0)
                          - ISNULL(movement.sth_iskonto2, 0)
                          - ISNULL(movement.sth_iskonto3, 0)
                          - ISNULL(movement.sth_iskonto4, 0)
                          - ISNULL(movement.sth_iskonto5, 0)
                          - ISNULL(movement.sth_iskonto6, 0)
                          + ISNULL(movement.sth_vergi, 0)
                        ELSE 0
                    END) AS RetailStockAmount,
                    SUM(CASE
                        WHEN movement.sth_evrakno_seri LIKE 'PS%'
                         AND movement.sth_evrakno_seri NOT LIKE 'PSF%'
                         AND movement.sth_tip = 1
                         AND movement.sth_cins = 1
                         AND movement.sth_evraktip = 1 THEN
                            ISNULL(movement.sth_tutar, 0)
                          - ISNULL(movement.sth_iskonto1, 0)
                          - ISNULL(movement.sth_iskonto2, 0)
                          - ISNULL(movement.sth_iskonto3, 0)
                          - ISNULL(movement.sth_iskonto4, 0)
                          - ISNULL(movement.sth_iskonto5, 0)
                          - ISNULL(movement.sth_iskonto6, 0)
                          + ISNULL(movement.sth_vergi, 0)
                        ELSE 0
                    END) AS InvoiceStockAmount,
                    SUM(CASE
                        WHEN movement.sth_evrakno_seri LIKE 'PS%'
                         AND movement.sth_evrakno_seri NOT LIKE 'PSF%'
                         AND movement.sth_tip = 0
                         AND movement.sth_cins = 17
                         AND movement.sth_evraktip = 16 THEN
                            -- StokHareketYaz gider tutarini KDV dahil yazar; sth_vergi tekrar eklenmez.
                            ISNULL(movement.sth_tutar, 0)
                          - ISNULL(movement.sth_iskonto1, 0)
                          - ISNULL(movement.sth_iskonto2, 0)
                          - ISNULL(movement.sth_iskonto3, 0)
                          - ISNULL(movement.sth_iskonto4, 0)
                          - ISNULL(movement.sth_iskonto5, 0)
                          - ISNULL(movement.sth_iskonto6, 0)
                        ELSE 0
                    END) AS ExpenseSlipStockAmount,
                    SUM(CASE
                        WHEN movement.sth_evrakno_seri LIKE 'PS%'
                         AND movement.sth_evrakno_seri NOT LIKE 'PSF%'
                         AND movement.sth_tip = 1
                         AND movement.sth_cins = 1
                         AND movement.sth_evraktip = 4 THEN
                            ISNULL(movement.sth_tutar, 0)
                          - ISNULL(movement.sth_iskonto1, 0)
                          - ISNULL(movement.sth_iskonto2, 0)
                          - ISNULL(movement.sth_iskonto3, 0)
                          - ISNULL(movement.sth_iskonto4, 0)
                          - ISNULL(movement.sth_iskonto5, 0)
                          - ISNULL(movement.sth_iskonto6, 0)
                          + ISNULL(movement.sth_vergi, 0)
                        ELSE 0
                    END) AS CurrentComparableStockAmount
                FROM dbo.STOK_HAREKETLERI AS movement WITH (READUNCOMMITTED)
                WHERE movement.sth_tarih = @date
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_evrakno_seri LIKE 'PS%'
                  AND movement.sth_evrakno_seri NOT LIKE 'PSF%'
                  AND (
                       (movement.sth_tip = 1 AND movement.sth_cins = 1 AND movement.sth_evraktip IN (1, 4))
                    OR (movement.sth_tip = 0 AND movement.sth_cins = 17 AND movement.sth_evraktip = 16)
                  )
                  AND (@branchNo IS NULL OR movement.sth_cikis_depo_no = @branchNo)
                GROUP BY movement.sth_cikis_depo_no
            ),
            CurrentTotals AS (
                SELECT
                    CONVERT(int, movement.cha_create_user) AS BranchNo,
                    SUM(ISNULL(movement.cha_meblag, 0)) AS CurrentAccountAmount
                FROM dbo.CARI_HESAP_HAREKETLERI AS movement WITH (READUNCOMMITTED)
                WHERE movement.cha_tarihi = @date
                  AND movement.cha_evrak_tip = 63
                  AND movement.cha_pos_hareketi IN (0, 1)
                  AND ISNULL(movement.cha_evrakno_seri, '') NOT LIKE 'PSF%'
                  AND (@branchNo IS NULL OR movement.cha_create_user = @branchNo)
                GROUP BY movement.cha_create_user
            ),
            ZReportTotals AS (
                SELECT
                    TRY_CONVERT(int, movement.cha_kod) AS BranchNo,
                    SUM(ISNULL(movement.cha_meblag, 0)) AS ZReportAmount
                FROM dbo.CARI_HESAP_HAREKETLERI AS movement WITH (READUNCOMMITTED)
                WHERE movement.cha_tarihi = @date
                  AND movement.cha_tip = 1
                  AND movement.cha_cinsi = 5
                  AND movement.cha_evrakno_seri = 'X'
                  AND NULLIF(LTRIM(RTRIM(ISNULL(movement.cha_special1, ''))), '') IS NULL
                  AND TRY_CONVERT(int, movement.cha_kod) IS NOT NULL
                  AND (@branchNo IS NULL OR TRY_CONVERT(int, movement.cha_kod) = @branchNo)
                GROUP BY TRY_CONVERT(int, movement.cha_kod)
            ),
            Branches AS (
                SELECT BranchNo FROM StockTotals
                UNION
                SELECT BranchNo FROM CurrentTotals
                UNION
                SELECT BranchNo FROM ZReportTotals
            )
            SELECT
                branch.BranchNo,
                ISNULL(stock.RetailStockAmount, 0) AS RetailStockAmount,
                ISNULL(stock.InvoiceStockAmount, 0) AS InvoiceStockAmount,
                ISNULL(stock.ExpenseSlipStockAmount, 0) AS ExpenseSlipStockAmount,
                ISNULL(stock.CurrentComparableStockAmount, 0) AS CurrentComparableStockAmount,
                ISNULL(currentTotal.CurrentAccountAmount, 0) AS CurrentAccountAmount,
                ISNULL(zReport.ZReportAmount, 0) AS ZReportAmount
            FROM Branches AS branch
            LEFT JOIN StockTotals AS stock ON stock.BranchNo = branch.BranchNo
            LEFT JOIN CurrentTotals AS currentTotal ON currentTotal.BranchNo = branch.BranchNo
            LEFT JOIN ZReportTotals AS zReport ON zReport.BranchNo = branch.BranchNo
            WHERE branch.BranchNo > 100
            ORDER BY branch.BranchNo
            OPTION (RECOMPILE);
            """;

        var mikroRows = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            mikroSql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@branchNo", request.BranchNo);
            },
            reader => new SalesComparisonMikroSqlRow(
                ReadInt(reader, "BranchNo"),
                ReadDecimal(reader, "RetailStockAmount"),
                ReadDecimal(reader, "InvoiceStockAmount"),
                ReadDecimal(reader, "ExpenseSlipStockAmount"),
                ReadDecimal(reader, "CurrentComparableStockAmount"),
                ReadDecimal(reader, "CurrentAccountAmount"),
                ReadDecimal(reader, "ZReportAmount")),
            cancellationToken);

        const string furpaSql = """
            SELECT
                TRY_CONVERT(int, line.Sube) AS BranchNo,
                SUM(CASE
                    WHEN TRY_CONVERT(int, invoice.BelgeTuru) = 1
                        THEN ISNULL(line.NetTutar, 0) + ISNULL(line.KdvTutari, 0)
                    ELSE 0
                END) AS RetailTransferredAmount,
                SUM(CASE
                    WHEN TRY_CONVERT(int, invoice.BelgeTuru) = 3
                        THEN ISNULL(line.NetTutar, 0) + ISNULL(line.KdvTutari, 0)
                    ELSE 0
                END) AS InvoiceTransferredAmount,
                SUM(CASE
                    WHEN TRY_CONVERT(int, invoice.BelgeTuru) = 4
                        THEN ISNULL(line.NetTutar, 0) + ISNULL(line.KdvTutari, 0)
                    ELSE 0
                END) AS ExpenseSlipTransferredAmount
            FROM dbo.PosFaturaSatirs AS line WITH (READUNCOMMITTED)
            INNER JOIN dbo.PosFaturas AS invoice WITH (READUNCOMMITTED)
                ON invoice.FaturaGuid = line.FaturaGuid
            WHERE line.Tarih >= @date
              AND line.Tarih < @nextDate
              AND TRY_CONVERT(int, line.Sube) IS NOT NULL
              AND TRY_CONVERT(int, invoice.BelgeTuru) IN (1, 3, 4)
              AND (@branchNo IS NULL OR TRY_CONVERT(int, line.Sube) = @branchNo)
            GROUP BY TRY_CONVERT(int, line.Sube)
            ORDER BY BranchNo;
            """;

        var furpaRows = await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            furpaSql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
            },
            reader => new SalesComparisonFurpaSqlRow(
                ReadInt(reader, "BranchNo"),
                ReadDecimal(reader, "RetailTransferredAmount"),
                ReadDecimal(reader, "InvoiceTransferredAmount"),
                ReadDecimal(reader, "ExpenseSlipTransferredAmount")),
            cancellationToken);

        var mikroByBranch = mikroRows.ToDictionary(item => item.BranchNo);
        var furpaByBranch = furpaRows.ToDictionary(item => item.BranchNo);
        var branchNos = mikroByBranch.Keys
            .Concat(furpaByBranch.Keys)
            .Where(item => item > 100)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        var branchNames = await GetBranchNamesAsync(branchNos, cancellationToken);

        var rows = branchNos.Select(branchNo =>
        {
            mikroByBranch.TryGetValue(branchNo, out var mikro);
            furpaByBranch.TryGetValue(branchNo, out var furpa);

            return CreateSalesComparisonRow(
                businessDate,
                branchNo,
                branchNames.GetValueOrDefault(branchNo, string.Empty),
                mikro?.RetailStockAmount ?? 0m,
                mikro?.InvoiceStockAmount ?? 0m,
                mikro?.ExpenseSlipStockAmount ?? 0m,
                mikro?.CurrentComparableStockAmount ?? 0m,
                mikro?.CurrentAccountAmount ?? 0m,
                furpa?.RetailTransferredAmount ?? 0m,
                furpa?.InvoiceTransferredAmount ?? 0m,
                furpa?.ExpenseSlipTransferredAmount ?? 0m,
                mikro?.ZReportAmount ?? 0m,
                tolerance);
        }).ToArray();

        var summary = new KasaHareketSalesComparisonSummaryDto(
            rows.Length,
            rows.Count(item => item.HasDifference),
            Round(rows.Sum(item => item.StockMovementAmount)),
            Round(rows.Sum(item => item.CurrentAccountAmount)),
            Round(rows.Sum(item => item.TransferredAmount)),
            Round(rows.Sum(item => item.ExpenseReceiptAmount)),
            Round(rows.Sum(item => item.ZReportAmount)),
            Round(rows.Sum(item => item.ZReportTransferDifference)),
            Round(rows.Sum(item => item.TransferredTurnoverDifference)),
            Round(rows.Sum(item => item.CurrentStockDifference)),
            Round(rows.Where(item => item.ZReportTransferDifference < -tolerance)
                .Sum(item => item.ZReportTransferDifference)),
            Round(rows.Where(item => item.ZReportTransferDifference > tolerance)
                .Sum(item => item.ZReportTransferDifference)),
            Round(rows.Sum(item => item.RetailStockAmount)),
            Round(rows.Sum(item => item.RetailTransferredAmount)),
            Round(rows.Sum(item => item.RetailDifference)),
            Round(rows.Sum(item => item.InvoiceStockAmount)),
            Round(rows.Sum(item => item.InvoiceTransferredAmount)),
            Round(rows.Sum(item => item.InvoiceDifference)),
            Round(rows.Sum(item => item.ExpenseSlipStockAmount)),
            Round(rows.Sum(item => item.ExpenseSlipTransferredAmount)),
            Round(rows.Sum(item => item.ExpenseSlipDifference)),
            Round(rows.Sum(item => item.NetStockTurnover)),
            Round(rows.Sum(item => item.NetTransferredTurnover)),
            Round(rows.Sum(item => item.NetDifference)));

        return new KasaHareketSalesComparisonDto(
            businessDate,
            request.BranchNo,
            tolerance,
            summary,
            rows);
    }

    public async Task<KasaHareketSatisMutabakatReportDto> GetSatisMutabakatReportAsync(
        KasaHareketSatisMutabakatRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));
        if (request.BranchNo is <= 0)
        {
            throw new ArgumentException("Sube numarasi sifirdan buyuk olmalidir.", nameof(request.BranchNo));
        }

        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);

        const string mikroSql = """
            WITH StockTotals AS (
                SELECT
                    movement.sth_cikis_depo_no AS BranchNo,
                    SUM(
                          ISNULL(movement.sth_tutar, 0)
                        - ISNULL(movement.sth_iskonto1, 0)
                        - ISNULL(movement.sth_iskonto2, 0)
                        - ISNULL(movement.sth_iskonto3, 0)
                        - ISNULL(movement.sth_iskonto4, 0)
                        - ISNULL(movement.sth_iskonto5, 0)
                        - ISNULL(movement.sth_iskonto6, 0)
                        + ISNULL(movement.sth_vergi, 0)) AS AktarilanTutar
                FROM dbo.STOK_HAREKETLERI AS movement WITH (READUNCOMMITTED)
                WHERE movement.sth_tarih = @date
                  AND movement.sth_normal_iade = 0
                  AND movement.sth_evrakno_seri LIKE 'PS%'
                  AND movement.sth_evrakno_seri NOT LIKE 'PSF%'
                  AND movement.sth_tip = 1
                  AND movement.sth_cins = 1
                  AND movement.sth_evraktip IN (1, 4)
                  AND (@branchNo IS NULL OR movement.sth_cikis_depo_no = @branchNo)
                GROUP BY movement.sth_cikis_depo_no
            ),
            ZReportTotals AS (
                SELECT
                    TRY_CONVERT(int, movement.cha_kod) AS BranchNo,
                    SUM(ISNULL(movement.cha_meblag, 0)) AS ZRaporuTutari
                FROM dbo.CARI_HESAP_HAREKETLERI AS movement WITH (READUNCOMMITTED)
                WHERE movement.cha_tarihi = @date
                  AND movement.cha_tip = 1
                  AND movement.cha_cinsi = 5
                  AND movement.cha_evrakno_seri = 'X'
                  AND NULLIF(LTRIM(RTRIM(ISNULL(cha_special1, ''))), '') IS NULL
                  AND TRY_CONVERT(int, movement.cha_kod) IS NOT NULL
                  AND (@branchNo IS NULL OR TRY_CONVERT(int, movement.cha_kod) = @branchNo)
                GROUP BY TRY_CONVERT(int, movement.cha_kod)
            ),
            ZReportBaskaCariTotals AS (
                SELECT
                    TRY_CONVERT(int, movement.cha_srmrkkodu) AS BranchNo,
                    SUM(ISNULL(movement.cha_meblag, 0)) AS BaskaCariTutar,
                    STRING_AGG(CAST(RTRIM(movement.cha_evrakno_seri) + '/' + CAST(movement.cha_evrakno_sira AS VARCHAR) + ' (Cari:' + RTRIM(movement.cha_kod) + ' ' + FORMAT(movement.cha_meblag, 'N0') + ' TL)' AS VARCHAR(MAX)), ', ') AS BaskaCariDetay
                FROM dbo.CARI_HESAP_HAREKETLERI AS movement WITH (READUNCOMMITTED)
                WHERE movement.cha_tarihi = @date
                  AND movement.cha_tip = 1
                  AND movement.cha_cinsi = 5
                  AND movement.cha_evrakno_seri = 'X'
                  AND NULLIF(LTRIM(RTRIM(ISNULL(cha_special1, ''))), '') IS NULL
                  AND (TRY_CONVERT(int, movement.cha_kod) IS NULL OR TRY_CONVERT(int, movement.cha_kod) <= 100 OR TRY_CONVERT(int, movement.cha_kod) <> TRY_CONVERT(int, movement.cha_srmrkkodu))
                  AND TRY_CONVERT(int, movement.cha_srmrkkodu) > 100
                  AND movement.cha_meblag > 500
                  AND (@branchNo IS NULL OR TRY_CONVERT(int, movement.cha_srmrkkodu) = @branchNo)
                GROUP BY TRY_CONVERT(int, movement.cha_srmrkkodu)
            ),
            Branches AS (
                SELECT BranchNo FROM StockTotals
                UNION
                SELECT BranchNo FROM ZReportTotals
                UNION
                SELECT BranchNo FROM ZReportBaskaCariTotals
            )
            SELECT
                branch.BranchNo,
                ISNULL(stock.AktarilanTutar, 0) AS AktarilanTutar,
                ISNULL(zReport.ZRaporuTutari, 0) AS ZRaporuTutari,
                ISNULL(baskaCari.BaskaCariTutar, 0) AS BaskaCariTutar,
                ISNULL(baskaCari.BaskaCariDetay, '') AS BaskaCariDetay
            FROM Branches AS branch
            LEFT JOIN StockTotals AS stock ON stock.BranchNo = branch.BranchNo
            LEFT JOIN ZReportTotals AS zReport ON zReport.BranchNo = branch.BranchNo
            LEFT JOIN ZReportBaskaCariTotals AS baskaCari ON baskaCari.BranchNo = branch.BranchNo
            WHERE branch.BranchNo > 100
            ORDER BY branch.BranchNo
            OPTION (RECOMPILE);
            """;

        var mikroRows = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            mikroSql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@branchNo", request.BranchNo);
            },
            reader => new SatisMutabakatMikroSqlRow(
                ReadInt(reader, "BranchNo"),
                ReadDecimal(reader, "AktarilanTutar"),
                ReadDecimal(reader, "ZRaporuTutari"),
                ReadDecimal(reader, "BaskaCariTutar"),
                ReadString(reader, "BaskaCariDetay")),
            cancellationToken);

        const string furpaSql = """
            WITH Faturalar AS (
                SELECT
                    TRY_CONVERT(int, line.Sube) AS BranchNo,
                    SUM(ISNULL(line.NetTutar, 0) + ISNULL(line.KdvTutari, 0)) AS AraTabloTutar,
                    SUM(CASE
                        WHEN TRY_CONVERT(int, invoice.BelgeTuru) = 4
                            THEN ISNULL(line.NetTutar, 0) + ISNULL(line.KdvTutari, 0)
                        ELSE 0
                    END) AS GiderPusulasiTutar,
                    SUM(CASE
                        WHEN TRY_CONVERT(int, invoice.BelgeTuru) IN (1, 3)
                            THEN ISNULL(line.NetTutar, 0) + ISNULL(line.KdvTutari, 0)
                        ELSE 0
                    END) AS GPSHaricTutar
                FROM dbo.PosFaturaSatirs AS line WITH (READUNCOMMITTED)
                INNER JOIN dbo.PosFaturas AS invoice WITH (READUNCOMMITTED)
                    ON invoice.FaturaGuid = line.FaturaGuid
                WHERE line.Tarih >= @date
                  AND line.Tarih < @nextDate
                  AND TRY_CONVERT(int, line.Sube) IS NOT NULL
                  AND TRY_CONVERT(int, invoice.BelgeTuru) IN (1, 3, 4)
                  AND (@branchNo IS NULL OR TRY_CONVERT(int, line.Sube) = @branchNo)
                GROUP BY TRY_CONVERT(int, line.Sube)
            ),
            Odemeler AS (
                SELECT
                    TRY_CONVERT(int, payment.Sube) AS BranchNo,
                    SUM(CASE WHEN ISNULL(payment.OdemeTipi, 0) = 1 THEN ISNULL(payment.Tutar, 0) ELSE 0 END) AS Nakit,
                    SUM(CASE WHEN ISNULL(payment.OdemeTipi, 0) = 2 THEN ISNULL(payment.Tutar, 0) ELSE 0 END) AS KrediKarti,
                    SUM(CASE WHEN ISNULL(payment.OdemeTipi, 0) = 3 THEN ISNULL(payment.Tutar, 0) ELSE 0 END) AS YemekKarti,
                    SUM(CASE WHEN ISNULL(payment.OdemeTipi, 0) = 4 THEN ISNULL(payment.Tutar, 0) ELSE 0 END) AS HediyeKarti
                FROM dbo.PosFaturaOdemes AS payment WITH (READUNCOMMITTED)
                WHERE payment.Tarih = @date
                  AND TRY_CONVERT(int, payment.Sube) IS NOT NULL
                  AND (@branchNo IS NULL OR TRY_CONVERT(int, payment.Sube) = @branchNo)
                GROUP BY TRY_CONVERT(int, payment.Sube)
            ),
            Branches AS (
                SELECT BranchNo FROM Faturalar
                UNION
                SELECT BranchNo FROM Odemeler
            )
            SELECT
                branch.BranchNo,
                ISNULL(f.AraTabloTutar, 0) AS AraTabloTutar,
                ISNULL(f.GiderPusulasiTutar, 0) AS GiderPusulasiTutar,
                ISNULL(f.GPSHaricTutar, 0) AS GPSHaricTutar,
                ISNULL(o.Nakit, 0) AS Nakit,
                ISNULL(o.KrediKarti, 0) AS KrediKarti,
                ISNULL(o.YemekKarti, 0) AS YemekKarti,
                ISNULL(o.HediyeKarti, 0) AS HediyeKarti
            FROM Branches branch
            LEFT JOIN Faturalar f ON f.BranchNo = branch.BranchNo
            LEFT JOIN Odemeler o ON o.BranchNo = branch.BranchNo
            WHERE branch.BranchNo > 100
            ORDER BY branch.BranchNo;
            """;

        var furpaRows = await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            furpaSql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
            },
            reader => new SatisMutabakatFurpaSqlRow(
                ReadInt(reader, "BranchNo"),
                ReadDecimal(reader, "AraTabloTutar"),
                ReadDecimal(reader, "GiderPusulasiTutar"),
                ReadDecimal(reader, "GPSHaricTutar"),
                ReadDecimal(reader, "Nakit"),
                ReadDecimal(reader, "KrediKarti"),
                ReadDecimal(reader, "YemekKarti"),
                ReadDecimal(reader, "HediyeKarti")),
            cancellationToken);

        var mikroByBranch = mikroRows.ToDictionary(item => item.BranchNo);
        var furpaByBranch = furpaRows.ToDictionary(item => item.BranchNo);
        var branchNos = mikroByBranch.Keys
            .Concat(furpaByBranch.Keys)
            .Where(item => item > 100)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        var branchNames = await GetBranchNamesAsync(branchNos, cancellationToken);

        var rows = branchNos.Select(branchNo =>
        {
            mikroByBranch.TryGetValue(branchNo, out var mikro);
            furpaByBranch.TryGetValue(branchNo, out var furpa);

            var araTabloTutar = Round(furpa?.AraTabloTutar ?? 0m);
            var giderPusulasiTutar = Round(furpa?.GiderPusulasiTutar ?? 0m);
            var gpsHaricTutar = Round(furpa?.GPSHaricTutar ?? 0m);
            var aktarilanTutar = Round(mikro?.AktarilanTutar ?? 0m);
            var nakit = Round(furpa?.Nakit ?? 0m);
            var krediKarti = Round(furpa?.KrediKarti ?? 0m);
            var yemekKarti = Round(furpa?.YemekKarti ?? 0m);
            var hediyeKarti = Round(furpa?.HediyeKarti ?? 0m);
            var zRaporuTutari = Round(mikro?.ZRaporuTutari ?? 0m);
            var baskaCariTutar = Round(mikro?.BaskaCariTutar ?? 0m);
            var baskaCariDetay = mikro?.BaskaCariDetay ?? string.Empty;

            var gercekKasaFarki = Round((gpsHaricTutar - hediyeKarti) - zRaporuTutari);
            var netAraTabloTutar = gpsHaricTutar;
            var transferDifference = Round(netAraTabloTutar - aktarilanTutar);

            string durum;
            if (netAraTabloTutar != 0m && aktarilanTutar == 0m)
            {
                durum = "MİKROYA AKTARILMAMIŞ";
            }
            else if (Math.Abs(transferDifference) <= 0.01m && Math.Abs(gercekKasaFarki) <= 1.00m)
            {
                durum = "MUTABIK";
            }
            else
            {
                durum = "FARK VAR";
            }

            string farkSebebi;
            if (netAraTabloTutar != 0m && aktarilanTutar == 0m)
            {
                farkSebebi = "Mikroya Aktarılmamış (Stagingde satış var ama Mikro faturası yok)";
            }
            else if (Math.Abs(transferDifference) > 0.01m)
            {
                farkSebebi = $"Furpa / Mikro net aktarım farkı: {transferDifference:N2} TL";
            }
            else if (gercekKasaFarki < -100m && zRaporuTutari > gpsHaricTutar)
            {
                farkSebebi = "Eksik Kasa Aktarımı (POS kasalarından biri staginge henüz gelmemiş)";
            }
            else if (Math.Abs(gercekKasaFarki) <= 1.00m && hediyeKarti > 0m)
            {
                farkSebebi = $"Hediye Kartı Kullanımı ({hediyeKarti:N0} TL düşüldü, fark kalmadı)";
            }
            else if (Math.Abs(gercekKasaFarki) <= 1.00m)
            {
                farkSebebi = "Yok (Kasa ve Z raporu tam mutabık)";
            }
            else if (Math.Abs(gercekKasaFarki - yemekKarti) <= 1.00m)
            {
                farkSebebi = $"Yemek Kartı Uyuşmazlığı ({yemekKarti:N2} TL yemek çekinden kaynaklı)";
            }
            else if (gercekKasaFarki > 500m && baskaCariTutar > 500m)
            {
                farkSebebi = $"Z Raporu Başka Cariye Yazılmış (Evrak: {baskaCariDetay})";
            }
            else if (gercekKasaFarki > 500m)
            {
                farkSebebi = "Z Raporu Eksik Girilmiş (Mikroda Z meblağı işlenmemiş veya eksik kaydedilmiş)";
            }
            else
            {
                farkSebebi = "Kasa Açığı / Fazlası (Fiş veya kuruş farkı)";
            }

            return new KasaHareketSatisMutabakatRowDto(
                businessDate,
                branchNo,
                branchNames.GetValueOrDefault(branchNo, string.Empty),
                araTabloTutar,
                giderPusulasiTutar,
                gpsHaricTutar,
                aktarilanTutar,
                nakit,
                krediKarti,
                yemekKarti,
                hediyeKarti,
                zRaporuTutari,
                gercekKasaFarki,
                durum,
                farkSebebi);
        })
        .OrderBy(row => row.Durum switch
        {
            "MİKROYA AKTARILMAMIŞ" => 1,
            "FARK VAR" => 2,
            _ => 3
        })
        .ThenByDescending(row => Math.Abs(row.GercekKasaFarki))
        .ToArray();

        var summary = new KasaHareketSatisMutabakatSummaryDto(
            rows.Length,
            rows.Count(r => r.Durum == "MUTABIK"),
            rows.Count(r => r.Durum == "FARK VAR"),
            0,
            rows.Count(r => r.Durum == "MİKROYA AKTARILMAMIŞ"),
            Round(rows.Sum(r => r.AraTabloTutar)),
            Round(rows.Sum(r => r.GiderPusulasiTutar)),
            Round(rows.Sum(r => r.GPSHaricTutar)),
            Round(rows.Sum(r => r.AktarilanTutar)),
            Round(rows.Sum(r => r.Nakit)),
            Round(rows.Sum(r => r.KrediKarti)),
            Round(rows.Sum(r => r.YemekKarti)),
            Round(rows.Sum(r => r.HediyeKarti)),
            Round(rows.Sum(r => r.ZRaporuTutari)),
            Round(rows.Sum(r => r.GercekKasaFarki)));

        return new KasaHareketSatisMutabakatReportDto(
            businessDate,
            request.BranchNo,
            summary,
            rows);
    }

    public async Task<KasaHareketDosyaBilgiDto> GetHareketDosyaBilgisiAsync(
        KasaHareketDosyaBilgiRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BranchNo <= 0)
        {
            throw new ArgumentException("Şube numarası sıfırdan büyük olmalıdır.", nameof(request.BranchNo));
        }
        if (request.CashRegisterNo < 0 || request.CashRegisterNo > 999)
        {
            throw new ArgumentException("Kasa numarası 0 ile 999 arasında olmalıdır.", nameof(request.CashRegisterNo));
        }

        var date = (request.Date ?? DateTime.Today).Date;
        var dateFormatted = date.ToString("ddMMyy", CultureInfo.InvariantCulture);

        if (request.CashRegisterNo == 0)
        {
            var allCashRegs = await furpaDbContext.CashRegistryDetails
                .AsNoTracking()
                .Where(c => c.BranchNo == request.BranchNo)
                .Select(c => c.CashRegisterNo)
                .Distinct()
                .OrderBy(cashRegisterNo => cashRegisterNo)
                .ToArrayAsync(cancellationToken);

            var bDetail = await furpaDbContext.BranchDetails
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BranchNo == request.BranchNo, cancellationToken);

            var dep = await mikroDbContext.DEPOLARs
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.dep_no == request.BranchNo, cancellationToken);

            var bName = dep?.dep_adi ?? string.Empty;
            var bIp = bDetail?.BranchIpAddress ?? string.Empty;
            var pGenel = bDetail?.PosGenelFolderPath ?? string.Empty;

            var cRoot = ResolveFileRootPath(null);
            var cFolderPath = Path.Combine(cRoot, request.BranchNo.ToString(CultureInfo.InvariantCulture));

            int sourceExistingCount = 0;
            long totalSourceBytes = 0;
            DateTime? latestSourceModified = null;

            int destExistingCount = 0;
            long totalDestBytes = 0;
            DateTime? latestDestModified = null;

            foreach (var crNo in allCashRegs)
            {
                var kasaStr = crNo.ToString("D3", CultureInfo.InvariantCulture);
                var fn = $"HR{dateFormatted}.{kasaStr}";

                if (!string.IsNullOrWhiteSpace(bIp))
                {
                    var sp = $@"\\{bIp}\{pGenel.Trim('\\')}\{fn}";
                    try
                    {
                        if (File.Exists(sp))
                        {
                            var fi = new FileInfo(sp);
                            sourceExistingCount++;
                            totalSourceBytes += fi.Length;
                            if (latestSourceModified == null || fi.LastWriteTime > latestSourceModified)
                            {
                                latestSourceModified = fi.LastWriteTime;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                var dp = Path.Combine(cFolderPath, fn);
                try
                {
                    if (File.Exists(dp))
                    {
                        var fi = new FileInfo(dp);
                        destExistingCount++;
                        totalDestBytes += fi.Length;
                        if (latestDestModified == null || fi.LastWriteTime > latestDestModified)
                        {
                            latestDestModified = fi.LastWriteTime;
                        }
                    }
                }
                catch
                {
                }
            }

            return new KasaHareketDosyaBilgiDto(
                request.BranchNo,
                bName,
                0,
                "TÜM",
                date,
                $"HR{dateFormatted}.* (Tüm Kasalar: {allCashRegs.Length} Kasa)",
                bIp,
                pGenel,
                string.IsNullOrWhiteSpace(bIp)
                    ? string.Empty
                    : $@"\\{bIp}\{pGenel.Trim('\\')}\HR{dateFormatted}.* ({sourceExistingCount}/{allCashRegs.Length} kasa mevcut)",
                sourceExistingCount > 0,
                totalSourceBytes,
                latestSourceModified,
                cFolderPath,
                Path.Combine(cFolderPath, $"HR{dateFormatted}.* ({destExistingCount}/{allCashRegs.Length} kasa mevcut)"),
                destExistingCount > 0,
                totalDestBytes,
                latestDestModified);
        }

        var formattedKasaNo = request.CashRegisterNo.ToString("D3", CultureInfo.InvariantCulture);
        var fileName = $"HR{dateFormatted}.{formattedKasaNo}";

        var branchDetail = await furpaDbContext.BranchDetails
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BranchNo == request.BranchNo, cancellationToken);

        var depo = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.dep_no == request.BranchNo, cancellationToken);

        var branchName = depo?.dep_adi ?? string.Empty;
        var branchIp = branchDetail?.BranchIpAddress ?? string.Empty;
        var posGenel = branchDetail?.PosGenelFolderPath ?? string.Empty;

        var sourceFilePath = string.IsNullOrWhiteSpace(branchIp)
            ? string.Empty
            : $@"\\{branchIp}\{posGenel.Trim('\\')}\{fileName}";

        var centralRoot = ResolveFileRootPath(null);
        var centralFolderPath = Path.Combine(centralRoot, request.BranchNo.ToString(CultureInfo.InvariantCulture));
        var destFilePath = Path.Combine(centralFolderPath, fileName);

        bool sourceExists = false;
        long? sourceSize = null;
        DateTime? sourceModified = null;
        if (!string.IsNullOrWhiteSpace(sourceFilePath))
        {
            try
            {
                if (File.Exists(sourceFilePath))
                {
                    var fi = new FileInfo(sourceFilePath);
                    sourceExists = true;
                    sourceSize = fi.Length;
                    sourceModified = fi.LastWriteTime;
                }
            }
            catch
            {
                // Ağ / paylaşım erişim hatalarını yutuyoruz
            }
        }

        bool destExists = false;
        long? destSize = null;
        DateTime? destModified = null;
        try
        {
            if (File.Exists(destFilePath))
            {
                var fi = new FileInfo(destFilePath);
                destExists = true;
                destSize = fi.Length;
                destModified = fi.LastWriteTime;
            }
        }
        catch
        {
            // Ağ / klasör erişim hatalarını yutuyoruz
        }

        return new KasaHareketDosyaBilgiDto(
            request.BranchNo,
            branchName,
            request.CashRegisterNo,
            formattedKasaNo,
            date,
            fileName,
            branchIp,
            posGenel,
            sourceFilePath,
            sourceExists,
            sourceSize,
            sourceModified,
            centralFolderPath,
            destFilePath,
            destExists,
            destSize,
            destModified);
    }

    public async Task<KasaHareketDosyaKopyalaResultDto> KopyalaHareketDosyasiAsync(
        KasaHareketDosyaKopyalaRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashRegisterNo == 0)
        {
            var allCashRegs = await furpaDbContext.CashRegistryDetails
                .AsNoTracking()
                .Where(c => c.BranchNo == request.BranchNo)
                .Select(c => c.CashRegisterNo)
                .Distinct()
                .OrderBy(cashRegisterNo => cashRegisterNo)
                .ToArrayAsync(cancellationToken);

            if (allCashRegs.Length == 0)
            {
                return new KasaHareketDosyaKopyalaResultDto(
                    false,
                    $"{request.BranchNo} numaralı şube için kayıtlı kasa tanımı bulunamadı.",
                    string.Empty,
                    string.Empty,
                    0,
                    0);
            }

            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            int successCount = 0;
            int notFoundCount = 0;
            long totalBytes = 0;
            var errList = new List<string>();

            foreach (var crNo in allCashRegs)
            {
                var singleResult = await KopyalaHareketDosyasiAsync(
                    new KasaHareketDosyaKopyalaRequest(request.BranchNo, crNo, request.Date, request.Overwrite),
                    cancellationToken);

                if (singleResult.Success)
                {
                    successCount++;
                    totalBytes += singleResult.BytesCopied;
                }
                else
                {
                    if (singleResult.Message.Contains("bulunamadı"))
                    {
                        notFoundCount++;
                    }
                    else
                    {
                        errList.Add($"Kasa {crNo}: {singleResult.Message}");
                    }
                }
            }

            swTotal.Stop();
            var summary = $"Şube {request.BranchNo}: {allCashRegs.Length} kasadan {successCount} tanesi başarıyla kopyalandı ({totalBytes:N0} bayt, {swTotal.ElapsedMilliseconds} ms).";
            if (notFoundCount > 0)
            {
                summary += $" ({notFoundCount} kasada dosya bulunamadı)";
            }
            if (errList.Count > 0)
            {
                summary += $" Detay: {string.Join("; ", errList)}";
            }

            var cRoot = ResolveFileRootPath(null);
            var cFolder = Path.Combine(cRoot, request.BranchNo.ToString(CultureInfo.InvariantCulture));

            return new KasaHareketDosyaKopyalaResultDto(
                successCount > 0,
                summary,
                $"Şube {request.BranchNo} tüm kasalar",
                cFolder,
                totalBytes,
                swTotal.ElapsedMilliseconds);
        }

        var info = await GetHareketDosyaBilgisiAsync(
            new KasaHareketDosyaBilgiRequest(request.BranchNo, request.CashRegisterNo, request.Date),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(info.SourceFilePath))
        {
            return new KasaHareketDosyaKopyalaResultDto(
                false,
                $"Şube {request.BranchNo} için IP adresi veya klasör yolu yapılandırılmamış.",
                string.Empty,
                info.DestinationFilePath,
                0,
                0);
        }

        if (!info.SourceFileExists)
        {
            return new KasaHareketDosyaKopyalaResultDto(
                false,
                $"Şube kasasında dosya bulunamadı: {info.SourceFilePath}",
                info.SourceFilePath,
                info.DestinationFilePath,
                0,
                0);
        }

        if (info.DestinationFileExists && !request.Overwrite)
        {
            return new KasaHareketDosyaKopyalaResultDto(
                false,
                $"Merkezde dosya zaten mevcut ve üzerine yazma seçeneği kapalı: {info.DestinationFilePath}",
                info.SourceFilePath,
                info.DestinationFilePath,
                0,
                0);
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var bytesCopied = await CopyFileAtomicallyAsync(
                info.SourceFilePath,
                info.DestinationFilePath,
                request.Overwrite,
                cancellationToken);

            sw.Stop();
            return new KasaHareketDosyaKopyalaResultDto(
                true,
                $"Dosya başarıyla merkeze kopyalandı ({bytesCopied:N0} bayt, {sw.ElapsedMilliseconds} ms).",
                info.SourceFilePath,
                info.DestinationFilePath,
                bytesCopied,
                sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new KasaHareketDosyaKopyalaResultDto(
                false,
                $"Dosya kopyalanırken hata oluştu: {ex.Message}",
                info.SourceFilePath,
                info.DestinationFilePath,
                0,
                0);
        }
    }

    internal static async Task<long> CopyFileAtomicallyAsync(
        string sourceFilePath,
        string destinationFilePath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 64 * 1024;
        var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new InvalidOperationException("Hedef dosya klasoru belirlenemedi.");
        }

        Directory.CreateDirectory(destinationDirectory);
        var temporaryFilePath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationFilePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            long bytesCopied = 0;
            var buffer = new byte[bufferSize];

            await using (var sourceStream = new FileStream(
                             sourceFilePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.ReadWrite,
                             bufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var temporaryStream = new FileStream(
                             temporaryFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(
                           buffer.AsMemory(0, bufferSize),
                           cancellationToken)) > 0)
                {
                    await temporaryStream.WriteAsync(
                        buffer.AsMemory(0, bytesRead),
                        cancellationToken);
                    bytesCopied += bytesRead;
                }

                await temporaryStream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryFilePath, destinationFilePath, overwrite);
            return bytesCopied;
        }
        catch
        {
            try
            {
                File.Delete(temporaryFilePath);
            }
            catch
            {
                // The original copy failure is more useful than a cleanup failure.
            }

            throw;
        }
    }

    public async Task<KasaHareketDosyaIcerikDto> OkuHareketDosyasiIcerigiAsync(
        KasaHareketDosyaIcerikRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashRegisterNo == 0)
        {
            throw new ArgumentException("Dosya içeriğini görüntülemek için belirli bir kasa numarası seçilmelidir.", nameof(request.CashRegisterNo));
        }

        var info = await GetHareketDosyaBilgisiAsync(
            new KasaHareketDosyaBilgiRequest(request.BranchNo, request.CashRegisterNo, request.Date),
            cancellationToken);

        var isSource = string.Equals(request.TargetLocation, "source", StringComparison.OrdinalIgnoreCase);
        var targetPath = isSource ? info.SourceFilePath : info.DestinationFilePath;
        var readFrom = isSource ? "Şube Kasası" : "Merkez Klasör";

        if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
        {
            throw new FileNotFoundException($"İncelenmek istenen dosya bulunamadı ({readFrom}): {targetPath}");
        }

        var maxLines = request.MaxLines is > 0 ? request.MaxLines.Value : int.MaxValue;
        var lines = new List<string>();
        int totalLines = 0;

        Encoding encoding;
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encoding = Encoding.GetEncoding(1254);
        }
        catch
        {
            encoding = Encoding.UTF8;
        }

        await using var fs = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, encoding);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            totalLines++;
            if (lines.Count < maxLines)
            {
                lines.Add(line);
            }
        }

        return new KasaHareketDosyaIcerikDto(
            info.FileName,
            targetPath,
            readFrom,
            totalLines,
            lines.Count,
            lines);
    }

    public async Task<KasaHareketKasaZDetayReportDto> GetKasaZDetayReportAsync(
        KasaHareketKasaZDetayRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));
        if (request.BranchNo <= 0)
        {
            throw new ArgumentException("Şube numarası sıfırdan büyük olmalıdır.", nameof(request.BranchNo));
        }

        var depo = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.dep_no == request.BranchNo, cancellationToken);
        var branchName = depo?.dep_adi ?? string.Empty;

        const string sql = """
            SELECT
                TRY_CONVERT(int, cha_kod) AS BranchNo,
                ISNULL(PARSENAME(REPLACE(cha_aciklama, ' ', '.'), 2), '') AS KasaNo,
                ISNULL(cha_meblag, 0) AS ZTutari,
                ISNULL(cha_evrakno_seri, '') AS DocumentSeries,
                ISNULL(cha_evrakno_sira, 0) AS DocumentSequence,
                ISNULL(cha_aciklama, '') AS Description
            FROM dbo.CARI_HESAP_HAREKETLERI WITH (READUNCOMMITTED)
            WHERE cha_evrak_tip = 60
              AND cha_tip = 1
              AND cha_cinsi = 5
              AND cha_belge_tarih = @date
              AND cha_srmrkkodu = @branchNo
              AND cha_kod = cha_srmrkkodu
            ORDER BY PARSENAME(REPLACE(cha_aciklama, ' ', '.'), 2), cha_evrakno_sira
            """;

        var rows = new List<KasaHareketKasaZDetayRowDto>();
        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            var pDate = command.CreateParameter();
            pDate.ParameterName = "@date";
            pDate.Value = request.Date.Date;
            command.Parameters.Add(pDate);

            var pBranch = command.CreateParameter();
            pBranch.ParameterName = "@branchNo";
            pBranch.Value = request.BranchNo;
            command.Parameters.Add(pBranch);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new KasaHareketKasaZDetayRowDto(
                    ReadInt(reader, "BranchNo"),
                    ReadString(reader, "KasaNo"),
                    Round(ReadDecimal(reader, "ZTutari")),
                    ReadString(reader, "DocumentSeries"),
                    ReadInt(reader, "DocumentSequence"),
                    ReadString(reader, "Description")));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var total = rows.Sum(r => r.ZTutari);
        return new KasaHareketKasaZDetayReportDto(
            request.Date.Date,
            request.BranchNo,
            branchName,
            Round(total),
            rows);
    }

    public async Task<KasaHareketBirlikKartReportDto> GetBirlikKartReportAsync(
        KasaHareketBirlikKartRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));
        var businessDate = request.Date.Date;

        var branchNames = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(d => d.dep_no.HasValue && d.dep_no.Value > 100)
            .ToDictionaryAsync(d => d.dep_no!.Value, d => d.dep_adi ?? string.Empty, cancellationToken);

        const string sql = """
            SELECT
                ISNULL(o.KasaMaliBellekKodu, '') AS KasaMaliBellekKodu,
                o.Sube AS SubeNo,
                ISNULL(o.KasaKodu, 0) AS CashRegisterNo,
                SUM(ISNULL(o.Tutar, 0)) AS Toplam,
                ISNULL(o.CekNumarasi, '') AS CekNumarasi
            FROM dbo.PosFaturaOdemes AS o WITH (NOLOCK)
            WHERE o.CekNumarasi <> N''
              AND o.Tarih = @date
              AND (@branchNo IS NULL OR o.Sube = @branchNo)
            GROUP BY o.CekNumarasi, o.Sube, o.KasaMaliBellekKodu, o.KasaKodu
            ORDER BY o.Sube, SUM(ISNULL(o.Tutar, 0)) DESC
            """;

        var rows = new List<KasaHareketBirlikKartRowDto>();
        var connection = furpaDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            var pDate = command.CreateParameter();
            pDate.ParameterName = "@date";
            pDate.Value = businessDate;
            command.Parameters.Add(pDate);

            var pBranch = command.CreateParameter();
            pBranch.ParameterName = "@branchNo";
            pBranch.Value = (object?)request.BranchNo ?? DBNull.Value;
            command.Parameters.Add(pBranch);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var memoryCode = ReadString(reader, "KasaMaliBellekKodu");
                var branchNo = ReadInt(reader, "SubeNo");
                var cashRegNo = ReadInt(reader, "CashRegisterNo");
                var amount = ReadDecimal(reader, "Toplam");
                var cardNo = ReadString(reader, "CekNumarasi");

                rows.Add(new KasaHareketBirlikKartRowDto(
                    branchNo,
                    branchNames.GetValueOrDefault(branchNo, string.Empty),
                    memoryCode,
                    cashRegNo,
                    Round(amount),
                    cardNo));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var totalAmount = rows.Sum(r => r.Tutar);
        return new KasaHareketBirlikKartReportDto(
            businessDate,
            request.BranchNo,
            Round(totalAmount),
            rows.Count,
            rows);
    }

    public async Task<KasaHareketFisIptalReportDto> GetFisIptalReportAsync(
        KasaHareketFisIptalRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.StartDate, nameof(request.StartDate));
        ValidateDate(request.EndDate, nameof(request.EndDate));
        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("Başlangıç tarihi bitiş tarihinden büyük olamaz.");
        }

        var branchDict = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(d => d.dep_no.HasValue && d.dep_no.Value > 100)
            .ToDictionaryAsync(d => d.dep_no!.Value, d => (Name: d.dep_adi ?? string.Empty, Region: d.dep_bolge_kodu ?? string.Empty), cancellationToken);

        const string sql = """
            SELECT
                i.Sube AS SubeNo,
                i.Tarih,
                CONVERT(varchar(5), i.saat, 108) AS Saat,
                ISNULL(i.ZNo, 0) AS ZNo,
                ISNULL(i.FisNo, 0) AS FisNo,
                ISNULL(i.KasaNo, 0) AS KasaNo,
                SUM(ISNULL(i.Toplam, 0) + ISNULL(i.ToplamKdv, 0)) AS Tutar,
                ISNULL(k.CashierName, '') AS CashierName,
                ISNULL(k.CashierCode, i.KullaniciKodu) AS CashierCode,
                i.FaturaGuid
            FROM dbo.PosFaturaIptals AS i WITH (NOLOCK)
            LEFT JOIN dbo.Cashiers AS k WITH (NOLOCK) ON k.CashierCode = i.KullaniciKodu
            WHERE i.Tarih >= @startDate AND i.Tarih < @endDateExclusive
              AND i.IptalNedeni = 0
              AND (@branchNo IS NULL OR i.Sube = @branchNo)
            GROUP BY i.Tarih, i.saat, i.ZNo, i.FisNo, i.KasaNo, k.CashierCode, k.CashierName, i.KullaniciKodu, i.FaturaGuid, i.Sube
            ORDER BY i.Tarih DESC, i.Sube, i.saat DESC
            """;

        var rows = new List<KasaHareketFisIptalRowDto>();
        var connection = furpaDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 90;

            var pStart = command.CreateParameter();
            pStart.ParameterName = "@startDate";
            pStart.Value = request.StartDate.Date;
            command.Parameters.Add(pStart);

            var pEndExclusive = command.CreateParameter();
            pEndExclusive.ParameterName = "@endDateExclusive";
            pEndExclusive.Value = request.EndDate.Date.AddDays(1);
            command.Parameters.Add(pEndExclusive);

            var pBranch = command.CreateParameter();
            pBranch.ParameterName = "@branchNo";
            pBranch.Value = (object?)request.BranchNo ?? DBNull.Value;
            command.Parameters.Add(pBranch);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var branchNo = ReadInt(reader, "SubeNo");
                var tarih = ReadDateTime(reader, "Tarih");
                var saat = ReadString(reader, "Saat");
                var zNo = ReadInt(reader, "ZNo");
                var fisNo = ReadInt(reader, "FisNo");
                var kasaNo = ReadInt(reader, "KasaNo");
                var tutar = ReadDecimal(reader, "Tutar");
                var cashierName = ReadString(reader, "CashierName");
                var cashierCode = ReadInt(reader, "CashierCode");
                var faturaGuid = ReadGuid(reader, "FaturaGuid");

                var meta = branchDict.GetValueOrDefault(branchNo);
                rows.Add(new KasaHareketFisIptalRowDto(
                    meta.Region ?? string.Empty,
                    branchNo,
                    meta.Name ?? string.Empty,
                    tarih,
                    saat,
                    zNo,
                    fisNo,
                    kasaNo,
                    Round(tutar),
                    cashierName,
                    cashierCode,
                    faturaGuid));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var totalAmount = rows.Sum(r => r.Tutar);
        return new KasaHareketFisIptalReportDto(
            request.StartDate.Date,
            request.EndDate.Date,
            request.BranchNo,
            Round(totalAmount),
            rows.Count,
            rows);
    }

    public async Task<KasaHareketFisIptalDetailDto> GetFisIptalDetailAsync(
        Guid faturaGuid,
        CancellationToken cancellationToken)
    {
        if (faturaGuid == Guid.Empty)
        {
            throw new ArgumentException("Geçerli bir FaturaGuid belirtilmelidir.", nameof(faturaGuid));
        }

        const string headerSql = """
            SELECT TOP 1
                i.FaturaGuid,
                i.Tarih,
                CONVERT(varchar(5), i.saat, 108) AS Saat,
                i.Sube AS SubeNo,
                ISNULL(i.KasaNo, 0) AS KasaNo,
                ISNULL(k.CashierName, '') AS CashierName,
                ISNULL(i.Toplam, 0) + ISNULL(i.ToplamKdv, 0) AS ToplamTutar
            FROM dbo.PosFaturaIptals AS i WITH (NOLOCK)
            LEFT JOIN dbo.Cashiers AS k WITH (NOLOCK) ON k.CashierCode = i.KullaniciKodu
            WHERE i.FaturaGuid = @faturaGuid
            """;

        DateTime tarih = DateTime.MinValue;
        string saat = string.Empty;
        int branchNo = 0;
        int kasaNo = 0;
        string cashierName = string.Empty;
        decimal toplamTutar = 0;
        bool found = false;

        const string lineSql = """
            SELECT
                ISNULL(a.Barkod, '') AS Barcode,
                ISNULL(a.Miktar, 0) AS Quantity,
                ISNULL(a.BirimFiyat, 0) AS UnitPrice,
                (ISNULL(a.Miktar, 0) * ISNULL(a.BirimFiyat, 0)) AS LineTotal
            FROM dbo.PosFaturaIptalSatirs AS a WITH (NOLOCK)
            WHERE a.FaturaGuid = @faturaGuid
            """;

        var tempLines = new List<(string Barcode, decimal Quantity, decimal UnitPrice, decimal LineTotal)>();
        var furpaConn = furpaDbContext.Database.GetDbConnection();
        var shouldCloseFurpa = furpaConn.State == ConnectionState.Closed;
        if (shouldCloseFurpa)
        {
            await furpaConn.OpenAsync(cancellationToken);
        }
        try
        {
            await using (var headerCmd = furpaConn.CreateCommand())
            {
                headerCmd.CommandText = headerSql;
                var pGuid = headerCmd.CreateParameter();
                pGuid.ParameterName = "@faturaGuid";
                pGuid.Value = faturaGuid;
                headerCmd.Parameters.Add(pGuid);

                await using var headerReader = await headerCmd.ExecuteReaderAsync(cancellationToken);
                if (await headerReader.ReadAsync(cancellationToken))
                {
                    found = true;
                    tarih = ReadDateTime(headerReader, "Tarih");
                    saat = ReadString(headerReader, "Saat");
                    branchNo = ReadInt(headerReader, "SubeNo");
                    kasaNo = ReadInt(headerReader, "KasaNo");
                    cashierName = ReadString(headerReader, "CashierName");
                    toplamTutar = ReadDecimal(headerReader, "ToplamTutar");
                }
            }

            if (!found)
            {
                throw new KeyNotFoundException($"İptal fiş belgesi bulunamadı: {faturaGuid}");
            }

            await using (var lineCmd = furpaConn.CreateCommand())
            {
                lineCmd.CommandText = lineSql;
                var pGuid = lineCmd.CreateParameter();
                pGuid.ParameterName = "@faturaGuid";
                pGuid.Value = faturaGuid;
                lineCmd.Parameters.Add(pGuid);

                await using var lineReader = await lineCmd.ExecuteReaderAsync(cancellationToken);
                while (await lineReader.ReadAsync(cancellationToken))
                {
                    tempLines.Add((
                        ReadString(lineReader, "Barcode"),
                        ReadDecimal(lineReader, "Quantity"),
                        ReadDecimal(lineReader, "UnitPrice"),
                        ReadDecimal(lineReader, "LineTotal")));
                }
            }
        }
        finally
        {
            if (shouldCloseFurpa)
            {
                await furpaConn.CloseAsync();
            }
        }

        var depo = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.dep_no == branchNo, cancellationToken);
        var branchName = depo?.dep_adi ?? string.Empty;

        var barcodes = tempLines.Select(l => l.Barcode).Where(b => !string.IsNullOrWhiteSpace(b)).Distinct().ToArray();
        var barcodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (barcodes.Length > 0)
        {
            var productRows = await (
                from b in mikroDbContext.BARKOD_TANIMLARIs.AsNoTracking()
                join s in mikroDbContext.STOKLARs.AsNoTracking() on b.bar_stokkodu equals s.sto_kod
                where barcodes.Contains(b.bar_kodu)
                select new { b.bar_kodu, s.sto_isim }
            ).ToArrayAsync(cancellationToken);

            foreach (var pr in productRows)
            {
                if (!string.IsNullOrWhiteSpace(pr.bar_kodu) && !barcodeMap.ContainsKey(pr.bar_kodu))
                {
                    barcodeMap[pr.bar_kodu] = pr.sto_isim ?? string.Empty;
                }
            }
        }

        var lines = tempLines.Select(l => new KasaHareketFisIptalLineDto(
            l.Barcode,
            barcodeMap.GetValueOrDefault(l.Barcode, string.Empty),
            l.Quantity,
            Round(l.UnitPrice),
            Round(l.LineTotal)
        )).ToArray();

        return new KasaHareketFisIptalDetailDto(
            faturaGuid,
            tarih,
            saat,
            branchNo,
            branchName,
            kasaNo,
            cashierName,
            Round(toplamTutar),
            lines);
    }

    public async Task<KasaHareketGiderPusulasiReportDto> GetGiderPusulasiReportAsync(
        KasaHareketGiderPusulasiRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.StartDate, nameof(request.StartDate));
        ValidateDate(request.EndDate, nameof(request.EndDate));
        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("Başlangıç tarihi bitiş tarihinden büyük olamaz.");
        }

        var branchNames = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(d => d.dep_no.HasValue && d.dep_no.Value > 100)
            .ToDictionaryAsync(d => d.dep_no!.Value, d => d.dep_adi ?? string.Empty, cancellationToken);

        const string sql = """
            SELECT
                p.Sube AS SubeNo,
                p.Tarih,
                CONVERT(VARCHAR(5), p.Saat, 108) AS Saat,
                ISNULL(p.FisNo, 0) AS FisNo,
                ISNULL(p.KasaNo, 0) AS KasaNo,
                ISNULL(p.KullaniciKodu, 0) AS CashierCode,
                ISNULL(k.CashierName, '') AS CashierName,
                SUM(ISNULL(p.Toplam, 0) + ISNULL(p.ToplamKdv, 0)) AS Tutar,
                p.FaturaGuid
            FROM dbo.PosFaturas AS p WITH (NOLOCK)
            LEFT JOIN dbo.Cashiers AS k WITH (NOLOCK) ON k.CashierCode = p.KullaniciKodu
            WHERE p.Tarih >= @startDate AND p.Tarih < @endDateExclusive
              AND p.BelgeTuru = 4
              AND (@branchNo IS NULL OR p.Sube = @branchNo)
            GROUP BY p.Sube, p.Tarih, p.Saat, p.FisNo, p.KasaNo, p.KullaniciKodu, k.CashierName, p.FaturaGuid
            ORDER BY p.Tarih DESC, p.Sube, p.Saat DESC
            """;

        var rows = new List<KasaHareketGiderPusulasiRowDto>();
        var connection = furpaDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 90;

            var pStart = command.CreateParameter();
            pStart.ParameterName = "@startDate";
            pStart.Value = request.StartDate.Date;
            command.Parameters.Add(pStart);

            var pEndExclusive = command.CreateParameter();
            pEndExclusive.ParameterName = "@endDateExclusive";
            pEndExclusive.Value = request.EndDate.Date.AddDays(1);
            command.Parameters.Add(pEndExclusive);

            var pBranch = command.CreateParameter();
            pBranch.ParameterName = "@branchNo";
            pBranch.Value = (object?)request.BranchNo ?? DBNull.Value;
            command.Parameters.Add(pBranch);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var branchNo = ReadInt(reader, "SubeNo");
                var tarih = ReadDateTime(reader, "Tarih");
                var saat = ReadString(reader, "Saat");
                var fisNo = ReadInt(reader, "FisNo");
                var kasaNo = ReadInt(reader, "KasaNo");
                var cashierCode = ReadInt(reader, "CashierCode");
                var cashierName = ReadString(reader, "CashierName");
                var tutar = ReadDecimal(reader, "Tutar");
                var faturaGuid = ReadGuid(reader, "FaturaGuid");

                rows.Add(new KasaHareketGiderPusulasiRowDto(
                    branchNo,
                    branchNames.GetValueOrDefault(branchNo, string.Empty),
                    tarih,
                    saat,
                    fisNo,
                    kasaNo,
                    cashierCode,
                    cashierName,
                    Round(tutar),
                    faturaGuid));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var totalAmount = rows.Sum(r => r.Tutar);
        return new KasaHareketGiderPusulasiReportDto(
            request.StartDate.Date,
            request.EndDate.Date,
            request.BranchNo,
            Round(totalAmount),
            rows.Count,
            rows);
    }

    public async Task<KasaHareketGiderPusulasiDetailDto> GetGiderPusulasiDetailAsync(
        Guid faturaGuid,
        CancellationToken cancellationToken)
    {
        if (faturaGuid == Guid.Empty)
        {
            throw new ArgumentException("Geçerli bir FaturaGuid belirtilmelidir.", nameof(faturaGuid));
        }

        const string headerSql = """
            SELECT TOP 1
                p.FaturaGuid,
                p.Tarih,
                CONVERT(varchar(5), p.Saat, 108) AS Saat,
                p.Sube AS SubeNo,
                ISNULL(p.KasaNo, 0) AS KasaNo,
                ISNULL(k.CashierName, '') AS CashierName,
                ISNULL(p.Toplam, 0) + ISNULL(p.ToplamKdv, 0) AS ToplamTutar
            FROM dbo.PosFaturas AS p WITH (NOLOCK)
            LEFT JOIN dbo.Cashiers AS k WITH (NOLOCK) ON k.CashierCode = p.KullaniciKodu
            WHERE p.FaturaGuid = @faturaGuid
            """;

        DateTime tarih = DateTime.MinValue;
        string saat = string.Empty;
        int branchNo = 0;
        int kasaNo = 0;
        string cashierName = string.Empty;
        decimal toplamTutar = 0;
        bool found = false;

        const string lineSql = """
            SELECT
                ISNULL(s.urunkodu, '') AS ProductCode,
                ISNULL(s.Barkod, '') AS Barcode,
                ISNULL(s.Miktar, 0) AS Quantity,
                ISNULL(s.BirimFiyat, 0) AS UnitPrice,
                (ISNULL(s.Miktar, 0) * ISNULL(s.BirimFiyat, 0)) AS LineTotal
            FROM dbo.PosFaturaSatirs AS s WITH (NOLOCK)
            WHERE s.FaturaGuid = @faturaGuid
            """;

        var tempLines = new List<(string ProductCode, string Barcode, decimal Quantity, decimal UnitPrice, decimal LineTotal)>();
        var furpaConn = furpaDbContext.Database.GetDbConnection();
        var shouldCloseFurpa = furpaConn.State == ConnectionState.Closed;
        if (shouldCloseFurpa)
        {
            await furpaConn.OpenAsync(cancellationToken);
        }
        try
        {
            await using (var headerCmd = furpaConn.CreateCommand())
            {
                headerCmd.CommandText = headerSql;
                var pGuid = headerCmd.CreateParameter();
                pGuid.ParameterName = "@faturaGuid";
                pGuid.Value = faturaGuid;
                headerCmd.Parameters.Add(pGuid);

                await using var headerReader = await headerCmd.ExecuteReaderAsync(cancellationToken);
                if (await headerReader.ReadAsync(cancellationToken))
                {
                    found = true;
                    tarih = ReadDateTime(headerReader, "Tarih");
                    saat = ReadString(headerReader, "Saat");
                    branchNo = ReadInt(headerReader, "SubeNo");
                    kasaNo = ReadInt(headerReader, "KasaNo");
                    cashierName = ReadString(headerReader, "CashierName");
                    toplamTutar = ReadDecimal(headerReader, "ToplamTutar");
                }
            }

            if (!found)
            {
                throw new KeyNotFoundException($"Gider pusulası belgesi bulunamadı: {faturaGuid}");
            }

            await using (var lineCmd = furpaConn.CreateCommand())
            {
                lineCmd.CommandText = lineSql;
                var pGuid = lineCmd.CreateParameter();
                pGuid.ParameterName = "@faturaGuid";
                pGuid.Value = faturaGuid;
                lineCmd.Parameters.Add(pGuid);

                await using var lineReader = await lineCmd.ExecuteReaderAsync(cancellationToken);
                while (await lineReader.ReadAsync(cancellationToken))
                {
                    tempLines.Add((
                        ReadString(lineReader, "ProductCode"),
                        ReadString(lineReader, "Barcode"),
                        ReadDecimal(lineReader, "Quantity"),
                        ReadDecimal(lineReader, "UnitPrice"),
                        ReadDecimal(lineReader, "LineTotal")));
                }
            }
        }
        finally
        {
            if (shouldCloseFurpa)
            {
                await furpaConn.CloseAsync();
            }
        }

        var depo = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.dep_no == branchNo, cancellationToken);
        var branchName = depo?.dep_adi ?? string.Empty;

        var productCodes = tempLines.Select(l => l.ProductCode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToArray();
        var productMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (productCodes.Length > 0)
        {
            var productRows = await mikroDbContext.STOKLARs
                .AsNoTracking()
                .Where(s => productCodes.Contains(s.sto_kod))
                .Select(s => new { s.sto_kod, s.sto_isim })
                .ToArrayAsync(cancellationToken);

            foreach (var pr in productRows)
            {
                if (!string.IsNullOrWhiteSpace(pr.sto_kod) && !productMap.ContainsKey(pr.sto_kod))
                {
                    productMap[pr.sto_kod] = pr.sto_isim ?? string.Empty;
                }
            }
        }

        var lines = tempLines.Select(l => new KasaHareketGiderPusulasiLineDto(
            l.ProductCode,
            l.Barcode,
            productMap.GetValueOrDefault(l.ProductCode, string.Empty),
            l.Quantity,
            Round(l.UnitPrice),
            Round(l.LineTotal)
        )).ToArray();

        return new KasaHareketGiderPusulasiDetailDto(
            faturaGuid,
            tarih,
            saat,
            branchNo,
            branchName,
            kasaNo,
            cashierName,
            Round(toplamTutar),
            lines);
    }

    public async Task<KasaHareketTrendyolReportDto> GetTrendyolReportAsync(
        KasaHareketTrendyolRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));
        var businessDate = request.Date.Date;

        var branchNames = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(d => d.dep_no.HasValue && d.dep_no.Value > 100)
            .ToDictionaryAsync(d => d.dep_no!.Value, d => d.dep_adi ?? string.Empty, cancellationToken);

        const string sql = """
            SELECT
                p.Sube AS SubeNo,
                p.Tarih,
                ISNULL(p.FisNo, 0) AS FisNo,
                ISNULL(p.KasaNo, 0) AS KasaNo,
                ISNULL(k.CashierName, '') AS CashierName,
                SUM(ISNULL(p.Toplam, 0) + ISNULL(p.ToplamKdv, 0)) AS Tutar
            FROM dbo.PosFaturas AS p WITH (NOLOCK)
            LEFT JOIN dbo.Cashiers AS k WITH (NOLOCK) ON k.CashierCode = p.KullaniciKodu
            WHERE p.MusteriCariKodu = '8590921777'
              AND p.Tarih = @date
              AND (@branchNo IS NULL OR p.Sube = @branchNo)
            GROUP BY p.Sube, p.Tarih, p.FisNo, p.KasaNo, k.CashierName
            ORDER BY p.Sube, p.KasaNo, p.FisNo
            """;

        var rows = new List<KasaHareketTrendyolRowDto>();
        var connection = furpaDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            var pDate = command.CreateParameter();
            pDate.ParameterName = "@date";
            pDate.Value = businessDate;
            command.Parameters.Add(pDate);

            var pBranch = command.CreateParameter();
            pBranch.ParameterName = "@branchNo";
            pBranch.Value = (object?)request.BranchNo ?? DBNull.Value;
            command.Parameters.Add(pBranch);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var branchNo = ReadInt(reader, "SubeNo");
                var tarih = ReadDateTime(reader, "Tarih");
                var fisNo = ReadInt(reader, "FisNo");
                var kasaNo = ReadInt(reader, "KasaNo");
                var cashierName = ReadString(reader, "CashierName");
                var tutar = ReadDecimal(reader, "Tutar");

                rows.Add(new KasaHareketTrendyolRowDto(
                    branchNo,
                    branchNames.GetValueOrDefault(branchNo, string.Empty),
                    tarih,
                    fisNo,
                    kasaNo,
                    cashierName,
                    Round(tutar)));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var totalAmount = rows.Sum(r => r.Tutar);
        return new KasaHareketTrendyolReportDto(
            businessDate,
            request.BranchNo,
            Round(totalAmount),
            rows.Count,
            rows);
    }

    public async Task<KasaHareketNakitReportDto> GetNakitReportAsync(
        KasaHareketNakitRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));
        var businessDate = request.Date.Date;

        const string sql = """
            SELECT
                s.BranchNo,
                ISNULL(d.dep_adi, '') AS SubeAdi,
                SUM(ISNULL(s.Amount, 0)) AS NakitTutar,
                ISNULL(s.Description, '') AS Description
            FROM dbo.Summaries AS s WITH (READUNCOMMITTED)
            INNER JOIN dbo.DEPOLAR AS d WITH (READUNCOMMITTED) ON d.dep_no = s.BranchNo
            WHERE s.Description = 'Nakit Toplam'
              AND s.SummaryDate = @date
            GROUP BY s.BranchNo, d.dep_adi, s.Description
            ORDER BY s.BranchNo
            """;

        var rows = new List<KasaHareketNakitRowDto>();
        var connection = mikroDbContext.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            var pDate = command.CreateParameter();
            pDate.ParameterName = "@date";
            pDate.Value = businessDate;
            command.Parameters.Add(pDate);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new KasaHareketNakitRowDto(
                    ReadInt(reader, "BranchNo"),
                    ReadString(reader, "SubeAdi"),
                    Round(ReadDecimal(reader, "NakitTutar")),
                    ReadString(reader, "Description")));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        var totalAmount = rows.Sum(r => r.NakitTutar);
        return new KasaHareketNakitReportDto(
            businessDate,
            Round(totalAmount),
            rows.Count,
            rows);
    }


    public async Task<KasaHareketCashSummaryComparisonDto> GetCashSummaryComparisonAsync(
        KasaHareketCashSummaryComparisonRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));

        if (request.Tolerance < 0m)
        {
            throw new ArgumentException("Tolerance can not be negative.", nameof(request.Tolerance));
        }

        var reportRows = await GetReportAsync(
            new KasaHareketReportRequest(request.Date, request.BranchNo, request.CashRegisterNo),
            cancellationToken);
        var cashSummaryRows = await GetCashSummaryRowsAsync(request, cancellationToken);
        var reportByKey = reportRows.ToDictionary(
            row => (row.BranchNo, row.CashRegisterNo),
            row => row);
        var cashSummaryByKey = cashSummaryRows.ToDictionary(
            row => (row.BranchNo, row.CashRegisterNo),
            row => row);
        var keys = reportByKey.Keys
            .Concat(cashSummaryByKey.Keys)
            .Distinct()
            .OrderBy(key => key.BranchNo)
            .ThenBy(key => key.CashRegisterNo)
            .ToArray();

        var branchNames = await GetBranchNamesAsync(
            keys.Select(key => key.BranchNo),
            cancellationToken);

        var rows = keys
            .Select(key =>
            {
                reportByKey.TryGetValue(key, out var reportRow);
                cashSummaryByKey.TryGetValue(key, out var cashSummaryRow);

                var movementZReportAmount = reportRow?.Difference ?? 0m;
                var cashSummaryAmount = cashSummaryRow?.CashSummaryAmount ?? 0m;
                var differenceAmount = Round(movementZReportAmount - cashSummaryAmount);
                var status = ResolveComparisonStatus(
                    reportRow,
                    cashSummaryRow,
                    differenceAmount,
                    request.Tolerance);

                return new KasaHareketCashSummaryComparisonRowDto(
                    request.Date.Date,
                    key.BranchNo,
                    ResolveBranchName(key.BranchNo, reportRow?.BranchName, branchNames),
                    key.CashRegisterNo,
                    reportRow?.NetAmount ?? 0m,
                    reportRow?.Expense ?? 0m,
                    reportRow?.CheckAmount ?? 0m,
                    movementZReportAmount,
                    cashSummaryAmount,
                    cashSummaryRow?.DocumentCount ?? 0,
                    differenceAmount,
                    status.Code,
                    status.Name);
            })
            .ToArray();

        var summary = new KasaHareketCashSummaryComparisonSummaryDto(
            rows.Length,
            rows.Count(row => row.Status == "balanced"),
            rows.Count(row => row.Status == "difference"),
            rows.Count(row => row.Status == "missing-cash-summary"),
            rows.Count(row => row.Status == "missing-movement"),
            Round(rows.Sum(row => row.MovementZReportAmount)),
            Round(rows.Sum(row => row.CashSummaryAmount)),
            Round(rows.Sum(row => row.DifferenceAmount)));

        return new KasaHareketCashSummaryComparisonDto(
            request.Date.Date,
            request.BranchNo,
            request.CashRegisterNo,
            request.Tolerance,
            summary,
            rows);
    }

    public async Task<KasaHareketDetailDto> GetDetailAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken)
    {
        ValidateDate(request.Date, nameof(request.Date));

        if (request.BranchNo <= 0)
        {
            throw new ArgumentException("Branch no must be greater than zero.", nameof(request.BranchNo));
        }

        if (request.CashRegisterNo < 0)
        {
            throw new ArgumentException("Cash register no can not be negative.", nameof(request.CashRegisterNo));
        }

        var receiptTake = Math.Clamp(request.ReceiptTake, 0, 5000);
        var exactReportRequest = new KasaHareketReportRequest(
            request.Date,
            request.BranchNo,
            request.CashRegisterNo);
        var movementReport = (await GetReportAsync(exactReportRequest, cancellationToken)).FirstOrDefault();
        var comparison = await GetCashSummaryComparisonAsync(
            new KasaHareketCashSummaryComparisonRequest(
                request.Date,
                request.BranchNo,
                request.CashRegisterNo,
                0.01m),
            cancellationToken);
        var comparisonRow = comparison.Rows.FirstOrDefault()
            ?? CreateEmptyComparisonRow(request.Date.Date, request.BranchNo, request.CashRegisterNo);

        var cashierSummaries = (await GetCashierSummariesAsync(request, cancellationToken)).ToArray();
        var movementPaymentSummaries = await GetMovementPaymentSummariesAsync(request, cancellationToken);
        var cashSummaryPayments = await GetCashSummaryPaymentsAsync(request, cancellationToken);
        var cashSummaryDocuments = (await GetCashSummaryDocumentsAsync(request, cancellationToken)).ToArray();
        var receipts = receiptTake > 0
            ? (await GetReceiptsAsync(request with { ReceiptTake = receiptTake }, cancellationToken)).ToArray()
            : [];
        var canceledReceipts = receiptTake > 0
            ? (await GetCanceledReceiptsAsync(request with { ReceiptTake = receiptTake }, cancellationToken)).ToArray()
            : [];

        var cashierCodes = cashierSummaries
            .Select(item => item.CashierCode)
            .Concat(receipts.Select(item => item.CashierCode))
            .Concat(canceledReceipts.Select(item => item.CashierCode))
            .Concat(cashSummaryDocuments.Select(item => item.CashierNo.ToString(CultureInfo.InvariantCulture)))
            .Concat(cashSummaryDocuments.Select(item => item.ManagerNo.ToString(CultureInfo.InvariantCulture)));
        var cashierNames = await GetCashierNamesAsync(cashierCodes, cancellationToken);
        cashierSummaries = cashierSummaries
            .Select(item => item with
            {
                CashierName = ResolveCashierName(item.CashierCode, item.CashierName, cashierNames)
            })
            .ToArray();
        receipts = receipts
            .Select(item => item with
            {
                CashierName = ResolveCashierName(item.CashierCode, item.CashierName, cashierNames)
            })
            .ToArray();
        canceledReceipts = canceledReceipts
            .Select(item => item with
            {
                CashierName = ResolveCashierName(item.CashierCode, item.CashierName, cashierNames)
            })
            .ToArray();
        cashSummaryDocuments = cashSummaryDocuments
            .Select(item => item with
            {
                CashierName = ResolveCashierName(item.CashierNo, item.CashierName, cashierNames),
                ManagerName = ResolveCashierName(item.ManagerNo, item.ManagerName, cashierNames)
            })
            .ToArray();

        var branchNames = await GetBranchNamesAsync([request.BranchNo], cancellationToken);
        var branchName = ResolveBranchName(request.BranchNo, movementReport?.BranchName, branchNames);
        comparisonRow = comparisonRow with
        {
            BranchName = branchName
        };

        var receiptNos = receipts
            .Select(item => item.ReceiptNo)
            .Where(item => item > 0)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        var minReceiptNo = receiptNos.Length > 0 ? receiptNos[0] : (int?)null;
        var maxReceiptNo = receiptNos.Length > 0 ? receiptNos[^1] : (int?)null;
        var missingReceiptNos = minReceiptNo.HasValue && maxReceiptNo.HasValue
            ? Enumerable.Range(minReceiptNo.Value, maxReceiptNo.Value - minReceiptNo.Value + 1)
                .Except(receiptNos)
                .ToArray()
            : [];
        var totalReceiptCount = cashierSummaries.Sum(item => item.ReceiptCount);
        var totalLineCount = cashierSummaries.Sum(item => item.LineCount);
        var totalPaymentCount = movementPaymentSummaries.Sum(item => item.PaymentCount);
        var summary = new KasaHareketDetailSummaryDto(
            totalReceiptCount,
            receipts.Length,
            canceledReceipts.Length,
            totalLineCount,
            totalPaymentCount,
            cashSummaryDocuments.Length,
            cashSummaryPayments.Count,
            comparisonRow.MovementNetAmount,
            comparisonRow.MovementExpense,
            comparisonRow.MovementCheckAmount,
            comparisonRow.MovementZReportAmount,
            comparisonRow.CashSummaryAmount,
            comparisonRow.DifferenceAmount,
            minReceiptNo,
            maxReceiptNo,
            missingReceiptNos);

        return new KasaHareketDetailDto(
            request.Date.Date,
            request.BranchNo,
            branchName,
            request.CashRegisterNo,
            movementReport,
            comparisonRow,
            summary,
            cashierSummaries,
            movementPaymentSummaries,
            cashSummaryPayments,
            cashSummaryDocuments,
            receipts,
            canceledReceipts);
    }

    private async Task<IReadOnlyCollection<CashSummarySqlRow>> GetCashSummaryRowsAsync(
        KasaHareketCashSummaryComparisonRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            WITH DistinctSummaries AS (
                SELECT DISTINCT
                    CAST(s.SummaryDate AS date) AS BusinessDate,
                    s.BranchNo,
                    s.CashNo,
                    s.DocumentSerie,
                    s.DocumentOrderNo,
                    s.PaymentTypeID,
                    s.Amount
                FROM dbo.Summaries AS s WITH (NOLOCK)
                WHERE s.SummaryDate >= @date
                  AND s.SummaryDate < @nextDate
                  AND (@branchNo IS NULL OR s.BranchNo = @branchNo)
                  AND (@cashRegisterNo IS NULL OR s.CashNo = @cashRegisterNo)
            )
            SELECT
                BusinessDate,
                BranchNo,
                CashNo AS CashRegisterNo,
                SUM(CASE WHEN PaymentTypeID < 100 OR PaymentTypeID = 500 THEN ISNULL(Amount, 0) ELSE 0 END) AS CashSummaryAmount,
                COUNT(DISTINCT ISNULL(DocumentSerie, N'') + N'/' + CONVERT(nvarchar(20), ISNULL(DocumentOrderNo, 0))) AS DocumentCount
            FROM DistinctSummaries
            GROUP BY
                BusinessDate,
                BranchNo,
                CashNo
            ORDER BY
                BranchNo,
                CashNo;
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
            },
            reader => new CashSummarySqlRow(
                ReadDateTime(reader, "BusinessDate"),
                ReadInt(reader, "BranchNo"),
                ReadInt(reader, "CashRegisterNo"),
                ReadDecimal(reader, "CashSummaryAmount"),
                ReadInt(reader, "DocumentCount")),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<KasaHareketCashierSummaryDto>> GetCashierSummariesAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            WITH ReceiptTotals AS (
                SELECT
                    invoice.FaturaGuid,
                    NULLIF(LTRIM(RTRIM(COALESCE(invoice.KullaniciKodu, N''))), N'') AS CashierCode,
                    SUM(CASE
                        WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN 0
                        ELSE ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                    END) AS NetAmount,
                    SUM(CASE
                        WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                        ELSE 0
                    END) AS Expense
                FROM dbo.PosFaturas AS invoice WITH (NOLOCK)
                WHERE invoice.Tarih >= @date
                  AND invoice.Tarih < @nextDate
                  AND TRY_CONVERT(int, invoice.Sube) = @branchNo
                  AND TRY_CONVERT(int, invoice.KasaNo) = @cashRegisterNo
                GROUP BY
                    invoice.FaturaGuid,
                    NULLIF(LTRIM(RTRIM(COALESCE(invoice.KullaniciKodu, N''))), N'')
            ),
            LineTotals AS (
                SELECT
                    line.FaturaGuid,
                    COUNT(1) AS LineCount
                FROM dbo.PosFaturaSatirs AS line WITH (NOLOCK)
                WHERE line.Tarih >= @date
                  AND line.Tarih < @nextDate
                  AND TRY_CONVERT(int, line.Sube) = @branchNo
                  AND TRY_CONVERT(int, line.KasaKodu) = @cashRegisterNo
                GROUP BY line.FaturaGuid
            ),
            CheckTotals AS (
                SELECT
                    payment.FaturaGuid,
                    SUM(CASE WHEN ISNULL(payment.OdemeTipi, 0) = 4 THEN ISNULL(payment.Tutar, 0) ELSE 0 END) AS CheckAmount
                FROM dbo.PosFaturaOdemes AS payment WITH (NOLOCK)
                WHERE payment.Tarih >= @date
                  AND payment.Tarih < @nextDate
                  AND TRY_CONVERT(int, payment.Sube) = @branchNo
                  AND TRY_CONVERT(int, payment.KasaKodu) = @cashRegisterNo
                GROUP BY payment.FaturaGuid
            )
            SELECT
                COALESCE(receipt.CashierCode, N'') AS CashierCode,
                COUNT(1) AS ReceiptCount,
                SUM(ISNULL(line.LineCount, 0)) AS LineCount,
                SUM(ISNULL(receipt.NetAmount, 0)) AS NetAmount,
                SUM(ISNULL(receipt.Expense, 0)) AS Expense,
                SUM(ISNULL(checks.CheckAmount, 0)) AS CheckAmount,
                SUM(ISNULL(receipt.NetAmount, 0) - ISNULL(receipt.Expense, 0) - ISNULL(checks.CheckAmount, 0)) AS ZReportAmount
            FROM ReceiptTotals AS receipt
            LEFT JOIN LineTotals AS line ON receipt.FaturaGuid = line.FaturaGuid
            LEFT JOIN CheckTotals AS checks ON receipt.FaturaGuid = checks.FaturaGuid
            GROUP BY COALESCE(receipt.CashierCode, N'')
            ORDER BY COALESCE(receipt.CashierCode, N'');
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
            },
            reader => new KasaHareketCashierSummaryDto(
                ReadString(reader, "CashierCode"),
                string.Empty,
                ReadInt(reader, "ReceiptCount"),
                ReadInt(reader, "LineCount"),
                ReadDecimal(reader, "NetAmount"),
                ReadDecimal(reader, "Expense"),
                ReadDecimal(reader, "CheckAmount"),
                ReadDecimal(reader, "ZReportAmount")),
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<KasaHareketPaymentSummaryDto>> GetMovementPaymentSummariesAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            SELECT
                ISNULL(payment.OdemeTipi, 0) AS PaymentType,
                COUNT(1) AS PaymentCount,
                SUM(ISNULL(payment.Tutar, 0)) AS Amount
            FROM dbo.PosFaturaOdemes AS payment WITH (NOLOCK)
            WHERE payment.Tarih >= @date
              AND payment.Tarih < @nextDate
              AND TRY_CONVERT(int, payment.Sube) = @branchNo
              AND TRY_CONVERT(int, payment.KasaKodu) = @cashRegisterNo
            GROUP BY ISNULL(payment.OdemeTipi, 0)
            ORDER BY ISNULL(payment.OdemeTipi, 0);
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
            },
            reader =>
            {
                var paymentType = ReadInt(reader, "PaymentType");
                return new KasaHareketPaymentSummaryDto(
                    paymentType,
                    ResolveMovementPaymentTypeName(paymentType),
                    ReadInt(reader, "PaymentCount"),
                    ReadDecimal(reader, "Amount"));
            },
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<KasaHareketCashSummaryPaymentDto>> GetCashSummaryPaymentsAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            WITH DistinctSummaries AS (
                SELECT DISTINCT
                    s.DocumentSerie,
                    s.DocumentOrderNo,
                    s.PaymentTypeID,
                    s.Amount,
                    s.SlipNumber
                FROM dbo.Summaries AS s WITH (NOLOCK)
                WHERE s.SummaryDate >= @date
                  AND s.SummaryDate < @nextDate
                  AND s.BranchNo = @branchNo
                  AND s.CashNo = @cashRegisterNo
            )
            SELECT
                ds.PaymentTypeID AS PaymentTypeId,
                SUM(ISNULL(ds.SlipNumber, 0)) AS SlipCount,
                SUM(ISNULL(ds.Amount, 0)) AS Amount
            FROM DistinctSummaries AS ds
            GROUP BY ds.PaymentTypeID
            ORDER BY ds.PaymentTypeID;
            """;

        var rows = await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
            },
            reader => new CashSummaryPaymentSource(
                ReadInt(reader, "PaymentTypeId"),
                ReadInt(reader, "SlipCount"),
                ReadDecimal(reader, "Amount")),
            cancellationToken);

        var paymentTypeNos = rows
            .Where(item => item.PaymentTypeId != 500)
            .Select(item => item.PaymentTypeId)
            .Distinct()
            .ToArray();
        var paymentTypes = await furpaDbContext.PaymentTypes
            .AsNoTracking()
            .Where(item => paymentTypeNos.Contains(item.PaymentTypeNo))
            .Select(item => new
            {
                item.PaymentTypeNo,
                PaymentName = item.PaymentName ?? string.Empty,
                AccountCode = item.AccountCode ?? string.Empty
            })
            .ToDictionaryAsync(item => item.PaymentTypeNo, cancellationToken);

        return rows
            .Select(item =>
            {
                paymentTypes.TryGetValue(item.PaymentTypeId, out var paymentType);
                return new KasaHareketCashSummaryPaymentDto(
                    item.PaymentTypeId,
                    item.PaymentTypeId == 500 ? "Nakit" : paymentType?.PaymentName ?? string.Empty,
                    paymentType?.AccountCode ?? string.Empty,
                    item.SlipCount,
                    item.Amount,
                    IsCashSummaryPaymentIncluded(item.PaymentTypeId));
            })
            .ToArray();
    }

    private async Task<IReadOnlyCollection<KasaHareketCashSummaryDocumentDto>> GetCashSummaryDocumentsAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            WITH DistinctSummaries AS (
                SELECT DISTINCT
                    s.DocumentSerie,
                    s.DocumentOrderNo,
                    s.CashNo,
                    s.ZReportNo,
                    s.CashierNo,
                    s.ManagerNo,
                    s.SummaryDate,
                    s.PaymentTypeID,
                    s.Amount,
                    s.CreateDate
                FROM dbo.Summaries AS s WITH (NOLOCK)
                WHERE s.SummaryDate >= @date
                  AND s.SummaryDate < @nextDate
                  AND s.BranchNo = @branchNo
                  AND s.CashNo = @cashRegisterNo
            )
            SELECT
                COALESCE(DocumentSerie, N'') AS DocumentSerie,
                ISNULL(DocumentOrderNo, 0) AS DocumentOrderNo,
                MAX(ISNULL(CashNo, 0)) AS CashNo,
                MAX(ISNULL(ZReportNo, 0)) AS ZReportNo,
                MAX(ISNULL(CashierNo, 0)) AS CashierNo,
                MAX(ISNULL(ManagerNo, 0)) AS ManagerNo,
                MAX(SummaryDate) AS SummaryDate,
                SUM(CASE WHEN PaymentTypeID < 100 OR PaymentTypeID = 500 THEN ISNULL(Amount, 0) ELSE 0 END) AS TotalAmount,
                COUNT(1) AS PaymentLineCount,
                MAX(CreateDate) AS CreateDate
            FROM DistinctSummaries
            GROUP BY
                COALESCE(DocumentSerie, N''),
                ISNULL(DocumentOrderNo, 0)
            ORDER BY
                MAX(SummaryDate),
                COALESCE(DocumentSerie, N''),
                ISNULL(DocumentOrderNo, 0);
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
            },
            reader =>
            {
                var documentSerie = ReadString(reader, "DocumentSerie");
                var documentOrderNo = ReadInt(reader, "DocumentOrderNo");
                return new KasaHareketCashSummaryDocumentDto(
                    documentSerie,
                    documentOrderNo,
                    $"{documentSerie}/{documentOrderNo}",
                    ReadInt(reader, "CashNo"),
                    ReadInt(reader, "ZReportNo"),
                    ReadInt(reader, "CashierNo"),
                    string.Empty,
                    ReadInt(reader, "ManagerNo"),
                    string.Empty,
                    ReadDateTime(reader, "SummaryDate"),
                    ReadDecimal(reader, "TotalAmount"),
                    ReadInt(reader, "PaymentLineCount"),
                    ReadDateTime(reader, "CreateDate"));
            },
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<KasaHareketReceiptDto>> GetReceiptsAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            WITH LineTotals AS (
                SELECT
                    line.FaturaGuid,
                    COUNT(1) AS LineCount
                FROM dbo.PosFaturaSatirs AS line WITH (NOLOCK)
                WHERE line.Tarih >= @date
                  AND line.Tarih < @nextDate
                  AND TRY_CONVERT(int, line.Sube) = @branchNo
                  AND TRY_CONVERT(int, line.KasaKodu) = @cashRegisterNo
                GROUP BY line.FaturaGuid
            ),
            PaymentTotals AS (
                SELECT
                    payment.FaturaGuid,
                    COUNT(1) AS PaymentCount,
                    SUM(CASE WHEN ISNULL(payment.OdemeTipi, 0) = 4 THEN ISNULL(payment.Tutar, 0) ELSE 0 END) AS CheckAmount
                FROM dbo.PosFaturaOdemes AS payment WITH (NOLOCK)
                WHERE payment.Tarih >= @date
                  AND payment.Tarih < @nextDate
                  AND TRY_CONVERT(int, payment.Sube) = @branchNo
                  AND TRY_CONVERT(int, payment.KasaKodu) = @cashRegisterNo
                GROUP BY payment.FaturaGuid
            ),
            PromotionTotals AS (
                SELECT
                    promotion.FaturaGuid,
                    COUNT(1) AS PromotionCount
                FROM dbo.PosFaturaPromosyons AS promotion WITH (NOLOCK)
                WHERE promotion.Tarih >= @date
                  AND promotion.Tarih < @nextDate
                  AND TRY_CONVERT(int, promotion.Sube) = @branchNo
                  AND TRY_CONVERT(int, promotion.KasaNo) = @cashRegisterNo
                GROUP BY promotion.FaturaGuid
            )
            SELECT TOP (@take)
                invoice.FaturaGuid,
                invoice.Tarih,
                invoice.Saat,
                TRY_CONVERT(int, invoice.Sube) AS BranchNo,
                TRY_CONVERT(int, invoice.KasaNo) AS CashRegisterNo,
                TRY_CONVERT(int, invoice.FisNo) AS ReceiptNo,
                COALESCE(invoice.ZNo, N'') AS ZNo,
                ISNULL(invoice.BelgeTuru, 0) AS DocumentKind,
                COALESCE(invoice.KullaniciKodu, N'') AS CashierCode,
                COALESCE(invoice.KartNumarasi, N'') AS CardNumber,
                COALESCE(invoice.MusteriCariKodu, N'') AS CustomerCurrentCode,
                ISNULL(invoice.Toplam, 0) AS GrossAmount,
                ISNULL(invoice.ToplamKdv, 0) AS TaxAmount,
                ISNULL(invoice.FaturaIndirimi, 0) AS DiscountAmount,
                CASE
                    WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN 0
                    ELSE ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                END AS NetAmount,
                CASE
                    WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                    ELSE 0
                END AS ExpenseAmount,
                ISNULL(payment.CheckAmount, 0) AS CheckAmount,
                CASE
                    WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN 0
                    ELSE ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                END
                - CASE
                    WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0)
                    ELSE 0
                END
                - ISNULL(payment.CheckAmount, 0) AS ZReportAmount,
                ISNULL(line.LineCount, 0) AS LineCount,
                ISNULL(payment.PaymentCount, 0) AS PaymentCount,
                ISNULL(promotion.PromotionCount, 0) AS PromotionCount,
                COALESCE(invoice.KasaMaliBellekKodu, N'') AS FiscalMemoryCode,
                COALESCE(invoice.IslemSonuc, N'') AS ProcessResult
            FROM dbo.PosFaturas AS invoice WITH (NOLOCK)
            LEFT JOIN LineTotals AS line ON invoice.FaturaGuid = line.FaturaGuid
            LEFT JOIN PaymentTotals AS payment ON invoice.FaturaGuid = payment.FaturaGuid
            LEFT JOIN PromotionTotals AS promotion ON invoice.FaturaGuid = promotion.FaturaGuid
            WHERE invoice.Tarih >= @date
              AND invoice.Tarih < @nextDate
              AND TRY_CONVERT(int, invoice.Sube) = @branchNo
              AND TRY_CONVERT(int, invoice.KasaNo) = @cashRegisterNo
            ORDER BY
                invoice.Saat,
                TRY_CONVERT(int, invoice.FisNo),
                invoice.FaturaGuid;
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
                AddParameter(command, "@take", request.ReceiptTake);
            },
            reader =>
            {
                var documentKind = Convert.ToByte(ReadInt(reader, "DocumentKind"), CultureInfo.InvariantCulture);
                return new KasaHareketReceiptDto(
                    ReadGuid(reader, "FaturaGuid"),
                    ReadDateTime(reader, "Tarih"),
                    ReadTimeSpan(reader, "Saat"),
                    ReadInt(reader, "BranchNo"),
                    ReadInt(reader, "CashRegisterNo"),
                    ReadInt(reader, "ReceiptNo"),
                    ReadString(reader, "ZNo"),
                    documentKind,
                    ResolveDocumentKindName(documentKind),
                    ReadString(reader, "CashierCode"),
                    string.Empty,
                    ReadString(reader, "CardNumber"),
                    ReadString(reader, "CustomerCurrentCode"),
                    ReadDecimal(reader, "GrossAmount"),
                    ReadDecimal(reader, "TaxAmount"),
                    ReadDecimal(reader, "DiscountAmount"),
                    ReadDecimal(reader, "NetAmount"),
                    ReadDecimal(reader, "ExpenseAmount"),
                    ReadDecimal(reader, "CheckAmount"),
                    ReadDecimal(reader, "ZReportAmount"),
                    ReadInt(reader, "LineCount"),
                    ReadInt(reader, "PaymentCount"),
                    ReadInt(reader, "PromotionCount"),
                    ReadString(reader, "FiscalMemoryCode"),
                    ReadString(reader, "ProcessResult"),
                    0,
                    string.Empty);
            },
            cancellationToken);
    }

    private async Task<IReadOnlyCollection<KasaHareketReceiptDto>> GetCanceledReceiptsAsync(
        KasaHareketDetailRequest request,
        CancellationToken cancellationToken)
    {
        var businessDate = request.Date.Date;
        var nextDate = businessDate.AddDays(1);
        const string sql = """
            WITH LineTotals AS (
                SELECT
                    line.FaturaGuid,
                    COUNT(1) AS LineCount
                FROM dbo.PosFaturaIptalSatirs AS line WITH (NOLOCK)
                WHERE line.Tarih >= @date
                  AND line.Tarih < @nextDate
                  AND TRY_CONVERT(int, line.Sube) = @branchNo
                  AND TRY_CONVERT(int, line.KasaKodu) = @cashRegisterNo
                GROUP BY line.FaturaGuid
            )
            SELECT TOP (@take)
                invoice.FaturaGuid,
                invoice.Tarih,
                invoice.Saat,
                TRY_CONVERT(int, invoice.Sube) AS BranchNo,
                TRY_CONVERT(int, invoice.KasaNo) AS CashRegisterNo,
                TRY_CONVERT(int, invoice.FisNo) AS ReceiptNo,
                COALESCE(invoice.ZNo, N'') AS ZNo,
                ISNULL(invoice.BelgeTuru, 0) AS DocumentKind,
                COALESCE(invoice.KullaniciKodu, N'') AS CashierCode,
                COALESCE(invoice.KartNumarasi, N'') AS CardNumber,
                N'' AS CustomerCurrentCode,
                ISNULL(invoice.Toplam, 0) AS GrossAmount,
                ISNULL(invoice.ToplamKdv, 0) AS TaxAmount,
                ISNULL(invoice.FaturaIndirimi, 0) AS DiscountAmount,
                0 AS NetAmount,
                0 AS ExpenseAmount,
                0 AS CheckAmount,
                0 AS ZReportAmount,
                ISNULL(line.LineCount, 0) AS LineCount,
                0 AS PaymentCount,
                0 AS PromotionCount,
                COALESCE(invoice.KasaMaliBellekKodu, N'') AS FiscalMemoryCode,
                N'' AS ProcessResult,
                ISNULL(invoice.IptalNedeni, 0) AS CancelReason
            FROM dbo.PosFaturaIptals AS invoice WITH (NOLOCK)
            LEFT JOIN LineTotals AS line ON invoice.FaturaGuid = line.FaturaGuid
            WHERE invoice.Tarih >= @date
              AND invoice.Tarih < @nextDate
              AND TRY_CONVERT(int, invoice.Sube) = @branchNo
              AND TRY_CONVERT(int, invoice.KasaNo) = @cashRegisterNo
            ORDER BY
                invoice.Saat,
                TRY_CONVERT(int, invoice.FisNo),
                invoice.FaturaGuid;
            """;

        return await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@date", businessDate);
                AddParameter(command, "@nextDate", nextDate);
                AddParameter(command, "@branchNo", request.BranchNo);
                AddParameter(command, "@cashRegisterNo", request.CashRegisterNo);
                AddParameter(command, "@take", request.ReceiptTake);
            },
            reader =>
            {
                var documentKind = Convert.ToByte(ReadInt(reader, "DocumentKind"), CultureInfo.InvariantCulture);
                var cancelReason = Convert.ToByte(ReadInt(reader, "CancelReason"), CultureInfo.InvariantCulture);
                return new KasaHareketReceiptDto(
                    ReadGuid(reader, "FaturaGuid"),
                    ReadDateTime(reader, "Tarih"),
                    ReadTimeSpan(reader, "Saat"),
                    ReadInt(reader, "BranchNo"),
                    ReadInt(reader, "CashRegisterNo"),
                    ReadInt(reader, "ReceiptNo"),
                    ReadString(reader, "ZNo"),
                    documentKind,
                    ResolveDocumentKindName(documentKind),
                    ReadString(reader, "CashierCode"),
                    string.Empty,
                    ReadString(reader, "CardNumber"),
                    ReadString(reader, "CustomerCurrentCode"),
                    ReadDecimal(reader, "GrossAmount"),
                    ReadDecimal(reader, "TaxAmount"),
                    ReadDecimal(reader, "DiscountAmount"),
                    ReadDecimal(reader, "NetAmount"),
                    ReadDecimal(reader, "ExpenseAmount"),
                    ReadDecimal(reader, "CheckAmount"),
                    ReadDecimal(reader, "ZReportAmount"),
                    ReadInt(reader, "LineCount"),
                    ReadInt(reader, "PaymentCount"),
                    ReadInt(reader, "PromotionCount"),
                    ReadString(reader, "FiscalMemoryCode"),
                    ReadString(reader, "ProcessResult"),
                    cancelReason,
                    ResolveCancelReasonName(cancelReason));
            },
            cancellationToken);
    }

    private async Task<IReadOnlyDictionary<int, string>> GetBranchNamesAsync(
        IEnumerable<int> branchNumbers,
        CancellationToken cancellationToken)
    {
        var branches = branchNumbers
            .Where(item => item > 0)
            .Distinct()
            .ToArray();

        if (branches.Length == 0)
        {
            return new Dictionary<int, string>();
        }

        return await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(item => item.dep_no.HasValue && branches.Contains(item.dep_no.Value))
            .Select(item => new
            {
                BranchNo = item.dep_no ?? 0,
                BranchName = item.dep_adi ?? string.Empty
            })
            .ToDictionaryAsync(item => item.BranchNo, item => item.BranchName, cancellationToken);
    }

    private async Task<KasaHareketImportResultDto> ExecuteImportAsync(
        KasaHareketImportRequest request,
        MovementImportKind kind,
        CancellationToken cancellationToken)
    {
        var (startDate, endDate) = NormalizeDateRange(request.StartDate, request.EndDate);
        var state = new ImportRunState(kind, request.DryRun, startDate);
        var fileRootPath = ResolveFileRootPath(request.FileRootPath);
        var branchNumbers = await ResolveBranchNumbersAsync(request.Branches, cancellationToken);
        var cashRegisters = NormalizeCashRegisterNumbers(request.CashRegisters);

        if (branchNumbers.Count == 0)
        {
            state.AddWarning(null, null, null, null, null, "Aktarim icin sube bulunamadi.");
            return state.ToDto();
        }

        foreach (var date in EnumerateDates(startDate, endDate))
        {
            foreach (var branchNo in branchNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var files = FindFiles(fileRootPath, branchNo, date, cashRegisters, kind, state);

                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    state.ProcessedFiles++;

                    var parsedFile = await ParseFileAsync(filePath, branchNo, kind, cancellationToken);
                    state.AddIssues(parsedFile.Issues);
                    var existingReceiptKeys = request.SkipExisting
                        ? await LoadExistingReceiptKeysAsync(parsedFile.Receipts, kind, cancellationToken)
                        : new HashSet<ReceiptKey>();

                    foreach (var receipt in parsedFile.Receipts)
                    {
                        if (request.SkipExisting &&
                            existingReceiptKeys.Contains(ReceiptKey.From(receipt)))
                        {
                            state.SkippedExistingInvoices++;
                            continue;
                        }

                        if (!request.DryRun)
                        {
                            await InsertReceiptAsync(receipt, cancellationToken);
                        }

                        state.ProcessedInvoices++;
                        state.InsertedLines += receipt.Lines.Count;
                        state.InsertedPayments += receipt.Payments.Count;
                        state.InsertedPromotions += receipt.Promotions.Count;
                    }
                }
            }
        }

        return state.ToDto();
    }

    private async Task<IReadOnlyCollection<int>> ResolveBranchNumbersAsync(
        IReadOnlyCollection<int>? requestedBranches,
        CancellationToken cancellationToken)
    {
        if (requestedBranches is { Count: > 0 })
        {
            var branches = requestedBranches
                .Where(item => item > 0)
                .Distinct()
                .OrderBy(item => item)
                .ToArray();

            if (branches.Length != requestedBranches.Count)
            {
                throw new ArgumentException("Branch numbers must be greater than zero.", nameof(requestedBranches));
            }

            return branches;
        }

        return await furpaDbContext.BranchDetails
            .AsNoTracking()
            .Select(item => item.BranchNo)
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);
    }

    private static IReadOnlyCollection<int> NormalizeCashRegisterNumbers(
        IReadOnlyCollection<int>? cashRegisters)
    {
        if (cashRegisters is not { Count: > 0 })
        {
            return Array.Empty<int>();
        }

        var normalized = cashRegisters
            .Where(item => item >= 0 && item <= 999)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();

        if (normalized.Length != cashRegisters.Count)
        {
            throw new ArgumentException("Cash register numbers must be between 0 and 999.", nameof(cashRegisters));
        }

        return normalized;
    }

    private static IReadOnlyCollection<string> FindFiles(
        string fileRootPath,
        int branchNo,
        DateTime date,
        IReadOnlyCollection<int> cashRegisters,
        MovementImportKind kind,
        ImportRunState state)
    {
        var branchFolder = Path.Combine(fileRootPath, branchNo.ToString(CultureInfo.InvariantCulture));

        if (!Directory.Exists(branchFolder))
        {
            state.AddWarning(branchNo, null, branchFolder, null, null, "Sube klasoru bulunamadi.");
            return Array.Empty<string>();
        }

        var prefix = kind == MovementImportKind.Normal ? NormalPrefix : CancelPrefix;
        var fileName = $"{prefix}{date:ddMMyy}";

        if (cashRegisters.Count == 0)
        {
            try
            {
                return Directory
                    .GetFiles(branchFolder, $"{fileName}.*", SearchOption.TopDirectoryOnly)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                state.AddError(branchNo, null, branchFolder, null, null, exception.Message);
                return Array.Empty<string>();
            }
        }

        var files = new List<string>();
        foreach (var cashRegisterNo in cashRegisters)
        {
            var path = Path.Combine(
                branchFolder,
                $"{fileName}.{cashRegisterNo.ToString("000", CultureInfo.InvariantCulture)}");

            if (File.Exists(path))
            {
                files.Add(path);
                continue;
            }

            state.AddWarning(branchNo, cashRegisterNo, path, null, null, "Kasa hareket dosyasi bulunamadi.");
        }

        return files;
    }

    private async Task<ParsedFile> ParseFileAsync(
        string filePath,
        int branchNo,
        MovementImportKind kind,
        CancellationToken cancellationToken)
    {
        var issues = new List<KasaHareketImportIssueDto>();
        var receipts = new List<ParsedReceipt>();
        var state = new ParserState(branchNo, kind, Path.GetFileName(filePath));
        var lineNo = 0;

        foreach (var rawLine in File.ReadLines(filePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNo++;

            var tokens = SplitRecord(rawLine);
            if (tokens.Length == 0)
            {
                continue;
            }

            var code = ResolveRecordCode(tokens);
            if (code.Length == 0)
            {
                continue;
            }

            try
            {
                switch (code)
                {
                    case "FIS":
                    case "FAT":
                    case "GPS":
                    case "IRS":
                        BeginReceipt(state, code, tokens, lineNo, issues);
                        break;
                    case "TAR":
                        ApplyDateTime(state, tokens);
                        break;
                    case "BAS":
                        ResetReceiptDetails(state);
                        break;
                    case "SAT":
                    case "IPT":
                        BeginLine(state, code, tokens);
                        break;
                    case "BKD":
                        await CompleteLineAsync(state, tokens, lineNo, issues, cancellationToken);
                        break;
                    case "IND":
                        ApplyDiscount(state, tokens);
                        break;
                    case "PRM":
                        AddPromotion(state, tokens);
                        break;
                    case "NAK":
                    case "KRD":
                    case "CEK":
                    case "SDX":
                        AddPayment(state, code, tokens);
                        break;
                    case "FIP":
                        ApplyCancelReason(state, tokens);
                        break;
                    case "SON":
                        CompleteTotals(state, tokens);
                        break;
                    case "MLC":
                        CompleteReceipt(state, tokens, lineNo, receipts, issues);
                        break;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
            {
                state.MarkInvalid();
                issues.Add(state.CreateIssue(lineNo, exception.Message));
            }
        }

        if (state.Current is not null)
        {
            issues.Add(state.CreateIssue(lineNo, "Dosya MLC satiri gelmeden bitti; acik fis kayda alinmadi."));
        }

        return new ParsedFile(receipts, issues);
    }

    private static void BeginReceipt(
        ParserState state,
        string code,
        IReadOnlyList<string> tokens,
        int lineNo,
        ICollection<KasaHareketImportIssueDto> issues)
    {
        if (state.Kind == MovementImportKind.Cancel && code != "FIS")
        {
            issues.Add(state.CreateIssue(lineNo, $"{code} iptal belgesi legacy davranisa uygun olarak kayda alinmadi."));
            state.Clear();
            return;
        }

        var cashAndUser = Token(tokens, 4);
        var receiptAndZNo = Token(tokens, 5);

        var receipt = new ParsedReceipt
        {
            Kind = state.Kind,
            BranchNo = state.BranchNo,
            CreateDate = DateTime.Now,
            UserCode = SafeSubstring(cashAndUser, 4).Trim(),
            ZNo = SafeSubstring(receiptAndZNo, 6).Trim(),
            ReceiptNo = ParseInt(SafeSubstring(receiptAndZNo, 0, Math.Min(6, receiptAndZNo.Length))),
            CashRegisterNo = ParseInt(SafeSubstring(cashAndUser, 0, Math.Min(3, cashAndUser.Length))),
            DocumentKind = MapDocumentKind(code)
        };

        state.Begin(receipt);
    }

    private static void ApplyDateTime(ParserState state, IReadOnlyList<string> tokens)
    {
        var receipt = state.RequireCurrent();
        receipt.Date = ParseDate(Token(tokens, 4));
        receipt.Time = ParseTime(Token(tokens, 5));
    }

    private static void ResetReceiptDetails(ParserState state)
    {
        var receipt = state.RequireCurrent();
        receipt.Lines.Clear();
        receipt.Payments.Clear();
        receipt.Promotions.Clear();
        state.PendingLine = null;
        state.LastLine = null;
    }

    private static void BeginLine(ParserState state, string code, IReadOnlyList<string> tokens)
    {
        var receipt = state.RequireCurrent();
        var quantityToken = Token(tokens, 4);
        var amountToken = Token(tokens, 5);
        var quantity = ParseDecimal(SafeSubstring(quantityToken, 0, Math.Min(6, quantityToken.Length)));
        var grossAmount = ParseMoney(SafeSubstring(amountToken, 2, Math.Max(0, Math.Min(10, amountToken.Length - 2))));

        state.PendingLine = new ParsedLine
        {
            InvoiceGuid = receipt.InvoiceGuid,
            Quantity = quantity,
            GrossAmount = grossAmount,
            UnitPrice = quantity == 0m ? 0m : Round(grossAmount / quantity),
            TaxRate = MapTaxRate(ParseInt(SafeSubstring(amountToken, 0, Math.Min(2, amountToken.Length))), receipt.Date),
            Status = code == "IPT" ? (byte)1 : (byte)0,
            CashRegisterNo = receipt.CashRegisterNo,
            BranchNo = receipt.BranchNo,
            CashierCode = receipt.UserCode,
            Date = receipt.Date,
            Time = receipt.Time,
            CreateDate = receipt.CreateDate
        };
    }

    private async Task CompleteLineAsync(
        ParserState state,
        IReadOnlyList<string> tokens,
        int lineNo,
        ICollection<KasaHareketImportIssueDto> issues,
        CancellationToken cancellationToken)
    {
        var line = state.PendingLine
            ?? throw new InvalidOperationException("BKD satiri oncesinde SAT/IPT satiri bulunmali.");
        var barcodeLeft = NormalizeImportedBarcode(Token(tokens, 4));
        var barcodeRight = NormalizeImportedBarcode(Token(tokens, 5));
        var barcode = barcodeLeft.StartsWith("27", StringComparison.Ordinal) ||
                      barcodeLeft.StartsWith("29", StringComparison.Ordinal)
            ? SafeSubstring(barcodeLeft, 0, Math.Min(7, barcodeLeft.Length)).Trim()
            : NormalizeImportedBarcode(barcodeLeft + barcodeRight);

        line.Barcode = barcode;
        line.ProductCode = await ResolveProductCodeAsync(barcode, cancellationToken) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(line.ProductCode))
        {
            state.MarkInvalid();
            issues.Add(state.CreateIssue(lineNo, $"Sistemde olmayan barkod: {barcode}"));
            state.PendingLine = null;
            return;
        }

        var receipt = state.RequireCurrent();
        receipt.Lines.Add(line);
        state.LastLine = line;
        state.PendingLine = null;
    }

    private static void ApplyDiscount(ParserState state, IReadOnlyList<string> tokens)
    {
        var receipt = state.RequireCurrent();
        var discount = ParseMoney(Token(tokens, 5));

        if (discount == 0m)
        {
            return;
        }

        var discountToken = Token(tokens, 4);
        var isInvoiceDiscount = discountToken.StartsWith("TOP", StringComparison.OrdinalIgnoreCase);

        if (!isInvoiceDiscount)
        {
            var targetLine = state.LastLine ?? receipt.Lines.LastOrDefault();
            if (targetLine is not null)
            {
                ApplyLineDiscount(targetLine, discount);
                receipt.InvoiceDiscount += discount;
                return;
            }
        }

        var candidateLines = receipt.Lines.Where(l => l.Status == 0 && l.GrossAmount > 0m).ToList();
        if (candidateLines.Count == 0)
        {
            candidateLines = receipt.Lines.Where(l => l.GrossAmount > 0m).ToList();
        }

        var totalGross = candidateLines.Sum(l => l.GrossAmount);
        if (candidateLines.Count == 0 || totalGross <= 0m)
        {
            var fallbackLine = state.LastLine ?? receipt.Lines.LastOrDefault();
            if (fallbackLine is not null)
            {
                ApplyLineDiscount(fallbackLine, discount);
            }
            receipt.InvoiceDiscount += discount;
            return;
        }

        var distributedDiscount = 0m;
        for (var i = 0; i < candidateLines.Count; i++)
        {
            var line = candidateLines[i];
            decimal lineDiscount;

            if (i == candidateLines.Count - 1)
            {
                lineDiscount = discount - distributedDiscount;
            }
            else
            {
                lineDiscount = Round(discount * line.GrossAmount / totalGross);
                distributedDiscount += lineDiscount;
            }

            ApplyLineDiscount(line, lineDiscount);
        }

        receipt.InvoiceDiscount += discount;
    }

    private static void AddPromotion(ParserState state, IReadOnlyList<string> tokens)
    {
        if (state.Kind == MovementImportKind.Cancel)
        {
            return;
        }

        var receipt = state.RequireCurrent();
        var line = state.LastLine ?? receipt.Lines.LastOrDefault()
            ?? throw new InvalidOperationException("PRM satiri oncesinde tamamlanmis satir bulunmali.");
        var promotionToken = Token(tokens, 4);

        receipt.Promotions.Add(new ParsedPromotion
        {
            InvoiceGuid = receipt.InvoiceGuid,
            LineGuid = line.LineGuid,
            Barcode = line.Barcode,
            ProductCode = line.ProductCode,
            Amount = ParseMoney(Token(tokens, 5)),
            DiscountType = SafeSubstring(promotionToken, 0, Math.Min(2, promotionToken.Length)),
            PromotionCode = SafeSubstring(promotionToken, 2),
            BranchNo = receipt.BranchNo,
            CashRegisterNo = receipt.CashRegisterNo,
            Date = receipt.Date,
            Time = receipt.Time,
            ReceiptNo = receipt.ReceiptNo,
            CreateDate = DateTime.Now
        });
    }

    private static void AddPayment(ParserState state, string code, IReadOnlyList<string> tokens)
    {
        if (state.Kind == MovementImportKind.Cancel)
        {
            return;
        }

        var receipt = state.RequireCurrent();
        var payment = new ParsedPayment
        {
            InvoiceGuid = receipt.InvoiceGuid,
            PaymentType = code switch
            {
                "NAK" => (byte)1,
                "KRD" => (byte)2,
                "SDX" => (byte)3,
                "CEK" => (byte)4,
                _ => (byte)0
            },
            Amount = code == "CEK"
                ? ParseMoney(SafeSubstring(Token(tokens, 5), 4))
                : ParseMoney(Token(tokens, 5)),
            BankCode = code == "KRD" ? Token(tokens, 4) : string.Empty,
            SdxTypeCode = code == "SDX" ? Token(tokens, 4) : string.Empty,
            CheckNumber = code == "CEK"
                ? Token(tokens, 4) + SafeSubstring(Token(tokens, 5), 0, Math.Min(4, Token(tokens, 5).Length))
                : string.Empty,
            CashRegisterNo = receipt.CashRegisterNo,
            BranchNo = receipt.BranchNo,
            Date = receipt.Date,
            Time = receipt.Time,
            CreateDate = receipt.CreateDate
        };

        receipt.Payments.Add(payment);
    }

    private static void ApplyCancelReason(ParserState state, IReadOnlyList<string> tokens)
    {
        if (state.Kind != MovementImportKind.Cancel)
        {
            return;
        }

        var receipt = state.RequireCurrent();
        receipt.CancelReason = Convert.ToByte(ParseInt(Token(tokens, 4)), CultureInfo.InvariantCulture);

        foreach (var line in receipt.Lines)
        {
            line.CancelReason = receipt.CancelReason;
        }

        if (state.PendingLine is not null)
        {
            state.PendingLine.CancelReason = receipt.CancelReason;
        }
    }

    private static void CompleteTotals(ParserState state, IReadOnlyList<string> tokens)
    {
        var receipt = state.RequireCurrent();
        var customerCode = (Token(tokens, 4) + Token(tokens, 5)).Trim();

        if (customerCode.Length >= 16)
        {
            receipt.CardNumber = customerCode;
        }
        else
        {
            receipt.CustomerCurrentCode = Token(tokens, 4).Trim();
            receipt.ProcessResult = Token(tokens, 5).Trim();
        }

        RecalculateTotals(receipt);
    }

    private static void CompleteReceipt(
        ParserState state,
        IReadOnlyList<string> tokens,
        int lineNo,
        ICollection<ParsedReceipt> receipts,
        ICollection<KasaHareketImportIssueDto> issues)
    {
        var receipt = state.RequireCurrent();
        receipt.FiscalMemoryCode = Token(tokens, 4).Trim();
        RecalculateTotals(receipt);

        if (!state.IsCurrentValid)
        {
            issues.Add(state.CreateIssue(lineNo, "Fis hatali satirlar nedeniyle kayda alinmadi."));
            state.Clear();
            return;
        }

        receipts.Add(receipt);
        state.Clear();
    }

    private async Task<string?> ResolveProductCodeAsync(
        string barcode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        if (barcodeCache.TryGetValue(barcode, out var cachedProductCode))
        {
            return cachedProductCode;
        }

        var productCode = await mikroDbContext.BARKOD_TANIMLARIs
            .AsNoTracking()
            .Where(item => item.bar_kodu == barcode)
            .Select(item => item.bar_stokkodu)
            .FirstOrDefaultAsync(cancellationToken);

        barcodeCache[barcode] = string.IsNullOrWhiteSpace(productCode) ? null : productCode.Trim();
        return barcodeCache[barcode];
    }

    private async Task<HashSet<ReceiptKey>> LoadExistingReceiptKeysAsync(
        IReadOnlyCollection<ParsedReceipt> receipts,
        MovementImportKind kind,
        CancellationToken cancellationToken)
    {
        if (receipts.Count == 0)
        {
            return [];
        }

        var branchNo = receipts.First().BranchNo;
        var cashRegisterNos = receipts
            .Select(item => item.CashRegisterNo)
            .Distinct()
            .ToArray();
        var cashRegisterNo = cashRegisterNos.Length == 1
            ? cashRegisterNos[0]
            : (int?)null;
        var startDate = receipts.Min(item => item.Date.Date);
        var endDate = receipts.Max(item => item.Date.Date).AddDays(1);
        var tableName = kind == MovementImportKind.Normal
            ? "dbo.PosFaturas"
            : "dbo.PosFaturaIptals";

        var sql = $"""
            SELECT
                CAST(Tarih AS date) AS Date,
                TRY_CONVERT(int, Sube) AS BranchNo,
                TRY_CONVERT(int, KasaNo) AS CashRegisterNo,
                TRY_CONVERT(int, FisNo) AS ReceiptNo,
                TRY_CONVERT(int, BelgeTuru) AS DocumentKind
            FROM {tableName} WITH (NOLOCK)
            WHERE Tarih >= @startDate
              AND Tarih < @endDate
              AND TRY_CONVERT(int, Sube) = @branchNo
              AND (@cashRegisterNo IS NULL OR TRY_CONVERT(int, KasaNo) = @cashRegisterNo);
            """;

        var rows = await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate);
                AddParameter(command, "@endDate", endDate);
                AddParameter(command, "@branchNo", branchNo);
                AddParameter(command, "@cashRegisterNo", cashRegisterNo);
            },
            reader => new ReceiptKey(
                ReadDateTime(reader, "Date").Date,
                ReadInt(reader, "BranchNo"),
                ReadInt(reader, "CashRegisterNo"),
                ReadInt(reader, "ReceiptNo"),
                (byte)ReadInt(reader, "DocumentKind")),
            cancellationToken);

        return rows.ToHashSet();
    }

    private async Task InsertReceiptAsync(
        ParsedReceipt receipt,
        CancellationToken cancellationToken)
    {
        var connection = furpaDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (receipt.Kind == MovementImportKind.Normal)
            {
                await InsertNormalReceiptAsync(connection, transaction, receipt, cancellationToken);
            }
            else
            {
                await InsertCancelReceiptAsync(connection, transaction, receipt, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task InsertNormalReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        ParsedReceipt receipt,
        CancellationToken cancellationToken)
    {
        const string insertInvoiceSql = """
            INSERT INTO dbo.PosFaturas (
                FaturaGuid, Sube, Tarih, Saat, BelgeTuru, ZNo, FisNo, KasaNo,
                KullaniciKodu, KartNumarasi, Toplam, ToplamKdv, FaturaIndirimi,
                VergiMatrahi18, VergiMatrahi08, VergiMatrahi01, VergiMatrahi00,
                VergiToplami18, VergiToplami08, VergiToplami01, VergiToplami00,
                createDate, KasaMaliBellekKodu, MusteriCariKodu, IslemSonuc,
                MikroAktarimDurumu)
            VALUES (
                @FaturaGuid, @Sube, @Tarih, @Saat, @BelgeTuru, @ZNo, @FisNo, @KasaNo,
                @KullaniciKodu, @KartNumarasi, @Toplam, @ToplamKdv, @FaturaIndirimi,
                @VergiMatrahi18, @VergiMatrahi08, @VergiMatrahi01, @VergiMatrahi00,
                @VergiToplami18, @VergiToplami08, @VergiToplami01, @VergiToplami00,
                @createDate, @KasaMaliBellekKodu, @MusteriCariKodu, @IslemSonuc, 0);
            """;

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            insertInvoiceSql,
            cancellationToken,
            CreateReceiptParameters(receipt));

        foreach (var line in receipt.Lines)
        {
            await InsertLineAsync(connection, transaction, "dbo.PosFaturaSatirs", line, cancellationToken);
        }

        foreach (var payment in receipt.Payments)
        {
            await InsertPaymentAsync(connection, transaction, payment, receipt.FiscalMemoryCode, cancellationToken);
        }

        foreach (var promotion in receipt.Promotions)
        {
            await InsertPromotionAsync(connection, transaction, promotion, cancellationToken);
        }
    }

    private static async Task InsertCancelReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        ParsedReceipt receipt,
        CancellationToken cancellationToken)
    {
        const string insertInvoiceSql = """
            INSERT INTO dbo.PosFaturaIptals (
                FaturaGuid, Sube, Tarih, Saat, BelgeTuru, ZNo, FisNo, KasaNo,
                KullaniciKodu, KartNumarasi, Toplam, ToplamKdv, FaturaIndirimi,
                VergiMatrahi18, VergiMatrahi08, VergiMatrahi01, VergiMatrahi00,
                VergiToplami18, VergiToplami08, VergiToplami01, VergiToplami00,
                createDate, KasaMaliBellekKodu, IptalNedeni)
            VALUES (
                @FaturaGuid, @Sube, @Tarih, @Saat, @BelgeTuru, @ZNo, @FisNo, @KasaNo,
                @KullaniciKodu, @KartNumarasi, @Toplam, @ToplamKdv, @FaturaIndirimi,
                @VergiMatrahi18, @VergiMatrahi08, @VergiMatrahi01, @VergiMatrahi00,
                @VergiToplami18, @VergiToplami08, @VergiToplami01, @VergiToplami00,
                @createDate, @KasaMaliBellekKodu, @IptalNedeni);
            """;

        var parameters = CreateReceiptParameters(receipt).ToList();
        parameters.Add(("@IptalNedeni", receipt.CancelReason));

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            insertInvoiceSql,
            cancellationToken,
            parameters.ToArray());

        foreach (var line in receipt.Lines)
        {
            await InsertLineAsync(connection, transaction, "dbo.PosFaturaIptalSatirs", line, cancellationToken);
        }
    }

    private static async Task InsertLineAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        ParsedLine line,
        CancellationToken cancellationToken)
    {
        var hasCancelReason = string.Equals(tableName, "dbo.PosFaturaIptalSatirs", StringComparison.OrdinalIgnoreCase);
        var sql = hasCancelReason
            ? """
                INSERT INTO dbo.PosFaturaIptalSatirs (
                    SatirGuid, FaturaGuid, UrunKodu, Miktar, BirimFiyat, NetTutar,
                    Barkod, Tarih, Saat, KdvOrani, KdvTutari, Durum, KasaKodu,
                    Sube, FaturaAltiIndirimi, KasiyerKodu, createDate, IptalNedeni)
                VALUES (
                    @SatirGuid, @FaturaGuid, @UrunKodu, @Miktar, @BirimFiyat, @NetTutar,
                    @Barkod, @Tarih, @Saat, @KdvOrani, @KdvTutari, @Durum, @KasaKodu,
                    @Sube, @FaturaAltiIndirimi, @KasiyerKodu, @createDate, @IptalNedeni);
                """
            : """
                INSERT INTO dbo.PosFaturaSatirs (
                    SatirGuid, FaturaGuid, UrunKodu, Miktar, BirimFiyat, NetTutar,
                    Barkod, Tarih, Saat, KdvOrani, KdvTutari, Durum, KasaKodu,
                    Sube, FaturaAltiIndirimi, KasiyerKodu, createDate)
                VALUES (
                    @SatirGuid, @FaturaGuid, @UrunKodu, @Miktar, @BirimFiyat, @NetTutar,
                    @Barkod, @Tarih, @Saat, @KdvOrani, @KdvTutari, @Durum, @KasaKodu,
                    @Sube, @FaturaAltiIndirimi, @KasiyerKodu, @createDate);
                """;

        var parameters = new List<(string Name, object? Value)>
        {
            ("@SatirGuid", line.LineGuid),
            ("@FaturaGuid", line.InvoiceGuid),
            ("@UrunKodu", line.ProductCode),
            ("@Miktar", line.Quantity),
            ("@BirimFiyat", line.UnitPrice),
            ("@NetTutar", line.NetAmount),
            ("@Barkod", line.Barcode),
            ("@Tarih", line.Date.Date),
            ("@Saat", line.Time),
            ("@KdvOrani", line.TaxRate),
            ("@KdvTutari", line.TaxAmount),
            ("@Durum", line.Status),
            ("@KasaKodu", line.CashRegisterNo),
            ("@Sube", line.BranchNo),
            ("@FaturaAltiIndirimi", line.Discount),
            ("@KasiyerKodu", line.CashierCode),
            ("@createDate", line.CreateDate)
        };

        if (hasCancelReason)
        {
            parameters.Add(("@IptalNedeni", line.CancelReason));
        }

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            cancellationToken,
            parameters.ToArray());
    }

    private static async Task InsertPaymentAsync(
        DbConnection connection,
        DbTransaction transaction,
        ParsedPayment payment,
        string fiscalMemoryCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.PosFaturaOdemes (
                OdemeGuid, FaturaGuid, OdemeTipi, Tutar, BankaKodu, KasaKodu,
                Sube, Tarih, Saat, SdxTipKodu, CekNumarasi, createDate,
                KasaMaliBellekKodu)
            VALUES (
                @OdemeGuid, @FaturaGuid, @OdemeTipi, @Tutar, @BankaKodu, @KasaKodu,
                @Sube, @Tarih, @Saat, @SdxTipKodu, @CekNumarasi, @createDate,
                @KasaMaliBellekKodu);
            """;

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            cancellationToken,
            ("@OdemeGuid", payment.PaymentGuid),
            ("@FaturaGuid", payment.InvoiceGuid),
            ("@OdemeTipi", payment.PaymentType),
            ("@Tutar", payment.Amount),
            ("@BankaKodu", payment.BankCode),
            ("@KasaKodu", payment.CashRegisterNo),
            ("@Sube", payment.BranchNo),
            ("@Tarih", payment.Date.Date),
            ("@Saat", payment.Time),
            ("@SdxTipKodu", payment.SdxTypeCode),
            ("@CekNumarasi", payment.CheckNumber),
            ("@createDate", payment.CreateDate),
            ("@KasaMaliBellekKodu", fiscalMemoryCode));
    }

    private static async Task InsertPromotionAsync(
        DbConnection connection,
        DbTransaction transaction,
        ParsedPromotion promotion,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO dbo.PosFaturaPromosyons (
                FaturaGuid, SatirGuid, Barkod, UrunKod, Tutar, IndirimTip,
                PromosyonKod, Sube, KasaNo, Tarih, Saat, FisNo, CreateDate)
            VALUES (
                @FaturaGuid, @SatirGuid, @Barkod, @UrunKod, @Tutar, @IndirimTip,
                @PromosyonKod, @Sube, @KasaNo, @Tarih, @Saat, @FisNo, @CreateDate);
            """;

        await ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            cancellationToken,
            ("@FaturaGuid", promotion.InvoiceGuid),
            ("@SatirGuid", promotion.LineGuid),
            ("@Barkod", promotion.Barcode),
            ("@UrunKod", promotion.ProductCode),
            ("@Tutar", promotion.Amount),
            ("@IndirimTip", promotion.DiscountType),
            ("@PromosyonKod", promotion.PromotionCode),
            ("@Sube", promotion.BranchNo),
            ("@KasaNo", promotion.CashRegisterNo),
            ("@Tarih", promotion.Date.Date),
            ("@Saat", promotion.Time),
            ("@FisNo", promotion.ReceiptNo),
            ("@CreateDate", promotion.CreateDate));
    }

    private async Task PreparePendingMikroTransferAsync(
        DateTime startDate,
        DateTime endDate,
        int? branchNo,
        bool requireExclusiveBranch,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.PosFaturas
            SET MikroAktarimDurumu = 0
            WHERE Tarih >= @startDate
              AND Tarih < @endDateExclusive
              AND MikroAktarimDurumu IS NULL
              AND (@branchNo IS NULL OR TRY_CONVERT(int, Sube) = @branchNo);

            SELECT DISTINCT TRY_CONVERT(int, Sube) AS BranchNo
            FROM dbo.PosFaturas WITH (READCOMMITTEDLOCK)
            WHERE Tarih >= @startDate
              AND Tarih < @endDateExclusive
              AND ISNULL(MikroAktarimDurumu, 0) = 0
              AND TRY_CONVERT(int, Sube) IS NOT NULL
              AND (@branchNo IS NULL OR TRY_CONVERT(int, Sube) = @branchNo)
            ORDER BY BranchNo;
            """;

        var pendingBranchNos = await ExecuteReaderAsync(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate.Date);
                AddParameter(command, "@endDateExclusive", endDate.Date.AddDays(1));
                AddParameter(command, "@branchNo", branchNo);
            },
            reader => ReadInt(reader, "BranchNo"),
            cancellationToken);

        EnsureExclusivePendingBranch(branchNo, requireExclusiveBranch, pendingBranchNos);
        await ValidateMikroPosDocumentSequencesAsync(pendingBranchNos, cancellationToken);
    }

    private async Task ValidateMikroPosDocumentSequencesAsync(
        IReadOnlyCollection<int> pendingBranchNos,
        CancellationToken cancellationToken)
    {
        if (pendingBranchNos.Count == 0)
        {
            return;
        }

        const string sql = """
            SELECT
                @branchNo AS BranchNo,
                ISNULL(stockDocument.LastDocumentNo, 0) AS LastStockDocumentNo,
                ISNULL(customerDocument.LastDocumentNo, 0) AS LastCustomerDocumentNo,
                stockDocument.LastDocumentDate AS LastStockDocumentDate,
                customerDocument.LastDocumentDate AS LastCustomerDocumentDate
            FROM (VALUES (1)) AS seed(Value)
            OUTER APPLY (
                SELECT TOP (1)
                    movement.sth_evrakno_sira AS LastDocumentNo,
                    CAST(movement.sth_tarih AS date) AS LastDocumentDate
                FROM dbo.STOK_HAREKETLERI AS movement
                    WITH (NOLOCK, INDEX(NDX_STOK_HAREKETLERI_05))
                WHERE movement.sth_evraktip = 4
                  AND movement.sth_evrakno_seri = @series
                  AND movement.sth_pos_satis = 1
                ORDER BY movement.sth_evrakno_sira DESC, movement.sth_satirno DESC
            ) AS stockDocument
            OUTER APPLY (
                SELECT TOP (1)
                    movement.cha_evrakno_sira AS LastDocumentNo,
                    CAST(movement.cha_belge_tarih AS date) AS LastDocumentDate
                FROM dbo.CARI_HESAP_HAREKETLERI AS movement
                    WITH (NOLOCK, INDEX(NDX_CARI_HESAP_HAREKETLERI_04))
                WHERE movement.cha_evrak_tip = 63
                  AND movement.cha_evrakno_seri = @series
                  AND movement.cha_pos_hareketi = 1
                ORDER BY movement.cha_evrakno_sira DESC, movement.cha_satir_no DESC
            ) AS customerDocument;
            """;

        var sequences = new List<MikroPosDocumentSequence>();
        foreach (var branchNo in pendingBranchNos.Distinct().OrderBy(item => item))
        {
            var branchSequence = await ExecuteReaderAsync(
                mikroDbContext.Database.GetDbConnection(),
                sql,
                command =>
                {
                    AddParameter(command, "@branchNo", branchNo);
                    AddParameter(command, "@series", $"PS{branchNo}");
                },
                reader => new MikroPosDocumentSequence(
                    ReadInt(reader, "BranchNo"),
                    ReadInt(reader, "LastStockDocumentNo"),
                    ReadInt(reader, "LastCustomerDocumentNo"),
                    ReadNullableDateTime(reader, "LastStockDocumentDate"),
                    ReadNullableDateTime(reader, "LastCustomerDocumentDate")),
                cancellationToken,
                commandTimeoutSeconds: 15);
            sequences.AddRange(branchSequence);
        }

        EnsureMikroPosDocumentSequencesAreSafe(sequences);
    }

    internal static void EnsureMikroPosDocumentSequencesAreSafe(
        IReadOnlyCollection<MikroPosDocumentSequence> sequences)
    {
        var mismatches = sequences
            .Where(item => item.LastStockDocumentNo > item.LastCustomerDocumentNo)
            .OrderBy(item => item.BranchNo)
            .ToArray();
        if (mismatches.Length == 0)
        {
            return;
        }

        var details = string.Join(
            "; ",
            mismatches.Select(item =>
                $"sube {item.BranchNo}: stok PS{item.BranchNo}/{item.LastStockDocumentNo}" +
                FormatDocumentDate(item.LastStockDocumentDate) +
                $", cari PS{item.BranchNo}/{item.LastCustomerDocumentNo}" +
                FormatDocumentDate(item.LastCustomerDocumentDate)));

        throw new OperationConflictException(
            OperationConflictErrorCodes.MikroPosDocumentSequenceConflict,
            $"Mikro POS evrak siralari uyumsuz oldugu icin aktarim baslatilmadi. {details}. " +
            "Mevcut Mikro evraklarina dokunulmadi. Bu aktarimi tekrar baslatmayin; " +
            "eksik cari evraki ve onceki silme isleminin tarihini kontrol edin.",
            retryable: false);
    }

    private static string FormatDocumentDate(DateTime? value) =>
        value.HasValue ? $" ({value.Value:dd.MM.yyyy})" : string.Empty;

    internal static void EnsureExclusivePendingBranch(
        int? requestedBranchNo,
        bool requireExclusiveBranch,
        IReadOnlyCollection<int> pendingBranchNos)
    {
        if (!requireExclusiveBranch || requestedBranchNo is not > 0)
        {
            return;
        }

        if (!pendingBranchNos.Contains(requestedBranchNo.Value))
        {
            return;
        }

        var otherBranchNos = pendingBranchNos
            .Where(item => item != requestedBranchNo.Value)
            .Distinct()
            .OrderBy(item => item)
            .ToArray();
        if (otherBranchNos.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{requestedBranchNo.Value} subesi tek basina aktarilamadi. " +
            $"Ayni tarih araliginda bekleyen diger subeler: {string.Join(", ", otherBranchNos)}. " +
            "Tum bekleyen subeleri birlikte islemek icin Mikroya Aktar 2 islemini kullanin.");
    }

    private async Task<IReadOnlyCollection<MikroPosDocumentSnapshot>> GetMikroPosDocumentsAsync(
        DateTime startDate,
        DateTime endDate,
        int? branchNo,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                @stockDocumentFlowType AS DocumentFlowType,
                CAST(movement.sth_tarih AS date) AS DocumentDate,
                ISNULL(movement.sth_cikis_depo_no, 0) AS BranchNo,
                LTRIM(RTRIM(ISNULL(movement.sth_evrakno_seri, ''))) AS DocumentSerie,
                ISNULL(movement.sth_evrakno_sira, 0) AS DocumentOrderNo,
                COUNT_BIG(*) AS LineCount
            FROM dbo.STOK_HAREKETLERI AS movement WITH (READUNCOMMITTED)
            WHERE movement.sth_tarih >= @startDate
              AND movement.sth_tarih < @endDateExclusive
              AND movement.sth_pos_satis = 1
              AND movement.sth_evraktip IN (@retailStockDocumentType, @expenseStockDocumentType)
              AND movement.sth_evrakno_seri LIKE 'PS%'
              AND movement.sth_evrakno_seri NOT LIKE 'PSF%'
              AND movement.sth_cikis_depo_no > 100
              AND NULLIF(LTRIM(RTRIM(ISNULL(movement.sth_evrakno_seri, ''))), '') IS NOT NULL
              AND ISNULL(movement.sth_evrakno_sira, 0) > 0
              AND (@branchNo IS NULL OR movement.sth_cikis_depo_no = @branchNo)
            GROUP BY
                CAST(movement.sth_tarih AS date),
                movement.sth_cikis_depo_no,
                LTRIM(RTRIM(ISNULL(movement.sth_evrakno_seri, ''))),
                movement.sth_evrakno_sira

            UNION ALL

            SELECT
                @customerDocumentFlowType AS DocumentFlowType,
                CAST(movement.cha_tarihi AS date) AS DocumentDate,
                CONVERT(int, movement.cha_create_user) AS BranchNo,
                LTRIM(RTRIM(ISNULL(movement.cha_evrakno_seri, ''))) AS DocumentSerie,
                ISNULL(movement.cha_evrakno_sira, 0) AS DocumentOrderNo,
                COUNT_BIG(*) AS LineCount
            FROM dbo.CARI_HESAP_HAREKETLERI AS movement WITH (READUNCOMMITTED)
            WHERE movement.cha_tarihi >= @startDate
              AND movement.cha_tarihi < @endDateExclusive
              AND movement.cha_evrak_tip IN (@salesCustomerDocumentType, @expenseCustomerDocumentType)
              AND movement.cha_pos_hareketi IN (0, 1)
              AND movement.cha_evrakno_seri LIKE 'PS%'
              AND movement.cha_evrakno_seri NOT LIKE 'PSF%'
              AND CONVERT(int, movement.cha_create_user) > 100
              AND NULLIF(LTRIM(RTRIM(ISNULL(movement.cha_evrakno_seri, ''))), '') IS NOT NULL
              AND ISNULL(movement.cha_evrakno_sira, 0) > 0
              AND (@branchNo IS NULL OR CONVERT(int, movement.cha_create_user) = @branchNo)
            GROUP BY
                CAST(movement.cha_tarihi AS date),
                movement.cha_create_user,
                LTRIM(RTRIM(ISNULL(movement.cha_evrakno_seri, ''))),
                movement.cha_evrakno_sira;
            """;

        return await ExecuteReaderAsync(
            mikroDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@startDate", startDate.Date);
                AddParameter(command, "@endDateExclusive", endDate.Date.AddDays(1));
                AddParameter(command, "@branchNo", branchNo);
                AddParameter(command, "@stockDocumentFlowType", (int)DocumentFlowType.StockMovementDocument);
                AddParameter(command, "@customerDocumentFlowType", (int)DocumentFlowType.CustomerMovementDocument);
                AddParameter(command, "@retailStockDocumentType", PosRetailStockDocumentType);
                AddParameter(command, "@expenseStockDocumentType", PosExpenseStockDocumentType);
                AddParameter(command, "@salesCustomerDocumentType", PosSalesCustomerDocumentType);
                AddParameter(command, "@expenseCustomerDocumentType", PosExpenseCustomerDocumentType);
            },
            reader => new MikroPosDocumentSnapshot(
                new MikroPosDocumentKey(
                    (DocumentFlowType)ReadInt(reader, "DocumentFlowType"),
                    ReadDateTime(reader, "DocumentDate").Date,
                    ReadInt(reader, "BranchNo"),
                    ReadString(reader, "DocumentSerie"),
                    ReadInt(reader, "DocumentOrderNo")),
                checked((int)ReadInt64(reader, "LineCount"))),
            cancellationToken);
    }

    private async Task RecordMikroDocumentChangesAsync(
        IReadOnlyCollection<MikroPosDocumentSnapshot> documents,
        DocumentFlowStep step,
        string operationMessage,
        CancellationToken cancellationToken)
    {
        var requests = documents.OrderBy(item => item.Key.DocumentType)
            .ThenBy(item => item.Key.BranchNo)
            .ThenBy(item => item.Key.Date)
            .ThenBy(item => item.Key.DocumentSerie)
            .ThenBy(item => item.Key.DocumentOrderNo)
            .Select(document =>
            {
                var key = document.Key;
                var documentNo = $"{key.DocumentSerie}/{key.DocumentOrderNo}";
                var documentTypeName = key.DocumentType == DocumentFlowType.CustomerMovementDocument
                    ? "Cari hesap hareket evraki"
                    : "Stok hareket evraki";
                var message = $"{operationMessage} {documentTypeName}. Tarih: {key.Date:dd.MM.yyyy}, " +
                              $"sube: {key.BranchNo}, evrak: {documentNo}, " +
                              $"satir: {document.LineCount}.";

                return new RecordDocumentFlowRequest(
                    DocumentFlowKeys.CreateEntity(
                        key.DocumentType,
                        key.BranchNo,
                        "POS",
                        $"{key.Date:yyyyMMdd}:{key.DocumentSerie}:{key.DocumentOrderNo}"),
                    key.DocumentType,
                    key.BranchNo,
                    null,
                    key.DocumentSerie,
                    key.DocumentOrderNo,
                    step,
                    DocumentFlowStatus.Succeeded,
                    message,
                    DocumentNo: documentNo);
            })
            .ToArray();

        await documentFlowService.RecordManyAsync(requests, cancellationToken);
    }

    private async Task<string> ExecuteProcedureAsync(
        string procedureName,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = furpaDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;
        var mutationLockAcquired = false;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (RequiresExclusiveMutationLock(procedureName))
            {
                await AcquireMikroMutationLockAsync(connection, cancellationToken);
                mutationLockAcquired = true;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = ResolveProcedureCommandTimeoutSeconds(procedureName);

            foreach (var parameter in parameters)
            {
                AddParameter(command, parameter.Name, parameter.Value);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    if (!string.Equals(reader.GetName(index), "_Message_", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(reader.GetName(index), "Message", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    return reader.IsDBNull(index)
                        ? string.Empty
                        : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
                }
            }

            return $"{procedureName} calisti.";
        }
        catch (SqlException exception) when (
            string.Equals(procedureName, StockMovementWriteProcedureName, StringComparison.OrdinalIgnoreCase) &&
            exception.Number is 2601 or 2627)
        {
            throw new OperationConflictException(
                OperationConflictErrorCodes.MikroPosDocumentSequenceConflict,
                $"Mikro POS evrak numarasi baska bir kayitla cakisti ({exception.Message.Trim()}). Ayni aktarimi yeniden calistirmadan " +
                "once stok ve cari evraklarini kontrol edin.",
                retryable: false,
                exception);
        }
        catch (SqlException exception) when (
            RequiresExclusiveMutationLock(procedureName) && exception.Number == 1205)
        {
            throw new OperationConflictException(
                OperationConflictErrorCodes.MikroWriteOutcomeUnconfirmed,
                OperationConflictMessages.MikroWriteOutcomeUnconfirmed,
                retryable: true,
                exception);
        }
        finally
        {
            if (mutationLockAcquired)
            {
                await ReleaseMikroMutationLockAsync(connection);
            }

            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static bool RequiresExclusiveMutationLock(string procedureName) =>
        procedureName.Equals("HareketSil", StringComparison.OrdinalIgnoreCase) ||
        procedureName.Equals(StockMovementWriteProcedureName, StringComparison.OrdinalIgnoreCase) ||
        procedureName.Equals("StokHareketSil", StringComparison.OrdinalIgnoreCase) ||
        procedureName.Equals("StokHareketSilTarihAralikli", StringComparison.OrdinalIgnoreCase);

    private static async Task AcquireMikroMutationLockAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = @lockTimeout;
            SELECT @result;
            """;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 40;
        AddParameter(command, "@resource", MikroMutationLockResource);
        AddParameter(command, "@lockTimeout", MikroMutationLockTimeoutMilliseconds);

        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new OperationConflictException(
                OperationConflictErrorCodes.MikroWriteQueueBusy,
                OperationConflictMessages.MikroWriteQueueBusy,
                retryable: true);
        }
    }

    private static async Task ReleaseMikroMutationLockAsync(DbConnection connection)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                EXEC sys.sp_releaseapplock
                    @Resource = @resource,
                    @LockOwner = 'Session';
                """;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 15;
            AddParameter(command, "@resource", MikroMutationLockResource);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (DbException)
        {
            // Closing the session below releases a session-owned application lock as a fallback.
            if (connection.State != ConnectionState.Closed)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static int ResolveProcedureCommandTimeoutSeconds(string procedureName) =>
        string.Equals(procedureName, StockMovementWriteProcedureName, StringComparison.OrdinalIgnoreCase)
            ? StockMovementWriteCommandTimeoutSeconds
            : DefaultProcedureCommandTimeoutSeconds;

    private static async Task<int> ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 300;

        foreach (var parameter in parameters)
        {
            AddParameter(command, parameter.Name, parameter.Value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        DbConnection connection,
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
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
            command.CommandTimeout = 300;
            configureCommand(command);

            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value is null || value == DBNull.Value
                ? default!
                : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlyCollection<T>> ExecuteReaderAsync<T>(
        DbConnection connection,
        string sql,
        Action<DbCommand> configureCommand,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken,
        int commandTimeoutSeconds = 300)
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
            command.CommandTimeout = commandTimeoutSeconds;
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

    private static (string Name, object? Value)[] CreateReceiptParameters(ParsedReceipt receipt) =>
    [
        ("@FaturaGuid", receipt.InvoiceGuid),
        ("@Sube", receipt.BranchNo),
        ("@Tarih", receipt.Date.Date),
        ("@Saat", receipt.Time),
        ("@BelgeTuru", receipt.DocumentKind),
        ("@ZNo", receipt.ZNo),
        ("@FisNo", receipt.ReceiptNo),
        ("@KasaNo", receipt.CashRegisterNo),
        ("@KullaniciKodu", receipt.UserCode),
        ("@KartNumarasi", receipt.CardNumber),
        ("@Toplam", receipt.Total),
        ("@ToplamKdv", receipt.TotalTax),
        ("@FaturaIndirimi", receipt.InvoiceDiscount),
        ("@VergiMatrahi18", receipt.TaxBase18),
        ("@VergiMatrahi08", receipt.TaxBase08),
        ("@VergiMatrahi01", receipt.TaxBase01),
        ("@VergiMatrahi00", receipt.TaxBase00),
        ("@VergiToplami18", receipt.TaxTotal18),
        ("@VergiToplami08", receipt.TaxTotal08),
        ("@VergiToplami01", receipt.TaxTotal01),
        ("@VergiToplami00", receipt.TaxTotal00),
        ("@createDate", receipt.CreateDate),
        ("@KasaMaliBellekKodu", receipt.FiscalMemoryCode),
        ("@MusteriCariKodu", receipt.CustomerCurrentCode),
        ("@IslemSonuc", receipt.ProcessResult)
    ];

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void ApplyLineDiscount(ParsedLine line, decimal discount)
    {
        line.Discount += discount;
        line.GrossAmount = Math.Max(0m, line.GrossAmount - discount);
        line.UnitPrice = line.Quantity == 0m ? 0m : Round(line.GrossAmount / line.Quantity);
    }

    private static void RecalculateTotals(ParsedReceipt receipt)
    {
        receipt.Total = 0m;
        receipt.TotalTax = 0m;
        receipt.TaxBase18 = 0m;
        receipt.TaxBase08 = 0m;
        receipt.TaxBase01 = 0m;
        receipt.TaxBase00 = 0m;
        receipt.TaxTotal18 = 0m;
        receipt.TaxTotal08 = 0m;
        receipt.TaxTotal01 = 0m;
        receipt.TaxTotal00 = 0m;

        foreach (var line in receipt.Lines)
        {
            var divisor = 1m + (line.TaxRate / 100m);
            var netAmount = line.TaxRate == 0 ? line.GrossAmount : Round(line.GrossAmount / divisor);
            var taxAmount = Round(line.GrossAmount - netAmount);
            var factor = line.Status == 1 ? -1m : 1m;

            line.NetAmount = netAmount;
            line.TaxAmount = taxAmount;

            receipt.Total += netAmount * factor;
            receipt.TotalTax += taxAmount * factor;

            switch (line.TaxRate)
            {
                case 20:
                case 18:
                    receipt.TaxBase18 += netAmount * factor;
                    receipt.TaxTotal18 += taxAmount * factor;
                    break;
                case 10:
                case 8:
                    receipt.TaxBase08 += netAmount * factor;
                    receipt.TaxTotal08 += taxAmount * factor;
                    break;
                case 1:
                    receipt.TaxBase01 += netAmount * factor;
                    receipt.TaxTotal01 += taxAmount * factor;
                    break;
                default:
                    receipt.TaxBase00 += netAmount * factor;
                    receipt.TaxTotal00 += taxAmount * factor;
                    break;
            }
        }

        receipt.Total = Round(receipt.Total);
        receipt.TotalTax = Round(receipt.TotalTax);
    }

    private static string[] SplitRecord(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return Array.Empty<string>();
        }

        var separator = rawLine.Contains(';', StringComparison.Ordinal)
            ? ';'
            : rawLine.Contains(',', StringComparison.Ordinal)
                ? ','
                : rawLine.Contains('\t', StringComparison.Ordinal) ? '\t' : ' ';

        return rawLine
            .Split(separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string ResolveRecordCode(IReadOnlyCollection<string> tokens)
    {
        foreach (var token in tokens)
        {
            var normalized = token.Trim().ToUpperInvariant();
            if (KnownRecordCodes.Contains(normalized))
            {
                return normalized;
            }
        }

        return string.Empty;
    }

    private static string Token(IReadOnlyList<string> tokens, int index) =>
        index >= 0 && index < tokens.Count ? tokens[index].Trim() : string.Empty;

    internal static string NormalizeImportedBarcode(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = new StringBuilder(Math.Min(value.Length, 64));
        foreach (var character in value)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                continue;
            }

            normalized.Append(character);
            if (normalized.Length == 64)
            {
                break;
            }
        }

        return normalized.ToString();
    }

    internal static string SanitizeImportIssueMessage(string? message)
    {
        const int maxLength = 500;
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Bilinmeyen aktarim hatasi.";
        }

        var sanitized = new StringBuilder(Math.Min(message.Length, maxLength));
        var pendingSpace = false;
        var truncated = false;

        foreach (var character in message)
        {
            if (char.IsControl(character))
            {
                pendingSpace |= char.IsWhiteSpace(character);
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = sanitized.Length > 0;
                continue;
            }

            if (pendingSpace && sanitized.Length < maxLength)
            {
                sanitized.Append(' ');
            }

            pendingSpace = false;
            if (sanitized.Length == maxLength)
            {
                truncated = true;
                break;
            }

            sanitized.Append(character);
        }

        var result = sanitized.ToString().Trim();
        if (result.Length == 0)
        {
            return "Bilinmeyen aktarim hatasi.";
        }

        return truncated ? result.TrimEnd() + "..." : result;
    }

    private static string SafeSubstring(string value, int startIndex) =>
        startIndex >= value.Length ? string.Empty : value[startIndex..];

    private static string SafeSubstring(string value, int startIndex, int length)
    {
        if (startIndex >= value.Length || length <= 0)
        {
            return string.Empty;
        }

        return value.Substring(startIndex, Math.Min(length, value.Length - startIndex));
    }

    private static byte MapDocumentKind(string code) =>
        code switch
        {
            "FIS" => 1,
            "FAT" => 2,
            "IRS" => 3,
            "GPS" => 4,
            _ => 0
        };

    private static readonly DateTime VatRateChangeDate = new(2023, 7, 10);

    private static int MapTaxRate(int taxCode, DateTime receiptDate = default)
    {
        var isPostReform = receiptDate == default || receiptDate.Date >= VatRateChangeDate;
        return taxCode switch
        {
            1 => isPostReform ? 20 : 18,
            2 => isPostReform ? 10 : 8,
            3 => 1,
            4 => 0,
            _ => 0
        };
    }

    private static DateTime ParseDate(string value)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.GetCultureInfo("tr-TR"),
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return parsed.Date;
        }

        var formats = new[] { "ddMMyy", "ddMMyyyy", "yyyyMMdd", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
        {
            return parsed.Date;
        }

        return DateTime.Today;
    }

    private static TimeSpan ParseTime(string value)
    {
        if (TimeSpan.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), out var parsed))
        {
            return parsed;
        }

        var formats = new[] { "hhmmss", "hhmm", "hh:mm:ss", "hh:mm" };
        if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime))
        {
            return dateTime.TimeOfDay;
        }

        return TimeSpan.Zero;
    }

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var normalized = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        var lastDotIndex = normalized.LastIndexOf('.');
        var lastCommaIndex = normalized.LastIndexOf(',');

        if (lastDotIndex >= 0 || lastCommaIndex >= 0)
        {
            var decimalSeparator = lastDotIndex > lastCommaIndex ? '.' : ',';
            var groupSeparator = decimalSeparator == '.' ? "," : ".";
            normalized = normalized.Replace(groupSeparator, string.Empty, StringComparison.Ordinal);
            normalized = normalized.Replace(decimalSeparator, '.');
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;
    }

    private static decimal ParseMoney(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var normalized = value.Trim();
        var hasExplicitDecimalSeparator = normalized.Contains('.', StringComparison.Ordinal) ||
                                          normalized.Contains(',', StringComparison.Ordinal);
        var parsed = ParseDecimal(normalized);

        return !hasExplicitDecimalSeparator && normalized.All(char.IsDigit)
            ? Round(parsed / 100m)
            : parsed;
    }

    private static int ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    internal static KasaHareketSalesComparisonRowDto CreateSalesComparisonRow(
        DateTime date,
        int branchNo,
        string branchName,
        decimal retailStockAmount,
        decimal invoiceStockAmount,
        decimal expenseSlipStockAmount,
        decimal currentComparableStockAmount,
        decimal currentAccountAmount,
        decimal retailTransferredAmount,
        decimal invoiceTransferredAmount,
        decimal expenseSlipTransferredAmount,
        decimal zReportAmount,
        decimal tolerance)
    {
        retailStockAmount = Round(retailStockAmount);
        invoiceStockAmount = Round(invoiceStockAmount);
        expenseSlipStockAmount = Round(expenseSlipStockAmount);
        currentComparableStockAmount = Round(currentComparableStockAmount);
        currentAccountAmount = Round(currentAccountAmount);
        retailTransferredAmount = Round(retailTransferredAmount);
        invoiceTransferredAmount = Round(invoiceTransferredAmount);
        expenseSlipTransferredAmount = Round(expenseSlipTransferredAmount);
        zReportAmount = Round(zReportAmount);

        var stockMovementAmount = Round(retailStockAmount + invoiceStockAmount);
        var transferredAmount = Round(retailTransferredAmount + invoiceTransferredAmount);
        var retailDifference = Round(retailTransferredAmount - retailStockAmount);
        var invoiceDifference = Round(invoiceTransferredAmount - invoiceStockAmount);
        var expenseSlipDifference = Round(expenseSlipTransferredAmount - expenseSlipStockAmount);
        var netStockTurnover = Round(retailStockAmount + invoiceStockAmount - expenseSlipStockAmount);
        var netTransferredTurnover = Round(
            retailTransferredAmount + invoiceTransferredAmount - expenseSlipTransferredAmount);
        var netDifference = Round(netTransferredTurnover - netStockTurnover);
        // These legacy totals intentionally follow the original Satis Karsilastirma/Form1 formulas.
        var expenseExcludedAmount = Round(transferredAmount - expenseSlipTransferredAmount);
        var zReportTransferDifference = Round(
            stockMovementAmount - (zReportAmount + expenseSlipTransferredAmount));
        var transferredTurnoverDifference = Round(transferredAmount - stockMovementAmount);
        var currentStockDifference = Round(currentComparableStockAmount - currentAccountAmount);
        var hasDifference = Math.Abs(retailDifference) > tolerance
                            || Math.Abs(invoiceDifference) > tolerance
                            || Math.Abs(expenseSlipDifference) > tolerance;

        return new KasaHareketSalesComparisonRowDto(
            date.Date,
            branchNo,
            branchName,
            stockMovementAmount,
            currentAccountAmount,
            transferredAmount,
            expenseExcludedAmount,
            expenseSlipTransferredAmount,
            zReportAmount,
            zReportTransferDifference,
            transferredTurnoverDifference,
            currentStockDifference,
            hasDifference,
            retailStockAmount,
            retailTransferredAmount,
            retailDifference,
            invoiceStockAmount,
            invoiceTransferredAmount,
            invoiceDifference,
            expenseSlipStockAmount,
            expenseSlipTransferredAmount,
            expenseSlipDifference,
            netStockTurnover,
            netTransferredTurnover,
            netDifference);
    }

    private static IReadOnlyCollection<DateTime> EnumerateDates(DateTime startDate, DateTime endDate)
    {
        var dates = new List<DateTime>();
        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }

    private static (DateTime StartDate, DateTime EndDate) NormalizeDateRange(DateTime startDate, DateTime endDate)
    {
        ValidateDate(startDate, nameof(startDate));
        ValidateDate(endDate, nameof(endDate));

        var normalizedStartDate = startDate.Date;
        var normalizedEndDate = endDate.Date;

        if (normalizedEndDate < normalizedStartDate)
        {
            throw new ArgumentException("End date can not be earlier than start date.", nameof(endDate));
        }

        return (normalizedStartDate, normalizedEndDate);
    }

    private static int[] NormalizeCashRegisterNos(
        int? cashRegisterNo,
        IReadOnlyCollection<int>? cashRegisterNos)
    {
        var normalized = new HashSet<int>();

        if (cashRegisterNo.HasValue)
        {
            normalized.Add(cashRegisterNo.Value);
        }

        if (cashRegisterNos is not null)
        {
            foreach (var selectedCashRegisterNo in cashRegisterNos)
            {
                normalized.Add(selectedCashRegisterNo);
            }
        }

        if (normalized.Any(item => item < 0))
        {
            throw new ArgumentException("Kasa numarasi negatif olamaz.", nameof(cashRegisterNos));
        }

        return normalized.OrderBy(item => item).ToArray();
    }

    internal static MikroPosDocumentSnapshot[] FindDocumentDifference(
        IReadOnlyCollection<MikroPosDocumentSnapshot> source,
        IReadOnlyCollection<MikroPosDocumentSnapshot> comparison)
    {
        var comparisonKeys = comparison.Select(item => item.Key).ToHashSet();
        return source.Where(item => !comparisonKeys.Contains(item.Key)).ToArray();
    }

    private static void ValidateDate(DateTime date, string parameterName)
    {
        if (date == default)
        {
            throw new ArgumentException("Date is required.", parameterName);
        }
    }

    private string ResolveFileRootPath(string? requestFileRootPath)
    {
        var configuredPath = configuration["KasaHareketAktarimi:FileRootPath"];
        var fileRootPath = FirstNonEmpty(requestFileRootPath, configuredPath, DefaultFileRootPath);
        return fileRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private int GetConfiguredScheduledAddDay() =>
        int.TryParse(
            configuration["KasaHareketAktarimi:ScheduledAddDay"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var configuredAddDay)
            ? configuredAddDay
            : -1;

    private static string CreateRunId(string importType, DateTime date) =>
        $"{importType}-{date:yyyyMMdd}-{DateTime.Now:HHmmss}";

    private static string ResolveStatus(bool dryRun, int errorCount)
    {
        if (errorCount > 0)
        {
            return dryRun ? "DryRunCompletedWithErrors" : "CompletedWithErrors";
        }

        return dryRun ? "DryRunCompleted" : "Completed";
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string ResolveBranchName(
        int branchNo,
        string? reportBranchName,
        IReadOnlyDictionary<int, string> branchNames)
    {
        if (!string.IsNullOrWhiteSpace(reportBranchName))
        {
            return reportBranchName;
        }

        return branchNames.TryGetValue(branchNo, out var branchName)
            ? branchName
            : string.Empty;
    }

    private static (string Code, string Name) ResolveComparisonStatus(
        KasaHareketReportRowDto? reportRow,
        CashSummarySqlRow? cashSummaryRow,
        decimal differenceAmount,
        decimal tolerance)
    {
        if (reportRow is null)
        {
            return ("missing-movement", "Aktarim Kaydi Yok");
        }

        if (cashSummaryRow is null || cashSummaryRow.DocumentCount == 0)
        {
            return ("missing-cash-summary", "Icmal Kaydi Yok");
        }

        return Math.Abs(differenceAmount) <= tolerance
            ? ("balanced", "Dengeli")
            : ("difference", "Fark Var");
    }

    private async Task<IReadOnlyDictionary<string, string>> GetCashierNamesAsync(
        IEnumerable<string> cashierCodes,
        CancellationToken cancellationToken)
    {
        var codes = cashierCodes
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0)
            .Where(item => item > 0)
            .Distinct()
            .ToArray();

        if (codes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await furpaDbContext.Cashiers
            .AsNoTracking()
            .Where(item => codes.Contains(item.CashierCode))
            .Select(item => new
            {
                Code = item.CashierCode,
                item.CashierName
            })
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(
            item => item.Code.ToString(CultureInfo.InvariantCulture),
            item => item.CashierName,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveCashierName(
        int cashierCode,
        string currentName,
        IReadOnlyDictionary<string, string> cashierNames) =>
        ResolveCashierName(cashierCode.ToString(CultureInfo.InvariantCulture), currentName, cashierNames);

    private static string ResolveCashierName(
        string cashierCode,
        string currentName,
        IReadOnlyDictionary<string, string> cashierNames)
    {
        if (!string.IsNullOrWhiteSpace(currentName))
        {
            return currentName;
        }

        return cashierNames.TryGetValue(cashierCode.Trim(), out var cashierName)
            ? cashierName
            : string.Empty;
    }

    private static KasaHareketCashSummaryComparisonRowDto CreateEmptyComparisonRow(
        DateTime date,
        int branchNo,
        int cashRegisterNo)
    {
        var status = ResolveComparisonStatus(null, null, 0m, 0.01m);
        return new KasaHareketCashSummaryComparisonRowDto(
            date,
            branchNo,
            string.Empty,
            cashRegisterNo,
            0m,
            0m,
            0m,
            0m,
            0m,
            0,
            0m,
            status.Code,
            status.Name);
    }

    private static bool IsCashSummaryPaymentIncluded(int paymentTypeId) =>
        paymentTypeId < 100 || paymentTypeId == 500;

    private static string ResolveMovementPaymentTypeName(int paymentType) =>
        paymentType switch
        {
            1 => "Nakit",
            2 => "Kredi Karti",
            3 => "Yemek Karti",
            4 => "Cek",
            _ => $"Odeme Tipi {paymentType}"
        };

    private static string ResolveDocumentKindName(byte documentKind) =>
        documentKind switch
        {
            1 => "Fis",
            2 => "Fatura",
            4 => "Gider Pusulasi",
            _ => $"Belge Turu {documentKind}"
        };

    private static string ResolveCancelReasonName(byte cancelReason) =>
        cancelReason == 0
            ? string.Empty
            : $"Iptal Nedeni {cancelReason}";

    private static DateTime ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? default
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static DateTime? ReadNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? 0
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static long ReadInt64(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? 0L
            : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? 0m
            : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static string ReadString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static Guid ReadGuid(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return Guid.Empty;
        }

        var value = reader.GetValue(ordinal);
        return value is Guid guid
            ? guid
            : Guid.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)
                ? parsed
                : Guid.Empty;
    }

    private static TimeSpan ReadTimeSpan(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return TimeSpan.Zero;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            TimeSpan timeSpan => timeSpan,
            DateTime dateTime => dateTime.TimeOfDay,
            _ => TimeSpan.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : TimeSpan.Zero
        };
    }

    private static readonly HashSet<string> KnownRecordCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "FIS",
        "FAT",
        "GPS",
        "IRS",
        "TAR",
        "BAS",
        "SAT",
        "IPT",
        "BKD",
        "IND",
        "PRM",
        "NAK",
        "KRD",
        "CEK",
        "SDX",
        "FIP",
        "SON",
        "MLC"
    };

    private sealed class ImportRunState(MovementImportKind kind, bool dryRun, DateTime startDate)
    {
        private readonly List<KasaHareketImportIssueDto> warnings = [];
        private readonly List<KasaHareketImportIssueDto> errors = [];

        public int ProcessedFiles { get; set; }

        public int ProcessedInvoices { get; set; }

        public int SkippedExistingInvoices { get; set; }

        public int InsertedLines { get; set; }

        public int InsertedPayments { get; set; }

        public int InsertedPromotions { get; set; }

        public void AddWarning(int? branchNo, int? cashRegisterNo, string? file, string? receiptNo, int? lineNo, string message) =>
            warnings.Add(new KasaHareketImportIssueDto(
                branchNo,
                cashRegisterNo,
                file,
                receiptNo,
                lineNo,
                SanitizeImportIssueMessage(message)));

        public void AddError(int? branchNo, int? cashRegisterNo, string? file, string? receiptNo, int? lineNo, string message) =>
            errors.Add(new KasaHareketImportIssueDto(
                branchNo,
                cashRegisterNo,
                file,
                receiptNo,
                lineNo,
                SanitizeImportIssueMessage(message)));

        public void AddIssues(IEnumerable<KasaHareketImportIssueDto> issues)
        {
            foreach (var issue in issues)
            {
                errors.Add(issue with { Message = SanitizeImportIssueMessage(issue.Message) });
            }
        }

        public KasaHareketImportResultDto ToDto() =>
            new(
                CreateRunId(kind == MovementImportKind.Normal ? "normal" : "cancel", startDate),
                kind == MovementImportKind.Normal ? "normal" : "cancel",
                ResolveStatus(dryRun, errors.Count),
                ProcessedFiles,
                ProcessedInvoices,
                SkippedExistingInvoices,
                InsertedLines,
                InsertedPayments,
                InsertedPromotions,
                warnings.ToArray(),
                errors.ToArray());
    }

    private sealed class ParserState(int branchNo, MovementImportKind kind, string fileName)
    {
        public int BranchNo { get; } = branchNo;

        public MovementImportKind Kind { get; } = kind;

        public ParsedReceipt? Current { get; private set; }

        public ParsedLine? PendingLine { get; set; }

        public ParsedLine? LastLine { get; set; }

        public bool IsCurrentValid { get; private set; } = true;

        public void Begin(ParsedReceipt receipt)
        {
            Current = receipt;
            PendingLine = null;
            LastLine = null;
            IsCurrentValid = true;
        }

        public void Clear()
        {
            Current = null;
            PendingLine = null;
            LastLine = null;
            IsCurrentValid = true;
        }

        public void MarkInvalid() => IsCurrentValid = false;

        public ParsedReceipt RequireCurrent() =>
            Current ?? throw new InvalidOperationException("Fis basligi bulunmadan detay satiri geldi.");

        public KasaHareketImportIssueDto CreateIssue(int? lineNo, string message) =>
            new(
                BranchNo,
                Current?.CashRegisterNo,
                fileName,
                Current?.ReceiptNo.ToString(CultureInfo.InvariantCulture),
                lineNo,
                SanitizeImportIssueMessage(message));
    }

    private sealed class ParsedReceipt
    {
        public Guid InvoiceGuid { get; } = Guid.NewGuid();

        public MovementImportKind Kind { get; init; }

        public int BranchNo { get; init; }

        public DateTime Date { get; set; } = DateTime.Today;

        public TimeSpan Time { get; set; }

        public byte DocumentKind { get; init; }

        public string ZNo { get; init; } = string.Empty;

        public int ReceiptNo { get; init; }

        public int CashRegisterNo { get; init; }

        public string UserCode { get; init; } = string.Empty;

        public string CardNumber { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public decimal TotalTax { get; set; }

        public decimal InvoiceDiscount { get; set; }

        public decimal TaxBase18 { get; set; }

        public decimal TaxBase08 { get; set; }

        public decimal TaxBase01 { get; set; }

        public decimal TaxBase00 { get; set; }

        public decimal TaxTotal18 { get; set; }

        public decimal TaxTotal08 { get; set; }

        public decimal TaxTotal01 { get; set; }

        public decimal TaxTotal00 { get; set; }

        public DateTime CreateDate { get; init; } = DateTime.Now;

        public string FiscalMemoryCode { get; set; } = string.Empty;

        public string CustomerCurrentCode { get; set; } = string.Empty;

        public string ProcessResult { get; set; } = string.Empty;

        public byte CancelReason { get; set; }

        public List<ParsedLine> Lines { get; } = [];

        public List<ParsedPayment> Payments { get; } = [];

        public List<ParsedPromotion> Promotions { get; } = [];
    }

    private sealed record ReceiptKey(
        DateTime Date,
        int BranchNo,
        int CashRegisterNo,
        int ReceiptNo,
        byte DocumentKind)
    {
        public static ReceiptKey From(ParsedReceipt receipt) =>
            new(
                receipt.Date.Date,
                receipt.BranchNo,
                receipt.CashRegisterNo,
                receipt.ReceiptNo,
                receipt.DocumentKind);
    }

    private sealed class ParsedLine
    {
        public Guid LineGuid { get; } = Guid.NewGuid();

        public Guid InvoiceGuid { get; init; }

        public string ProductCode { get; set; } = string.Empty;

        public decimal Quantity { get; init; }

        public decimal UnitPrice { get; set; }

        public decimal GrossAmount { get; set; }

        public decimal NetAmount { get; set; }

        public string Barcode { get; set; } = string.Empty;

        public DateTime Date { get; init; }

        public TimeSpan Time { get; init; }

        public int TaxRate { get; init; }

        public decimal TaxAmount { get; set; }

        public byte Status { get; init; }

        public int CashRegisterNo { get; init; }

        public int BranchNo { get; init; }

        public decimal Discount { get; set; }

        public string CashierCode { get; init; } = string.Empty;

        public DateTime CreateDate { get; init; }

        public byte CancelReason { get; set; }
    }

    private sealed class ParsedPayment
    {
        public Guid PaymentGuid { get; } = Guid.NewGuid();

        public Guid InvoiceGuid { get; init; }

        public byte PaymentType { get; init; }

        public decimal Amount { get; init; }

        public string BankCode { get; init; } = string.Empty;

        public int CashRegisterNo { get; init; }

        public int BranchNo { get; init; }

        public DateTime Date { get; init; }

        public TimeSpan Time { get; init; }

        public string SdxTypeCode { get; init; } = string.Empty;

        public string CheckNumber { get; init; } = string.Empty;

        public DateTime CreateDate { get; init; }
    }

    private sealed class ParsedPromotion
    {
        public Guid InvoiceGuid { get; init; }

        public Guid LineGuid { get; init; }

        public string Barcode { get; init; } = string.Empty;

        public string ProductCode { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        public string DiscountType { get; init; } = string.Empty;

        public string PromotionCode { get; init; } = string.Empty;

        public int BranchNo { get; init; }

        public int CashRegisterNo { get; init; }

        public DateTime Date { get; init; }

        public TimeSpan Time { get; init; }

        public int ReceiptNo { get; init; }

        public DateTime CreateDate { get; init; }
    }

    private sealed record ParsedFile(
        IReadOnlyCollection<ParsedReceipt> Receipts,
        IReadOnlyCollection<KasaHareketImportIssueDto> Issues);

    private sealed record ReportSqlRow(
        DateTime Date,
        int BranchNo,
        int CashRegisterNo,
        decimal NetAmount,
        decimal Expense,
        decimal CheckAmount,
        decimal Difference);

    private sealed record SalesComparisonMikroSqlRow(
        int BranchNo,
        decimal RetailStockAmount,
        decimal InvoiceStockAmount,
        decimal ExpenseSlipStockAmount,
        decimal CurrentComparableStockAmount,
        decimal CurrentAccountAmount,
        decimal ZReportAmount);

    private sealed record SalesComparisonFurpaSqlRow(
        int BranchNo,
        decimal RetailTransferredAmount,
        decimal InvoiceTransferredAmount,
        decimal ExpenseSlipTransferredAmount);

    private sealed record SatisMutabakatMikroSqlRow(
        int BranchNo,
        decimal AktarilanTutar,
        decimal ZRaporuTutari,
        decimal BaskaCariTutar,
        string BaskaCariDetay);

    private sealed record SatisMutabakatFurpaSqlRow(
        int BranchNo,
        decimal AraTabloTutar,
        decimal GiderPusulasiTutar,
        decimal GPSHaricTutar,
        decimal Nakit,
        decimal KrediKarti,
        decimal YemekKarti,
        decimal HediyeKarti);

    internal sealed record MikroPosDocumentKey(
        DocumentFlowType DocumentType,
        DateTime Date,
        int BranchNo,
        string DocumentSerie,
        int DocumentOrderNo);

    internal sealed record MikroPosDocumentSnapshot(
        MikroPosDocumentKey Key,
        int LineCount);

    internal sealed record MikroPosDocumentSequence(
        int BranchNo,
        int LastStockDocumentNo,
        int LastCustomerDocumentNo,
        DateTime? LastStockDocumentDate = null,
        DateTime? LastCustomerDocumentDate = null);

    private sealed record CashSummarySqlRow(
        DateTime Date,
        int BranchNo,
        int CashRegisterNo,
        decimal CashSummaryAmount,
        int DocumentCount);

    private sealed record CashSummaryPaymentSource(
        int PaymentTypeId,
        int SlipCount,
        decimal Amount);

    private enum MovementImportKind
    {
        Normal = 0,
        Cancel = 1
    }
}
