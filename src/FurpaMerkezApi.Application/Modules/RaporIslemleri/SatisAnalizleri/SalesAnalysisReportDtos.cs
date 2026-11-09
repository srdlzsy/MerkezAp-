namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.SatisAnalizleri;

public sealed record SalesAnalysisDateRangeRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate);

public sealed record SalesAnalysisAmountDto(
    string Code,
    string Name,
    double Amount);

public sealed record BankMovementAnalysisItemDto(
    int BranchNo,
    string BranchName,
    int ZNo,
    DateTime Date,
    string CashRegisterNo,
    string Bank,
    double BankAmount,
    int BankingNumber,
    string TerminalId);

public sealed record BranchBankMovementSummaryItemDto(
    int BranchNo,
    string BranchName,
    string Bank,
    double BankAmount,
    int BankingNumber);

public sealed record BankPaymentSummaryItemDto(
    string Bank,
    double Amount,
    int SlipNumber);

public sealed record BankPaymentSummaryReportDto(
    IReadOnlyCollection<BankPaymentSummaryItemDto> Items,
    double TotalAmount,
    int TotalSlipNumber);

public sealed record MerchantPaymentSummaryItemDto(
    string Bank,
    string MerchantNo,
    double Amount,
    int SlipNumber);

public sealed record MerchantPaymentSummaryReportDto(
    IReadOnlyCollection<MerchantPaymentSummaryItemDto> Items,
    double TotalAmount,
    int TotalSlipNumber);

public sealed record ValorPaymentSummaryItemDto(
    string Bank,
    int ValorDay,
    double Amount,
    int SlipNumber);

public sealed record ValorPaymentSummaryReportDto(
    IReadOnlyCollection<ValorPaymentSummaryItemDto> Items,
    double TotalAmount,
    int TotalSlipNumber);

public sealed record FoodCheckReportItemDto(
    int BranchNo,
    string BranchName,
    double Metropol,
    double Multinet,
    double Setcard,
    double SodexoKupon,
    double SodexoPos,
    double TicketKupon,
    double TicketPos,
    double Total);

public sealed record FoodCheckTotalsDto(
    double Metropol,
    double Multinet,
    double Setcard,
    double SodexoKupon,
    double SodexoPos,
    double TicketKupon,
    double TicketPos,
    double Total);

public sealed record FoodCheckReportDto(
    IReadOnlyCollection<FoodCheckReportItemDto> Items,
    FoodCheckTotalsDto Totals);

public enum FoodCheckTotalKind
{
    Total = 0,
    Metropol = 56,
    Multinet = 54,
    Setcard = 55,
    SodexoKupon = 51,
    SodexoPos = 50,
    TicketKupon = 53,
    TicketPos = 52
}

public sealed record MyoSalesReportItemDto(
    DateTime DocumentDate,
    int BranchNo,
    string BranchName,
    string DocumentSerie,
    int DocumentOrderNo,
    Guid? InvoiceGuid,
    string CustomerCode,
    string DocumentNo,
    string Description1,
    string Description2,
    string PaymentDescription,
    double SubTotal,
    double DiscountTotal,
    double NetAmount,
    double TotalTax,
    double Amount);

public sealed record MyoSalesReportDto(
    IReadOnlyCollection<MyoSalesReportItemDto> Items,
    double NetAmountTotal,
    double TotalTaxTotal,
    double AmountTotal,
    double DoorCashTotal,
    double DoorCreditCardTotal);

public sealed record MyoSalesByBranchItemDto(
    DateTime DocumentDate,
    int BranchNo,
    string BranchName,
    double Amount);

public sealed record ZReportBankAnalysisItemDto(
    string BranchName,
    int BranchNo,
    DateTime Date,
    int ZNo,
    string CashRegisterNo,
    string Bank,
    double BankAmount,
    int BankingNumber,
    string TerminalId,
    string MerchantNo);

public sealed record DiscountCardDetailItemDto(
    string CardNumber,
    int BranchNo,
    string BranchName,
    int UsageCount,
    double UsageTotal);

public sealed record MissingTurnoverBranchItemDto(
    int BranchNo,
    string BranchName,
    string Region);

public sealed record CategoryYearlyGrowthRequest(
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record CategoryYearlyGrowthItemDto(
    string CategoryName,
    string SubCategoryName,
    string SegmentName,
    string SubSegmentName,
    double CurrentQuantity,
    double CurrentAmount,
    double PreviousQuantity,
    double PreviousAmount,
    decimal GoalRatioPercent,
    decimal GoalAmount,
    decimal RealizedRatioPercent);

public sealed record CategorySalesDetailedRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int? BranchNo,
    string? SectorCode,
    string? RayonCode,
    string? CategoryCode,
    string? ProductCode,
    string? Region,
    int Take);

public sealed record CategorySalesDetailedItemDto(
    string Region,
    int BranchNo,
    string BranchName,
    string SectorName,
    string RayonName,
    string CategoryName,
    string ProductCode,
    string ProductName,
    double Quantity,
    double GrossAmount,
    double DiscountAmount,
    double NetAmount);
