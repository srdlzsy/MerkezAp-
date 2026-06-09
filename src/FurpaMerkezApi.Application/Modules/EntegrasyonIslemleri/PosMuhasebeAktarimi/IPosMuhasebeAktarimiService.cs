namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;

public interface IPosMuhasebeAktarimiService
{
    Task<PosAccountingOverviewDto> GetOverviewAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ZReportListItemDto>> ListZReportsAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken);

    Task<ZReportDetailDto> GetZReportDetailAsync(
        int totalId,
        CancellationToken cancellationToken);

    Task<PosAccountingImportResultDto> ImportZReportsAsync(
        ImportZReportsRequest request,
        CancellationToken cancellationToken);

    Task<PosAccountingBatchResultDto> SendZReportsToErpAsync(
        PosAccountingTransferRequest request,
        CancellationToken cancellationToken);

    Task<PosAccountingBatchResultDto> DeleteZReportsAsync(
        PosAccountingDeleteRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BranchInvoiceListItemDto>> ListPosInvoicesAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken);

    Task<BranchInvoiceDetailDto> GetPosInvoiceDetailAsync(
        int invoiceId,
        CancellationToken cancellationToken);

    Task<PosAccountingImportResultDto> ImportPosInvoicesAsync(
        ImportPosDocumentsRequest request,
        CancellationToken cancellationToken);

    Task<PosAccountingBatchResultDto> SendPosInvoicesToErpAsync(
        PosAccountingTransferRequest request,
        CancellationToken cancellationToken);

    Task<BranchInvoiceDetailDto> UpdatePosInvoiceAsync(
        UpdatePosInvoiceRequest request,
        CancellationToken cancellationToken);

    Task<PosAccountingBatchResultDto> DeletePosInvoicesAsync(
        PosAccountingDeleteRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExpenseNoteListItemDto>> ListExpenseNotesAsync(
        PosAccountingFilterRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseNoteDetailDto> GetExpenseNoteDetailAsync(
        int expenseId,
        CancellationToken cancellationToken);

    Task<PosAccountingImportResultDto> ImportExpenseNotesAsync(
        ImportPosDocumentsRequest request,
        CancellationToken cancellationToken);

    Task<PosAccountingBatchResultDto> SendExpenseNotesToErpAsync(
        PosAccountingTransferRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseNoteDetailDto> UpdateExpenseNoteAsync(
        UpdateExpenseNoteRequest request,
        CancellationToken cancellationToken);

    Task<PosAccountingBatchResultDto> DeleteExpenseNotesAsync(
        PosAccountingDeleteRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CashRegisterBranchMappingDto>> ListCashRegisterMappingsAsync(
        CashRegisterBranchMappingFilterRequest request,
        CancellationToken cancellationToken);

    Task<CashRegisterBranchMappingDto> CreateCashRegisterMappingAsync(
        UpsertCashRegisterBranchMappingRequest request,
        CancellationToken cancellationToken);

    Task<CashRegisterBranchMappingDto> UpdateCashRegisterMappingAsync(
        UpsertCashRegisterBranchMappingRequest request,
        CancellationToken cancellationToken);
}
