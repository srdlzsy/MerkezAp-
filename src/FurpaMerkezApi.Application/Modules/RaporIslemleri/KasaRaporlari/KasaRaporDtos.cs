namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.KasaRaporlari;

public sealed record ExpenseSlipReportRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int? WarehouseNo,
    int? CashRegisterNo,
    string? CashierCode);

public sealed record ExpenseSlipCashierSummaryDto(
    string CashierCode,
    string CashierName,
    decimal TotalExpenseAmount,
    int SlipCount);

public sealed record ExpenseSlipCashRegisterSummaryDto(
    int CashRegisterNo,
    decimal TotalExpenseAmount,
    int SlipCount,
    IReadOnlyCollection<ExpenseSlipCashierSummaryDto> Cashiers);

public sealed record ExpenseSlipBranchSummaryDto(
    int BranchNo,
    string BranchName,
    string ZoneCode,
    decimal TotalExpenseAmount,
    int SlipCount,
    IReadOnlyCollection<ExpenseSlipCashRegisterSummaryDto> CashRegisters);

public sealed record ExpenseSlipZoneSummaryDto(
    string ZoneCode,
    string ZoneName,
    decimal TotalExpenseAmount,
    int SlipCount,
    IReadOnlyCollection<ExpenseSlipBranchSummaryDto> Branches);

public sealed record ExpenseSlipDetailItemDto(
    int BranchNo,
    string BranchName,
    int CashRegisterNo,
    string CashierCode,
    string CashierName,
    DateTime Date,
    string Time,
    int SlipNo,
    Guid DocumentGuid,
    decimal Amount,
    string Description);
