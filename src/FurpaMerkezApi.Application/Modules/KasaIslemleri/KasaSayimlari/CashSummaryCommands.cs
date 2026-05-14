namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;

public sealed record CreateBanknoteTrackRequest(
    int WarehouseNo,
    DateTime BanknoteTrackDate,
    double TotalAmount,
    double DeliveryTotalAmount,
    string Deliverer,
    string Receiver);

public sealed record CreateCashSummaryRequest(
    int WarehouseNo,
    int CashNo,
    int ZReportNo,
    int CashierNo,
    int ManagerNo,
    double ZTotalValue,
    double Total,
    DateTime SummaryDate,
    IReadOnlyCollection<CreateCashSummaryGiftCheckLineRequest> GiftCheckMovements,
    IReadOnlyCollection<CreateCashSummaryBanknoteLineRequest> BanknoteMovements,
    IReadOnlyCollection<CreateCashSummaryPaymentLineRequest> PaymentTypes,
    IReadOnlyCollection<CreateCashSummaryStoreExpenseLineRequest> StoreExpenses);

public sealed record CreateCashSummaryGiftCheckLineRequest(
    int GiftCheckType,
    int Quantity,
    double Total,
    double Value);

public sealed record CreateCashSummaryBanknoteLineRequest(
    int BanknoteType,
    int Quantity,
    double Total,
    double Value);

public sealed record CreateCashSummaryPaymentLineRequest(
    string PaymentName,
    int PaymentTypeNo,
    string AccountCode,
    string TerminalId,
    int SlipNumber,
    double AmountValue);

public sealed record CreateCashSummaryStoreExpenseLineRequest(
    int StoreExpenseType,
    string Description,
    double AmountValue);

public sealed record UpdateCashSummaryDetailsRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    IReadOnlyCollection<UpdateCashSummaryDetailLineRequest> Details);

public sealed record UpdateCashSummaryDetailLineRequest(
    string TypeName,
    int PaymentTypeId,
    string AccountCode,
    int SlipNumber,
    double Amount,
    string TerminalId,
    string Description);

public sealed record UpdateCashSummaryBanknotesRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo,
    IReadOnlyCollection<UpdateCashSummaryBanknoteLineRequest> BanknoteMovements);

public sealed record UpdateCashSummaryBanknoteLineRequest(
    double Value,
    int BanknoteType,
    int Quantity,
    double Total);

public sealed record DeleteCashSummaryRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);

public sealed record CreateBanknoteTrackResponse(
    int BanknoteTrackId,
    DateTime BanknoteTrackDate,
    int WarehouseNo,
    bool Created);

public sealed record CreateCashSummaryResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime SummaryDate,
    int WarehouseNo,
    int LineCount,
    double Total,
    string WriteConnectionName);

public sealed record UpdateCashSummaryDetailsResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    int UpdatedLineCount,
    double TotalAmount);

public sealed record UpdateCashSummaryBanknotesResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    int UpdatedLineCount,
    double TotalAmount);

public sealed record DeleteCashSummaryResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    int DeletedSummaryLineCount,
    int DeletedBanknoteLineCount,
    int DeletedGiftCheckLineCount,
    int DeletedCustomerMovementCount);
