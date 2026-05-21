namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;

public sealed record CashSummaryReportItemDto(
    int WarehouseNo,
    string WarehouseName,
    double CashAmount,
    int CashAmountQuantity,
    double Akbank,
    int AkbankQuantity,
    double Halkbank,
    int HalkbankQuantity,
    double IsBankasi,
    int IsBankasiQuantity,
    double Teb,
    int TebQuantity,
    double YapiKredi,
    int YapiKrediQuantity,
    double ZiraatBankasi,
    int ZiraatBankasiQuantity,
    double Metropol,
    int MetropolQuantity,
    double Multinet,
    int MultinetQuantity,
    double Setcard,
    int SetcardQuantity,
    double SodexoKupon,
    int SodexoKuponQuantity,
    double SodexoPos,
    int SodexoPosQuantity,
    double TicketKupon,
    int TicketKuponQuantity,
    double TicketPos,
    int TicketPosQuantity,
    double ExpenseCompass,
    int ExpenseCompassQuantity,
    double StoreExpense,
    int StoreExpenseQuantity);

public sealed record CashSummaryListItemDto(
    int WarehouseNo,
    string WarehouseName,
    string DocumentSerie,
    int DocumentOrderNo,
    int CashNo,
    int ZReportNo,
    int CashierNo,
    int ManagerNo,
    DateTime SummaryDate,
    double Total);

public sealed record CashSummaryDetailItemDto(
    string TypeName,
    int PaymentTypeId,
    string AccountCode,
    int SlipNumber,
    double Amount,
    string TerminalId,
    string Description);

public sealed record BanknoteMovementItemDto(
    double Value,
    int BanknoteType,
    int Quantity,
    double Total);

public sealed record BanknoteTypeItemDto(
    double Value,
    double Quantity,
    double Total,
    int BanknoteType);

public sealed record GiftCheckMovementItemDto(
    double Value,
    int GiftCheckType,
    int Quantity,
    double Total);

public sealed record GiftCheckTypeItemDto(
    double Value,
    double Quantity,
    double Total,
    int GiftCheckType);

public sealed record PaymentTypeItemDto(
    string PaymentName,
    int PaymentTypeNo,
    string TerminalId,
    string AccountCode,
    int SlipNumber,
    double AmountValue);

public sealed record CashierItemDto(
    int CashierId,
    int CreateUser,
    DateTime CreateDate,
    int UpdateUser,
    DateTime UpdateDate,
    int CashierCode,
    string CashierName,
    string CashierPassword,
    string CashierAuthorization,
    bool CashierState);

public sealed record CashierSearchItemDto(
    int CashierCode,
    string CashierName,
    string CashierPassword,
    string CashierAuthorization,
    bool CashierState);

public sealed record CashRegistryItemDto(
    int DetailId,
    int BranchNo,
    int CashRegisterNo,
    byte CashRegisterType);

public sealed record CashRegisterDetailDto(
    int Id,
    string CashRegisterNo,
    string Bank,
    string TerminalId,
    string MerchantNo,
    int? CashNo);
