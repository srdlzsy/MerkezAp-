using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaHareketAktarimi;

public sealed class KasaHareketAktarimiService(
    FurpaDbContext furpaDbContext,
    MikroDbContext mikroDbContext,
    IConfiguration configuration)
    : IKasaHareketAktarimiService
{
    private const string DefaultFileRootPath = @"\\10.0.0.55\kasa\";
    private const string NormalPrefix = "HR";
    private const string CancelPrefix = "IP";

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

        return await furpaDbContext.CashRegistryDetails
            .AsNoTracking()
            .Where(item => item.BranchNo == branchNo)
            .OrderBy(item => item.CashRegisterNo)
            .Select(item => new KasaHareketCashRegisterDto(
                item.BranchNo,
                item.CashRegisterNo,
                item.CashRegisterType))
            .ToArrayAsync(cancellationToken);
    }

    public Task<KasaHareketImportResultDto> ImportMovementsAsync(
        KasaHareketImportRequest request,
        CancellationToken cancellationToken) =>
        ExecuteImportAsync(request, MovementImportKind.Normal, cancellationToken);

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

        var normalResult = await ImportMovementsAsync(importRequest, cancellationToken);
        var cancelResult = await ImportCancelMovementsAsync(importRequest, cancellationToken);

        return new KasaHareketImportResultDto(
            CreateRunId("scheduled", importDate),
            "scheduled",
            ResolveStatus(request.DryRun, normalResult.Errors.Count + cancelResult.Errors.Count),
            normalResult.ProcessedFiles + cancelResult.ProcessedFiles,
            normalResult.ProcessedInvoices + cancelResult.ProcessedInvoices,
            normalResult.SkippedExistingInvoices + cancelResult.SkippedExistingInvoices,
            normalResult.InsertedLines + cancelResult.InsertedLines,
            normalResult.InsertedPayments + cancelResult.InsertedPayments,
            normalResult.InsertedPromotions + cancelResult.InsertedPromotions,
            normalResult.Warnings.Concat(cancelResult.Warnings).ToArray(),
            normalResult.Errors.Concat(cancelResult.Errors).ToArray());
    }

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

        var message = await ExecuteProcedureAsync(
            "StokHareketYaz",
            cancellationToken,
            ("@Tarih", request.Date.Date),
            ("@Sube", request.BranchNo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));

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

        var message = await ExecuteProcedureAsync(
            "StokHareketSil",
            cancellationToken,
            ("@Tarih", request.Date.Date),
            ("@Sube", request.BranchNo?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));

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

        var message = await ExecuteProcedureAsync(
            "StokHareketYaz2",
            cancellationToken,
            ("@StartDate", startDate),
            ("@EndDate", endDate));

        return new KasaHareketProcedureResultDto(
            "StokHareketYaz2",
            message,
            startDate,
            null,
            null);
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
                        ELSE ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0) - ISNULL(invoice.FaturaIndirimi, 0)
                    END) AS NetAmount,
                    SUM(CASE
                        WHEN ISNULL(invoice.BelgeTuru, 0) = 4 THEN ISNULL(invoice.Toplam, 0) + ISNULL(invoice.ToplamKdv, 0) - ISNULL(invoice.FaturaIndirimi, 0)
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

        var branchNames = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(item => item.dep_no.HasValue)
            .Select(item => new
            {
                BranchNo = item.dep_no ?? 0,
                BranchName = item.dep_adi ?? string.Empty
            })
            .ToDictionaryAsync(item => item.BranchNo, item => item.BranchName, cancellationToken);

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

                    foreach (var receipt in parsedFile.Receipts)
                    {
                        if (request.SkipExisting &&
                            await ReceiptExistsAsync(receipt, cancellationToken))
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

        if (state.Kind == MovementImportKind.Normal && code == "FAT")
        {
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
        var grossAmount = ParseDecimal(SafeSubstring(amountToken, 2, Math.Max(0, Math.Min(10, amountToken.Length - 2))));

        state.PendingLine = new ParsedLine
        {
            InvoiceGuid = receipt.InvoiceGuid,
            Quantity = quantity,
            GrossAmount = grossAmount,
            UnitPrice = quantity == 0m ? 0m : Round(grossAmount / quantity),
            TaxRate = MapTaxRate(ParseInt(SafeSubstring(amountToken, 0, Math.Min(2, amountToken.Length)))),
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
        var barcodeLeft = Token(tokens, 4);
        var barcodeRight = Token(tokens, 5);
        var barcode = barcodeLeft.StartsWith("27", StringComparison.Ordinal) ||
                      barcodeLeft.StartsWith("29", StringComparison.Ordinal)
            ? SafeSubstring(barcodeLeft, 0, Math.Min(7, barcodeLeft.Length)).Trim()
            : (barcodeLeft + barcodeRight).Trim();

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
        var discount = ParseDecimal(Token(tokens, 5));

        if (discount == 0m)
        {
            return;
        }

        var targetLine = state.LastLine ?? receipt.Lines.LastOrDefault();

        if (targetLine is not null)
        {
            ApplyLineDiscount(targetLine, discount);
            receipt.InvoiceDiscount += discount;
            return;
        }

        var total = receipt.Lines.Sum(item => item.GrossAmount);
        if (total == 0m)
        {
            return;
        }

        foreach (var line in receipt.Lines)
        {
            var lineDiscount = Round(discount * line.GrossAmount / total);
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
            Amount = ParseDecimal(Token(tokens, 5)),
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
                ? ParseDecimal(SafeSubstring(Token(tokens, 5), 4))
                : ParseDecimal(Token(tokens, 5)),
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

    private async Task<bool> ReceiptExistsAsync(
        ParsedReceipt receipt,
        CancellationToken cancellationToken)
    {
        var tableName = receipt.Kind == MovementImportKind.Normal
            ? "dbo.PosFaturas"
            : "dbo.PosFaturaIptals";

        var sql = $"""
            SELECT COUNT(1)
            FROM {tableName} WITH (NOLOCK)
            WHERE TRY_CONVERT(int, Sube) = @branchNo
              AND TRY_CONVERT(int, KasaNo) = @cashRegisterNo
              AND TRY_CONVERT(int, FisNo) = @receiptNo
              AND TRY_CONVERT(int, BelgeTuru) = @documentKind
              AND CAST(Tarih AS date) = @date;
            """;

        var count = await ExecuteScalarAsync<int>(
            furpaDbContext.Database.GetDbConnection(),
            sql,
            command =>
            {
                AddParameter(command, "@branchNo", receipt.BranchNo);
                AddParameter(command, "@cashRegisterNo", receipt.CashRegisterNo);
                AddParameter(command, "@receiptNo", receipt.ReceiptNo);
                AddParameter(command, "@documentKind", receipt.DocumentKind);
                AddParameter(command, "@date", receipt.Date.Date);
            },
            cancellationToken);

        return count > 0;
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
                createDate, KasaMaliBellekKodu, MusteriCariKodu, IslemSonuc)
            VALUES (
                @FaturaGuid, @Sube, @Tarih, @Saat, @BelgeTuru, @ZNo, @FisNo, @KasaNo,
                @KullaniciKodu, @KartNumarasi, @Toplam, @ToplamKdv, @FaturaIndirimi,
                @VergiMatrahi18, @VergiMatrahi08, @VergiMatrahi01, @VergiMatrahi00,
                @VergiToplami18, @VergiToplami08, @VergiToplami01, @VergiToplami00,
                @createDate, @KasaMaliBellekKodu, @MusteriCariKodu, @IslemSonuc);
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
                createDate, KasaMaliBellekKodu, IptalNedeni, MusteriCariKodu, IslemSonuc)
            VALUES (
                @FaturaGuid, @Sube, @Tarih, @Saat, @BelgeTuru, @ZNo, @FisNo, @KasaNo,
                @KullaniciKodu, @KartNumarasi, @Toplam, @ToplamKdv, @FaturaIndirimi,
                @VergiMatrahi18, @VergiMatrahi08, @VergiMatrahi01, @VergiMatrahi00,
                @VergiToplami18, @VergiToplami08, @VergiToplami01, @VergiToplami00,
                @createDate, @KasaMaliBellekKodu, @IptalNedeni, @MusteriCariKodu, @IslemSonuc);
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

    private async Task<string> ExecuteProcedureAsync(
        string procedureName,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = furpaDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = procedureName;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 300;

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
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

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
            command.CommandTimeout = 180;
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
                case 18:
                    receipt.TaxBase18 += netAmount * factor;
                    receipt.TaxTotal18 += taxAmount * factor;
                    break;
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

    private static int MapTaxRate(int taxCode) =>
        taxCode switch
        {
            1 => 18,
            2 => 8,
            3 => 1,
            4 => 0,
            _ => 0
        };

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

        var normalized = value.Trim();
        if (decimal.TryParse(
                normalized,
                NumberStyles.Any,
                CultureInfo.GetCultureInfo("tr-TR"),
                out var parsed))
        {
            return parsed;
        }

        normalized = normalized.Replace(',', '.');
        return decimal.TryParse(
            normalized,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out parsed)
            ? parsed
            : 0m;
    }

    private static int ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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

    private static DateTime ReadDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? default
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int ReadInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? 0
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static decimal ReadDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? 0m
            : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
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
            warnings.Add(new KasaHareketImportIssueDto(branchNo, cashRegisterNo, file, receiptNo, lineNo, message));

        public void AddError(int? branchNo, int? cashRegisterNo, string? file, string? receiptNo, int? lineNo, string message) =>
            errors.Add(new KasaHareketImportIssueDto(branchNo, cashRegisterNo, file, receiptNo, lineNo, message));

        public void AddIssues(IEnumerable<KasaHareketImportIssueDto> issues)
        {
            foreach (var issue in issues)
            {
                errors.Add(issue);
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
                message);
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

    private enum MovementImportKind
    {
        Normal = 0,
        Cancel = 1
    }
}
