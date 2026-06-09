namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;

public sealed record PosAccountingFilterRequest(
    DateTime? StartDate,
    DateTime? EndDate,
    int? WarehouseNo,
    bool OnlyPending);

public sealed record ImportZReportsRequest(
    int? WarehouseNo,
    DateTime? BusinessDate,
    string? ReportPath,
    string? ImportMode,
    string? SourceCode,
    bool OverwriteExisting);

public sealed record ImportPosDocumentsRequest(
    int? WarehouseNo,
    DateTime BusinessDate,
    bool IncludePreviouslyImported,
    bool OverwriteExisting);

public sealed record PosAccountingTransferRequest(
    IReadOnlyCollection<int> DocumentIds,
    bool ContinueOnError);

public sealed record PosAccountingDeleteRequest(
    IReadOnlyCollection<int> DocumentIds);

public sealed record UpdatePosInvoiceRequest(
    int InvoiceId,
    int? DocumentNo,
    string? CustomerTaxNo,
    string? PaymentType);

public sealed record UpdateExpenseNoteRequest(
    int ExpenseId,
    int? BranchNo,
    string? DocumentNo,
    string? PaymentType);

public sealed record CashRegisterBranchMappingFilterRequest(
    int? BranchNo,
    string? CashRegisterNo);

public sealed record UpsertCashRegisterBranchMappingRequest(
    int? Id,
    string CashRegisterNo,
    int BranchNo);

public sealed record PosAccountingOverviewDto(
    int PendingZReportCount,
    double PendingZReportTotal,
    int PendingInvoiceCount,
    decimal PendingInvoiceTotal,
    int PendingExpenseNoteCount,
    decimal PendingExpenseNoteTotal,
    int CashRegisterMappingCount);

public sealed record ZReportListItemDto(
    int TotalId,
    int BillNo,
    int ZNo,
    string CashRegisterNo,
    string BranchName,
    DateTime Date,
    double CashPaymentTotal,
    double CreditCardPaymentTotal,
    double GreatTotal,
    bool IsSent);

public sealed record ZReportDetailLineDto(
    int DetailId,
    int TotalId,
    byte TaxRate,
    double BillTotal,
    double BillTaxTotal);

public sealed record ZReportBankDetailDto(
    int BankDetailId,
    int TotalId,
    string Bank,
    double BankAmount,
    int BankingNumber);

public sealed record ZReportDetailDto(
    ZReportListItemDto Header,
    IReadOnlyCollection<ZReportDetailLineDto> Details,
    IReadOnlyCollection<ZReportBankDetailDto> BankDetails);

public sealed record BranchInvoiceListItemDto(
    int InvoiceId,
    Guid InvoiceGuid,
    int BranchNo,
    string BranchName,
    int DocumentNo,
    string CustomerTaxNo,
    string CustomerName,
    DateTime InvoiceDate,
    string PaymentType,
    decimal InvoiceTotal,
    bool IsSent);

public sealed record BranchInvoiceLineDto(
    int LineId,
    int InvoiceId,
    short TaxRate,
    decimal Amount,
    decimal TaxAmount);

public sealed record BranchInvoiceDetailDto(
    BranchInvoiceListItemDto Header,
    IReadOnlyCollection<BranchInvoiceLineDto> Lines);

public sealed record ExpenseNoteListItemDto(
    int ExpenseId,
    Guid ExpenseGuid,
    string DocumentNo,
    int BranchNo,
    string BranchName,
    DateTime ExpenseDate,
    string PaymentType,
    decimal ExpenseTotal,
    bool IsSent);

public sealed record ExpenseNoteLineDto(
    int LineId,
    int ExpenseNoteId,
    short TaxRate,
    decimal Amount,
    decimal TaxAmount);

public sealed record ExpenseNoteDetailDto(
    ExpenseNoteListItemDto Header,
    IReadOnlyCollection<ExpenseNoteLineDto> Lines);

public sealed record CashRegisterBranchMappingDto(
    int Id,
    string CashRegisterNo,
    int BranchNo,
    string BranchName);

public sealed record PosAccountingImportResultDto(
    string DocumentKind,
    DateTime BusinessDate,
    int ImportedCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyCollection<PosAccountingOperationResultDto> Results);

public sealed record PosAccountingBatchResultDto(
    string DocumentKind,
    int RequestedCount,
    int SuccessCount,
    int ErrorCount,
    IReadOnlyCollection<PosAccountingOperationResultDto> Results);

public sealed record PosAccountingOperationResultDto(
    int? DocumentId,
    Guid? SourceGuid,
    bool Success,
    string Message);
