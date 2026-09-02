namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari;

public sealed record CashTurnoverListItemDto(
    DateTime BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    int ShiftNo,
    string CashierCode,
    string CashierName,
    int ProductLineCount,
    double TotalSalesQuantity,
    double TotalSalesAmount,
    int PaymentLineCount,
    double TotalCollectionAmount,
    double TotalCustomerCommission,
    double NetCollectionAmount,
    string Source,
    double GrossSalesTotal,
    double ComparisonTotal,
    double FuturesSalesTotal,
    bool PaymentDataMissing);

public sealed record CashTurnoverHeaderDto(
    DateTime BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    int ShiftNo,
    string CashierCode,
    string CashierName,
    int ProductLineCount,
    double TotalSalesQuantity,
    double TotalSalesAmount,
    int PaymentLineCount,
    double TotalCollectionAmount,
    double TotalCustomerCommission,
    double NetCollectionAmount,
    string Source,
    double GrossSalesTotal,
    double ComparisonTotal,
    double FuturesSalesTotal,
    bool PaymentDataMissing);

public sealed record CashTurnoverPaymentDetailItemDto(
    int PaymentTypeNo,
    string PaymentTypeName,
    string CashBankCode,
    string CashBankName,
    int PaymentLineCount,
    double Amount,
    double CustomerCommission,
    double NetAmount,
    string Source);

public sealed record CashTurnoverDetailDto(
    CashTurnoverHeaderDto Header,
    IReadOnlyCollection<CashTurnoverPaymentDetailItemDto> Payments);

public sealed record CashTurnoverOverviewDto(
    double DailyTotal,
    double DailyCashPayment,
    double DailyCreditCardPayment,
    double DailyGiftCardPayment,
    double DailyExpenseNoteTotal,
    int DailyCustomerCount,
    int DailyFurparaCardCustomerCount,
    int DailyDiscountCardCustomerCount,
    int DailyExpenseNoteCount,
    double AverageBasketAmount,
    int DailyFuturesSalesCount,
    double DailyFuturesSalesTotal,
    double DailyCollectionTotal,
    double DailyGrossSalesTotal,
    double DailyComparisonTotal,
    int DailyPaymentDataMissingBranchCount,
    IReadOnlyCollection<CashTurnoverBranchOverviewItemDto> SubeCirolari);

public sealed record CashTurnoverBranchOverviewItemDto(
    string Region,
    int BranchNo,
    string BranchName,
    int CustomerCount,
    int DiscountCardCustomerCount,
    int FurparaCardCustomerCount,
    string LastBillTime,
    double CashTotal,
    double CreditTotal,
    double GiftCardTotal,
    double ExpenseNoteTotal,
    int ExpenseNoteCount,
    double OverallTotal,
    double FuturesSalesTotal,
    int FuturesSalesCount,
    double AverageBasketAmount,
    double CollectionTotal,
    double GrossSalesTotal,
    double ComparisonTotal,
    bool PaymentDataMissing);
