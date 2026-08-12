namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;

public sealed record KasaHareketBranchDto(
    int BranchNo,
    string BranchName,
    string Region);

public sealed record KasaHareketCashRegisterDto(
    int BranchNo,
    int CashRegisterNo,
    byte CashRegisterType,
    string CashRegisterTypeName,
    string CashRegisterTypeDescription);

public sealed record KasaHareketImportRequest(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyCollection<int>? Branches,
    IReadOnlyCollection<int>? CashRegisters,
    string? FileRootPath,
    bool SkipExisting,
    bool DryRun);

public sealed record KasaHareketScheduledImportRequest(
    DateTime? Date,
    int? AddDay,
    string? FileRootPath,
    bool SkipExisting,
    bool DryRun);

public sealed record KasaHareketDeleteStagingRequest(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo);

public sealed record KasaHareketMikroTransferRequest(
    DateTime Date,
    int? BranchNo);

public sealed record KasaHareketMikroTransferRangeRequest(
    DateTime StartDate,
    DateTime EndDate);

public sealed record KasaHareketReportRequest(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo);

public sealed record KasaHareketCashSummaryComparisonRequest(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo,
    decimal Tolerance);

public sealed record KasaHareketImportResultDto(
    string RunId,
    string ImportType,
    string Status,
    int ProcessedFiles,
    int ProcessedInvoices,
    int SkippedExistingInvoices,
    int InsertedLines,
    int InsertedPayments,
    int InsertedPromotions,
    IReadOnlyCollection<KasaHareketImportIssueDto> Warnings,
    IReadOnlyCollection<KasaHareketImportIssueDto> Errors);

public sealed record KasaHareketImportIssueDto(
    int? BranchNo,
    int? CashRegisterNo,
    string? File,
    string? ReceiptNo,
    int? LineNo,
    string Message);

public sealed record KasaHareketProcedureResultDto(
    string Procedure,
    string Message,
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo);

public sealed record KasaHareketReportRowDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    int CashRegisterNo,
    decimal NetAmount,
    decimal Expense,
    decimal CheckAmount,
    decimal Difference);

public sealed record KasaHareketReportSummaryDto(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo,
    int RowCount,
    decimal TotalNetAmount,
    decimal TotalExpense,
    decimal TotalCheckAmount,
    decimal TotalDifference);

public sealed record KasaHareketCashSummaryComparisonDto(
    DateTime Date,
    int? BranchNo,
    int? CashRegisterNo,
    decimal Tolerance,
    KasaHareketCashSummaryComparisonSummaryDto Summary,
    IReadOnlyCollection<KasaHareketCashSummaryComparisonRowDto> Rows);

public sealed record KasaHareketCashSummaryComparisonSummaryDto(
    int RowCount,
    int BalancedCount,
    int DifferenceCount,
    int MissingCashSummaryCount,
    int MissingMovementCount,
    decimal TotalMovementZReportAmount,
    decimal TotalCashSummaryAmount,
    decimal TotalDifferenceAmount);

public sealed record KasaHareketCashSummaryComparisonRowDto(
    DateTime Date,
    int BranchNo,
    string BranchName,
    int CashRegisterNo,
    decimal MovementNetAmount,
    decimal MovementExpense,
    decimal MovementCheckAmount,
    decimal MovementZReportAmount,
    decimal CashSummaryAmount,
    int CashSummaryDocumentCount,
    decimal DifferenceAmount,
    string Status,
    string StatusName);
