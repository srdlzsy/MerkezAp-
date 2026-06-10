using System.Data;
using System.Data.Common;
using System.Globalization;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCiroAktarimi;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCiroAktarimi;

public sealed class KasaCiroAktarimiService(
    MikroDbContext mikroDbContext,
    MikroWriteDbContext mikroWriteDbContext,
    IConfiguration configuration)
    : IKasaCiroAktarimiService
{
    private const string DefaultMovementRootPath = @"\\10.0.0.55\kasa\";
    private const string FilePrefix = "HR";
    private const int DefaultFirstBranchNo = 101;
    private const int DefaultLastBranchNo = 300;

    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly HashSet<string> GiftCardCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "10",
        "11",
        "12",
        "13",
        "14",
        "15",
        "16"
    };

    public async Task<IReadOnlyCollection<KasaCiroBranchDto>> ListBranchesAsync(
        CancellationToken cancellationToken)
    {
        var rows = await mikroDbContext.DEPOLARs
            .AsNoTracking()
            .Where(item =>
                item.dep_no.HasValue &&
                item.dep_no.Value >= DefaultFirstBranchNo &&
                item.dep_no.Value <= DefaultLastBranchNo)
            .OrderBy(item => item.dep_no)
            .Select(item => new
            {
                BranchNo = item.dep_no ?? 0,
                BranchName = item.dep_adi ?? string.Empty,
                Region = item.dep_bolge_kodu ?? string.Empty
            })
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(item => new KasaCiroBranchDto(item.BranchNo, item.BranchName, item.Region))
            .ToArray();
    }

    public async Task<KasaCiroImportResultDto> ImportTextMovementsAsync(
        KasaCiroImportRequest request,
        CancellationToken cancellationToken)
    {
        var (startDate, endDate) = NormalizeDateRange(request.StartDate, request.EndDate);
        var state = new ImportState(startDate, endDate, request.DryRun);
        var movementRootPath = ResolveMovementRootPath(request.MovementRootPath);
        var branchNumbers = NormalizeBranchNumbers(request.Branches);
        var requestedSpecificBranches = request.Branches is { Count: > 0 };

        foreach (var date in EnumerateDates(startDate, endDate))
        {
            state.ProcessedDays++;

            foreach (var branchNo in branchNumbers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var files = FindFiles(
                    movementRootPath,
                    date,
                    branchNo,
                    requestedSpecificBranches,
                    state);

                if (files.Count == 0)
                {
                    continue;
                }

                var branchTurnover = new BranchTurnover(date, branchNo);
                state.ProcessedBranches++;

                foreach (var filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!TryParseCashRegisterNo(filePath, out var cashRegisterNo))
                    {
                        state.AddError(
                            date,
                            branchNo,
                            null,
                            filePath,
                            null,
                            "Kasa no dosya uzantisindan okunamadi.");
                        continue;
                    }

                    var detail = ParseFile(
                        filePath,
                        date,
                        branchNo,
                        cashRegisterNo,
                        branchTurnover,
                        state,
                        cancellationToken);

                    if (detail is null)
                    {
                        continue;
                    }

                    branchTurnover.Details.Add(detail);
                    state.ProcessedFiles++;
                }

                if (branchTurnover.OverallTotal < 0.001m)
                {
                    state.SkippedEmptyBranches++;
                    continue;
                }

                try
                {
                    var writeResult = await UpsertBranchTurnoverAsync(
                        branchTurnover,
                        request.DryRun,
                        cancellationToken);

                    state.InsertedTotals += writeResult.InsertedTotals;
                    state.UpdatedTotals += writeResult.UpdatedTotals;
                    state.InsertedDetails += writeResult.InsertedDetails;
                    state.UpdatedDetails += writeResult.UpdatedDetails;
                    state.InsertedDiscountCards += writeResult.InsertedDiscountCards;
                    state.UpdatedDiscountCards += writeResult.UpdatedDiscountCards;
                }
                catch (Exception exception) when (exception is DbException or InvalidOperationException)
                {
                    state.AddError(
                        date,
                        branchNo,
                        null,
                        null,
                        null,
                        $"Ciro kaydi yazilamadi: {exception.Message}");
                }
            }
        }

        return state.ToDto();
    }

    private TurnoverDetail? ParseFile(
        string filePath,
        DateTime date,
        int branchNo,
        int cashRegisterNo,
        BranchTurnover branchTurnover,
        ImportState importState,
        CancellationToken cancellationToken)
    {
        var fileState = new FileTurnoverState();
        var lineNo = 0;

        try
        {
            foreach (var line in File.ReadLines(filePath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNo++;

                try
                {
                    ApplyLine(
                        line,
                        date,
                        branchNo,
                        cashRegisterNo,
                        filePath,
                        lineNo,
                        fileState,
                        branchTurnover,
                        importState);
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
                {
                    importState.AddError(
                        date,
                        branchNo,
                        cashRegisterNo,
                        filePath,
                        lineNo,
                        exception.Message);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            importState.AddError(date, branchNo, cashRegisterNo, filePath, null, exception.Message);
            return null;
        }

        return new TurnoverDetail(
            cashRegisterNo,
            fileState.LastBillTime,
            fileState.CreditAmount,
            fileState.CashAmount,
            fileState.GiftCardAmount,
            fileState.ExpenseNoteAmount,
            fileState.FuturesSalesAmount);
    }

    private static void ApplyLine(
        string line,
        DateTime date,
        int branchNo,
        int cashRegisterNo,
        string filePath,
        int lineNo,
        FileTurnoverState fileState,
        BranchTurnover branchTurnover,
        ImportState importState)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (ContainsCode(line, "FIS") || ContainsCode(line, "FAT") || ContainsCode(line, "IRS"))
        {
            fileState.LastDocumentType = "FIS";
            branchTurnover.CustomerCount++;
        }

        if (ContainsCode(line, "GPS"))
        {
            fileState.LastDocumentType = "GPS";
            branchTurnover.ExpenseNoteCount++;
        }

        if (ContainsCode(line, "BAS"))
        {
            var columns = SplitComma(line);
            fileState.CustomerCode = ReadColumn(columns, 4) + ReadColumn(columns, 5);

            if (IsFutureSaleCustomerCode(fileState.CustomerCode))
            {
                branchTurnover.FuturesSalesCount++;
            }
        }

        if (!ContainsCode(line, "IND") && ContainsCode(line, "TOP") && IsFutureSaleCustomerCode(fileState.CustomerCode))
        {
            fileState.FuturesSalesAmount += ParseLastAmount(line);
        }

        if (ContainsCode(line, "TAR"))
        {
            fileState.LastBillTime = ReadAfterLastComma(line);
        }

        if (ContainsCode(line, "SON"))
        {
            ApplyCardUsage(
                line,
                date,
                branchNo,
                cashRegisterNo,
                filePath,
                lineNo,
                branchTurnover,
                importState);
        }

        if (ContainsCode(line, "KRD"))
        {
            ApplyCreditPayment(line, fileState);
        }

        if (ContainsCode(line, "SDX"))
        {
            fileState.GiftCardAmount += ParseLastAmount(line);
        }

        if (ContainsCode(line, "NAK"))
        {
            ApplyCashPayment(line, fileState);
        }
    }

    private static void ApplyCardUsage(
        string line,
        DateTime date,
        int branchNo,
        int cashRegisterNo,
        string filePath,
        int lineNo,
        BranchTurnover branchTurnover,
        ImportState importState)
    {
        var columns = SplitComma(line);
        var customerCode = ReadColumn(columns, 4) + ReadColumn(columns, 5);

        if (customerCode.Length < 16)
        {
            return;
        }

        var sonIndex = line.IndexOf("SON,", StringComparison.OrdinalIgnoreCase);
        if (sonIndex < 0)
        {
            importState.AddError(date, branchNo, cashRegisterNo, filePath, lineNo, "SON kart alani okunamadi.");
            return;
        }

        var cardCode = SafeSubstring(line, sonIndex + 4, 4);
        var cardNumber = SafeSubstring(line, sonIndex + 4, 17)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (string.IsNullOrWhiteSpace(cardNumber))
        {
            importState.AddError(date, branchNo, cashRegisterNo, filePath, lineNo, "Kart numarasi bos geldi.");
            return;
        }

        if (string.Equals(cardCode, "2012", StringComparison.Ordinal))
        {
            branchTurnover.FurparaCardCustomerCount++;
        }
        else
        {
            branchTurnover.DiscountCardCustomerCount++;
        }

        if (branchTurnover.DiscountCards.TryGetValue(cardNumber, out var detail))
        {
            detail.UsageCount++;
            return;
        }

        branchTurnover.DiscountCards[cardNumber] = new TurnoverDiscountCardDetail(cardNumber, date, 1);
    }

    private static void ApplyCreditPayment(string line, FileTurnoverState fileState)
    {
        if (string.IsNullOrWhiteSpace(fileState.LastDocumentType))
        {
            throw new InvalidOperationException("KRD satiri belge tipi gelmeden okundu.");
        }

        var amount = ParseLastAmount(line);
        var creditType = SplitComma(line, 4).LastOrDefault() ?? string.Empty;
        var cardTypeStart = creditType.IndexOf("KRD", StringComparison.OrdinalIgnoreCase) + 14;
        var cardType = cardTypeStart >= 14 ? SafeSubstring(creditType, cardTypeStart, 2) : string.Empty;

        if (string.Equals(fileState.LastDocumentType, "FIS", StringComparison.Ordinal))
        {
            if (GiftCardCodes.Contains(cardType) && !string.Equals(cardType, "17", StringComparison.Ordinal))
            {
                fileState.GiftCardAmount += amount;
                return;
            }

            fileState.CreditAmount += amount;
            return;
        }

        fileState.CreditAmount -= amount;
        fileState.ExpenseNoteAmount += amount;
    }

    private static void ApplyCashPayment(string line, FileTurnoverState fileState)
    {
        if (string.IsNullOrWhiteSpace(fileState.LastDocumentType))
        {
            throw new InvalidOperationException("NAK satiri belge tipi gelmeden okundu.");
        }

        var amount = ParseLastAmount(line);

        if (string.Equals(fileState.LastDocumentType, "FIS", StringComparison.Ordinal))
        {
            fileState.CashAmount += amount;
            return;
        }

        fileState.CashAmount -= amount;
        fileState.ExpenseNoteAmount += amount;
    }

    private async Task<WriteResult> UpsertBranchTurnoverAsync(
        BranchTurnover branchTurnover,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var connection = mikroWriteDbContext.Database.GetDbConnection();
        var closeConnection = connection.State == ConnectionState.Closed;
        DbTransaction? transaction = null;
        var result = new WriteResult();

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!dryRun)
            {
                transaction = await connection.BeginTransactionAsync(cancellationToken);
            }

            var turnoverId = await FindTurnoverIdAsync(
                connection,
                transaction,
                branchTurnover.BranchNo,
                branchTurnover.Date,
                !dryRun,
                cancellationToken);

            if (turnoverId.HasValue)
            {
                result.UpdatedTotals++;

                if (!dryRun)
                {
                    await UpdateTurnoverTotalAsync(
                        connection,
                        transaction,
                        turnoverId.Value,
                        branchTurnover,
                        cancellationToken);
                }
            }
            else
            {
                result.InsertedTotals++;

                turnoverId = dryRun
                    ? 0
                    : await InsertTurnoverTotalAsync(connection, transaction, branchTurnover, cancellationToken);
            }

            foreach (var detail in branchTurnover.Details)
            {
                var detailId = turnoverId > 0
                    ? await FindDetailIdAsync(
                        connection,
                        transaction,
                        turnoverId.Value,
                        detail.CashRegisterNo,
                        !dryRun,
                        cancellationToken)
                    : null;

                if (detailId.HasValue)
                {
                    result.UpdatedDetails++;

                    if (!dryRun)
                    {
                        await UpdateTurnoverDetailAsync(
                            connection,
                            transaction,
                            detailId.Value,
                            detail,
                            cancellationToken);
                    }
                }
                else
                {
                    result.InsertedDetails++;

                    if (!dryRun)
                    {
                        await InsertTurnoverDetailAsync(
                            connection,
                            transaction,
                            turnoverId!.Value,
                            detail,
                            cancellationToken);
                    }
                }
            }

            foreach (var cardDetail in branchTurnover.DiscountCards.Values)
            {
                var cardDetailId = turnoverId > 0
                    ? await FindDiscountCardDetailIdAsync(
                        connection,
                        transaction,
                        turnoverId.Value,
                        cardDetail.CardNumber,
                        !dryRun,
                        cancellationToken)
                    : null;

                if (cardDetailId.HasValue)
                {
                    result.UpdatedDiscountCards++;

                    if (!dryRun)
                    {
                        await UpdateDiscountCardDetailAsync(
                            connection,
                            transaction,
                            cardDetailId.Value,
                            cardDetail,
                            cancellationToken);
                    }
                }
                else
                {
                    result.InsertedDiscountCards++;

                    if (!dryRun)
                    {
                        await InsertDiscountCardDetailAsync(
                            connection,
                            transaction,
                            turnoverId!.Value,
                            cardDetail,
                            cancellationToken);
                    }
                }
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }

            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static Task<int?> FindTurnoverIdAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int branchNo,
        DateTime date,
        bool withLock,
        CancellationToken cancellationToken)
    {
        var lockHint = withLock ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty;
        var sql = $"""
            SELECT TOP (1) [TurnoverId]
            FROM [dbo].[TurnoverTotals]{lockHint}
            WHERE [TurnoverDate] = @turnoverDate
              AND [BranchNo] = @branchNo;
            """;

        return ExecuteInt32OrNullAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddParameter(command, "@turnoverDate", date.Date);
                AddParameter(command, "@branchNo", branchNo);
            },
            cancellationToken);
    }

    private static async Task<int> InsertTurnoverTotalAsync(
        DbConnection connection,
        DbTransaction? transaction,
        BranchTurnover branchTurnover,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO [dbo].[TurnoverTotals]
                ([BranchNo],
                 [CustomerCount],
                 [FurparaCardCustomerCount],
                 [DiscountCardCustomerCount],
                 [ExpenseNoteCount],
                 [FuturesSalesCount],
                 [TurnoverDate],
                 [TurnoverLastUpdate],
                 [TurnoverOverallTotal])
            OUTPUT INSERTED.[TurnoverId]
            VALUES
                (@branchNo,
                 @customerCount,
                 @furparaCardCustomerCount,
                 @discountCardCustomerCount,
                 @expenseNoteCount,
                 @futuresSalesCount,
                 @turnoverDate,
                 @turnoverLastUpdate,
                 @turnoverOverallTotal);
            """;

        var id = await ExecuteInt32OrNullAsync(
            connection,
            transaction,
            sql,
            command => AddTurnoverTotalParameters(command, branchTurnover),
            cancellationToken);

        return id ?? throw new InvalidOperationException("TurnoverTotals insert kimligi okunamadi.");
    }

    private static Task UpdateTurnoverTotalAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int turnoverId,
        BranchTurnover branchTurnover,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [dbo].[TurnoverTotals]
            SET [BranchNo] = @branchNo,
                [CustomerCount] = @customerCount,
                [FurparaCardCustomerCount] = @furparaCardCustomerCount,
                [DiscountCardCustomerCount] = @discountCardCustomerCount,
                [ExpenseNoteCount] = @expenseNoteCount,
                [FuturesSalesCount] = @futuresSalesCount,
                [TurnoverDate] = @turnoverDate,
                [TurnoverLastUpdate] = @turnoverLastUpdate,
                [TurnoverOverallTotal] = @turnoverOverallTotal
            WHERE [TurnoverId] = @turnoverId;
            """;

        return ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddTurnoverTotalParameters(command, branchTurnover);
                AddParameter(command, "@turnoverId", turnoverId);
            },
            cancellationToken);
    }

    private static Task<int?> FindDetailIdAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int turnoverId,
        int cashRegisterNo,
        bool withLock,
        CancellationToken cancellationToken)
    {
        var lockHint = withLock ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty;
        var sql = $"""
            SELECT TOP (1) [DetailId]
            FROM [dbo].[TurnoverDetails]{lockHint}
            WHERE [TurnoverId] = @turnoverId
              AND [CashRegisterNo] = @cashRegisterNo;
            """;

        return ExecuteInt32OrNullAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddParameter(command, "@turnoverId", turnoverId);
                AddParameter(command, "@cashRegisterNo", cashRegisterNo);
            },
            cancellationToken);
    }

    private static Task InsertTurnoverDetailAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int turnoverId,
        TurnoverDetail detail,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO [dbo].[TurnoverDetails]
                ([TurnoverId],
                 [CashRegisterNo],
                 [LastBillTime],
                 [CreditTotal],
                 [CashTotal],
                 [GiftCardTotal],
                 [ExpenseNoteTotal],
                 [FuturesSalesTotal])
            VALUES
                (@turnoverId,
                 @cashRegisterNo,
                 @lastBillTime,
                 @creditTotal,
                 @cashTotal,
                 @giftCardTotal,
                 @expenseNoteTotal,
                 @futuresSalesTotal);
            """;

        return ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddParameter(command, "@turnoverId", turnoverId);
                AddTurnoverDetailParameters(command, detail);
            },
            cancellationToken);
    }

    private static Task UpdateTurnoverDetailAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int detailId,
        TurnoverDetail detail,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [dbo].[TurnoverDetails]
            SET [CashRegisterNo] = @cashRegisterNo,
                [LastBillTime] = @lastBillTime,
                [CreditTotal] = @creditTotal,
                [CashTotal] = @cashTotal,
                [GiftCardTotal] = @giftCardTotal,
                [ExpenseNoteTotal] = @expenseNoteTotal,
                [FuturesSalesTotal] = @futuresSalesTotal
            WHERE [DetailId] = @detailId;
            """;

        return ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddTurnoverDetailParameters(command, detail);
                AddParameter(command, "@detailId", detailId);
            },
            cancellationToken);
    }

    private static Task<int?> FindDiscountCardDetailIdAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int turnoverId,
        string cardNumber,
        bool withLock,
        CancellationToken cancellationToken)
    {
        var lockHint = withLock ? " WITH (UPDLOCK, HOLDLOCK)" : string.Empty;
        var sql = $"""
            SELECT TOP (1) [CardDetailId]
            FROM [dbo].[TurnoverDiscountCardDetails]{lockHint}
            WHERE [TurnoverId] = @turnoverId
              AND [CardNumber] = @cardNumber;
            """;

        return ExecuteInt32OrNullAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddParameter(command, "@turnoverId", turnoverId);
                AddParameter(command, "@cardNumber", cardNumber);
            },
            cancellationToken);
    }

    private static Task InsertDiscountCardDetailAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int turnoverId,
        TurnoverDiscountCardDetail detail,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO [dbo].[TurnoverDiscountCardDetails]
                ([TurnoverId],
                 [CardNumber],
                 [Date],
                 [UsageCount])
            VALUES
                (@turnoverId,
                 @cardNumber,
                 @date,
                 @usageCount);
            """;

        return ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddParameter(command, "@turnoverId", turnoverId);
                AddDiscountCardDetailParameters(command, detail);
            },
            cancellationToken);
    }

    private static Task UpdateDiscountCardDetailAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int cardDetailId,
        TurnoverDiscountCardDetail detail,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [dbo].[TurnoverDiscountCardDetails]
            SET [CardNumber] = @cardNumber,
                [Date] = @date,
                [UsageCount] = @usageCount
            WHERE [CardDetailId] = @cardDetailId;
            """;

        return ExecuteNonQueryAsync(
            connection,
            transaction,
            sql,
            command =>
            {
                AddDiscountCardDetailParameters(command, detail);
                AddParameter(command, "@cardDetailId", cardDetailId);
            },
            cancellationToken);
    }

    private static async Task<int?> ExecuteInt32OrNullAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 180;

        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        configureCommand(command);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull
            ? null
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        Action<DbCommand> configureCommand,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = 180;

        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        configureCommand(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddTurnoverTotalParameters(DbCommand command, BranchTurnover branchTurnover)
    {
        AddParameter(command, "@branchNo", branchTurnover.BranchNo);
        AddParameter(command, "@customerCount", branchTurnover.CustomerCount);
        AddParameter(command, "@furparaCardCustomerCount", branchTurnover.FurparaCardCustomerCount);
        AddParameter(command, "@discountCardCustomerCount", branchTurnover.DiscountCardCustomerCount);
        AddParameter(command, "@expenseNoteCount", branchTurnover.ExpenseNoteCount);
        AddParameter(command, "@futuresSalesCount", branchTurnover.FuturesSalesCount);
        AddParameter(command, "@turnoverDate", branchTurnover.Date.Date);
        AddParameter(command, "@turnoverLastUpdate", DateTime.Now);
        AddParameter(command, "@turnoverOverallTotal", branchTurnover.OverallTotal);
    }

    private static void AddTurnoverDetailParameters(DbCommand command, TurnoverDetail detail)
    {
        AddParameter(command, "@cashRegisterNo", detail.CashRegisterNo);
        AddParameter(command, "@lastBillTime", detail.LastBillTime);
        AddParameter(command, "@creditTotal", detail.CreditTotal);
        AddParameter(command, "@cashTotal", detail.CashTotal);
        AddParameter(command, "@giftCardTotal", detail.GiftCardTotal);
        AddParameter(command, "@expenseNoteTotal", detail.ExpenseNoteTotal);
        AddParameter(command, "@futuresSalesTotal", detail.FuturesSalesTotal);
    }

    private static void AddDiscountCardDetailParameters(DbCommand command, TurnoverDiscountCardDetail detail)
    {
        AddParameter(command, "@cardNumber", detail.CardNumber);
        AddParameter(command, "@date", detail.Date.Date);
        AddParameter(command, "@usageCount", detail.UsageCount);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private IReadOnlyCollection<string> FindFiles(
        string movementRootPath,
        DateTime date,
        int branchNo,
        bool requestedSpecificBranches,
        ImportState state)
    {
        var branchFolder = Path.Combine(movementRootPath, branchNo.ToString(CultureInfo.InvariantCulture));
        var fileName = $"{FilePrefix}{date:ddMMyy}";

        if (!Directory.Exists(branchFolder))
        {
            if (requestedSpecificBranches)
            {
                state.AddWarning(date, branchNo, null, branchFolder, null, "Sube klasoru bulunamadi.");
            }

            return Array.Empty<string>();
        }

        try
        {
            var files = Directory
                .GetFiles(branchFolder, $"{fileName}.*", SearchOption.TopDirectoryOnly)
                .Where(file => Path.GetFileName(file).StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length == 0 && requestedSpecificBranches)
            {
                state.AddWarning(
                    date,
                    branchNo,
                    null,
                    Path.Combine(branchFolder, $"{fileName}.*"),
                    null,
                    "Ciro hareket dosyasi bulunamadi.");
            }

            return files;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            state.AddError(date, branchNo, null, branchFolder, null, exception.Message);
            return Array.Empty<string>();
        }
    }

    private string ResolveMovementRootPath(string? requestMovementRootPath)
    {
        var movementRootPath = FirstNonEmpty(
            requestMovementRootPath,
            configuration["KasaCiroAktarimi:MovementFilePath"],
            configuration["MovementFileSetting:MovementFilePath"],
            configuration["KasaHareketAktarimi:FileRootPath"],
            DefaultMovementRootPath);

        return movementRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static IReadOnlyCollection<int> NormalizeBranchNumbers(IReadOnlyCollection<int>? branches)
    {
        if (branches is not { Count: > 0 })
        {
            return Enumerable
                .Range(DefaultFirstBranchNo, DefaultLastBranchNo - DefaultFirstBranchNo + 1)
                .ToArray();
        }

        var normalized = branches
            .Where(branchNo => branchNo > 0)
            .Distinct()
            .OrderBy(branchNo => branchNo)
            .ToArray();

        if (normalized.Length != branches.Count)
        {
            throw new ArgumentException("Branch numbers must be greater than zero.", nameof(branches));
        }

        return normalized;
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

    private static IReadOnlyCollection<DateTime> EnumerateDates(DateTime startDate, DateTime endDate)
    {
        var dates = new List<DateTime>();

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            dates.Add(date);
        }

        return dates;
    }

    private static bool TryParseCashRegisterNo(string filePath, out int cashRegisterNo)
    {
        var extension = Path.GetExtension(filePath).TrimStart('.');
        return int.TryParse(extension, NumberStyles.Integer, CultureInfo.InvariantCulture, out cashRegisterNo);
    }

    private static bool IsFutureSaleCustomerCode(string customerCode)
    {
        var trimmed = customerCode.Trim();
        return trimmed.Length > 0 && trimmed.Length < 12;
    }

    private static string[] SplitComma(string line, int count = int.MaxValue) =>
        count == int.MaxValue
            ? line.Split(',', StringSplitOptions.TrimEntries)
            : line.Split(',', count, StringSplitOptions.TrimEntries);

    private static string ReadColumn(IReadOnlyList<string> columns, int index) =>
        index >= 0 && index < columns.Count ? columns[index].Trim() : string.Empty;

    private static bool ContainsCode(string line, string code) =>
        line.Contains(code, StringComparison.OrdinalIgnoreCase);

    private static string ReadAfterLastComma(string line)
    {
        var index = line.LastIndexOf(',');
        return index < 0 ? string.Empty : line[(index + 1)..].Trim();
    }

    private static decimal ParseLastAmount(string line) =>
        ParseDecimal(ReadAfterLastComma(line));

    private static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        var normalized = value.Trim();

        if (normalized.Contains('.') &&
            !normalized.Contains(',') &&
            decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantParsed))
        {
            return invariantParsed;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, TurkishCulture, out var turkishParsed))
        {
            return turkishParsed;
        }

        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out invariantParsed))
        {
            return invariantParsed;
        }

        var legacyNormalized = normalized.Replace('.', ',');
        if (decimal.TryParse(legacyNormalized, NumberStyles.Number, TurkishCulture, out turkishParsed))
        {
            return turkishParsed;
        }

        throw new FormatException($"Tutar okunamadi: '{value}'.");
    }

    private static string SafeSubstring(string value, int startIndex, int length)
    {
        if (startIndex < 0 || startIndex >= value.Length || length <= 0)
        {
            return string.Empty;
        }

        return value.Substring(startIndex, Math.Min(length, value.Length - startIndex));
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string CreateRunId(DateTime startDate) =>
        $"kasa-ciro-{startDate:yyyyMMdd}-{DateTime.Now:HHmmss}";

    private static string ResolveStatus(bool dryRun, int errorCount)
    {
        if (errorCount > 0)
        {
            return dryRun ? "DryRunCompletedWithErrors" : "CompletedWithErrors";
        }

        return dryRun ? "DryRunCompleted" : "Completed";
    }

    private sealed class FileTurnoverState
    {
        public decimal CreditAmount { get; set; }

        public decimal CashAmount { get; set; }

        public decimal GiftCardAmount { get; set; }

        public decimal ExpenseNoteAmount { get; set; }

        public decimal FuturesSalesAmount { get; set; }

        public string CustomerCode { get; set; } = string.Empty;

        public string LastDocumentType { get; set; } = string.Empty;

        public string LastBillTime { get; set; } = string.Empty;
    }

    private sealed class BranchTurnover(DateTime date, int branchNo)
    {
        public DateTime Date { get; } = date.Date;

        public int BranchNo { get; } = branchNo;

        public int CustomerCount { get; set; }

        public int FurparaCardCustomerCount { get; set; }

        public int DiscountCardCustomerCount { get; set; }

        public int ExpenseNoteCount { get; set; }

        public int FuturesSalesCount { get; set; }

        public List<TurnoverDetail> Details { get; } = [];

        public Dictionary<string, TurnoverDiscountCardDetail> DiscountCards { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public decimal OverallTotal =>
            Details.Sum(detail =>
                detail.CashTotal +
                detail.CreditTotal +
                detail.GiftCardTotal +
                detail.FuturesSalesTotal);
    }

    private sealed record TurnoverDetail(
        int CashRegisterNo,
        string LastBillTime,
        decimal CreditTotal,
        decimal CashTotal,
        decimal GiftCardTotal,
        decimal ExpenseNoteTotal,
        decimal FuturesSalesTotal);

    private sealed class TurnoverDiscountCardDetail(string cardNumber, DateTime date, int usageCount)
    {
        public string CardNumber { get; } = cardNumber;

        public DateTime Date { get; } = date.Date;

        public int UsageCount { get; set; } = usageCount;
    }

    private sealed class WriteResult
    {
        public int InsertedTotals { get; set; }

        public int UpdatedTotals { get; set; }

        public int InsertedDetails { get; set; }

        public int UpdatedDetails { get; set; }

        public int InsertedDiscountCards { get; set; }

        public int UpdatedDiscountCards { get; set; }
    }

    private sealed class ImportState(DateTime startDate, DateTime endDate, bool dryRun)
    {
        private readonly List<KasaCiroImportIssueDto> warnings = [];
        private readonly List<KasaCiroImportIssueDto> errors = [];

        public int ProcessedDays { get; set; }

        public int ProcessedBranches { get; set; }

        public int ProcessedFiles { get; set; }

        public int SkippedEmptyBranches { get; set; }

        public int InsertedTotals { get; set; }

        public int UpdatedTotals { get; set; }

        public int InsertedDetails { get; set; }

        public int UpdatedDetails { get; set; }

        public int InsertedDiscountCards { get; set; }

        public int UpdatedDiscountCards { get; set; }

        public void AddWarning(
            DateTime? date,
            int? branchNo,
            int? cashRegisterNo,
            string? file,
            int? lineNo,
            string message) =>
            warnings.Add(new KasaCiroImportIssueDto(date, branchNo, cashRegisterNo, file, lineNo, message));

        public void AddError(
            DateTime? date,
            int? branchNo,
            int? cashRegisterNo,
            string? file,
            int? lineNo,
            string message) =>
            errors.Add(new KasaCiroImportIssueDto(date, branchNo, cashRegisterNo, file, lineNo, message));

        public KasaCiroImportResultDto ToDto() =>
            new(
                CreateRunId(startDate),
                ResolveStatus(dryRun, errors.Count),
                startDate,
                endDate,
                ProcessedDays,
                ProcessedBranches,
                ProcessedFiles,
                SkippedEmptyBranches,
                InsertedTotals,
                UpdatedTotals,
                InsertedDetails,
                UpdatedDetails,
                InsertedDiscountCards,
                UpdatedDiscountCards,
                warnings.ToArray(),
                errors.ToArray());
    }
}
