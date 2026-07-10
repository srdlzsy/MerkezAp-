namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.YeniKasaAnalizleri;

public sealed record YeniKasaAnalizRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    string? CashRegisterNo,
    string? CashierCode,
    int Take,
    bool OnlyProblematic);

public sealed record YeniKasaFisDetayRequest(
    string? Uuid,
    DateTime? BusinessDate,
    int? WarehouseNo,
    string? CashRegisterNo,
    string? ReceiptNumber);

public sealed record YeniKasaCiroOzetItemDto(
    DateTime BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    string CashRegisterNo,
    string CashierCode,
    string CashierName,
    int SaleRowCount,
    int ReceiptCount,
    int ProductLineCount,
    double ProductQuantity,
    double SaleTotal,
    int PaymentLineCount,
    double PaymentTotal,
    double Difference,
    DateTime? FirstSaleAt,
    DateTime? LastSaleAt);

public sealed record YeniKasaKasaOzetItemDto(
    DateTime BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    string CashRegisterNo,
    int SaleRowCount,
    int ReceiptCount,
    double SaleTotal,
    double PaymentTotal,
    double CashTotal,
    double CreditCardTotal,
    double GiftCardTotal,
    double OtherPaymentTotal,
    double UnknownPaymentTotal,
    double Difference,
    int CashierCount,
    DateTime? LastSaleAt);

public sealed record YeniKasaFisMutabakatItemDto(
    DateTime BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    string CashRegisterNo,
    string CashierCode,
    string CashierName,
    string Uuid,
    string ReceiptNumber,
    int SaleRowCount,
    int ProductLineCount,
    int PaymentLineCount,
    double SaleTotal,
    double ProductLineTotal,
    double PaymentTotal,
    double SalePaymentDifference,
    double SaleLineDifference,
    string Status,
    IReadOnlyCollection<string> Issues,
    DateTime? ReceivedAt);

public sealed record YeniKasaAnomalyItemDto(
    string Type,
    string Severity,
    DateTime? BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    string CashRegisterNo,
    string CashierCode,
    string Uuid,
    string ReceiptNumber,
    double SaleTotal,
    double PaymentTotal,
    double Difference,
    string Description);

public sealed record YeniKasaPaymentMethodItemDto(
    string PaymentMethodCode,
    string PaymentMethodName,
    string Category,
    int? PaymentMethodId,
    int? PavoMediator,
    int? PavoType,
    int PaymentLineCount,
    double Amount,
    bool IsKnown);

public sealed record YeniKasaSaglikOzetItemDto(
    DateTime BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    string CashRegisterNo,
    int ReceiptCount,
    int ProblemReceiptCount,
    int CriticalProblemCount,
    double SaleTotal,
    double PaymentTotal,
    double DifferenceTotal,
    DateTime? LastSaleAt,
    string RiskLevel,
    IReadOnlyCollection<string> TopIssues);

public sealed record YeniKasaFisDetayDto(
    string Uuid,
    string ReceiptNumber,
    DateTime? BusinessDate,
    int WarehouseNo,
    string WarehouseName,
    string CashRegisterNo,
    string CashierCode,
    string CashierName,
    double SaleTotal,
    double ProductLineTotal,
    double PaymentTotal,
    double SalePaymentDifference,
    double SaleLineDifference,
    string Status,
    IReadOnlyCollection<string> Issues,
    IReadOnlyCollection<YeniKasaFisMutabakatItemDto> ReconciliationItems,
    IReadOnlyCollection<YeniKasaFisSatisSatiriDto> SaleRows,
    IReadOnlyCollection<YeniKasaFisUrunSatiriDto> ProductLines,
    IReadOnlyCollection<YeniKasaFisOdemeSatiriDto> Payments);

public sealed record YeniKasaFisSatisSatiriDto(
    int Id,
    string Uuid,
    string ReceiptNumber,
    DateTime ReceivedAt,
    int WarehouseNo,
    string WarehouseCode,
    string CashRegisterNo,
    string CashierCode,
    double SaleTotal,
    double RemainingAmount,
    string MarketId,
    string Status);

public sealed record YeniKasaFisUrunSatiriDto(
    int Id,
    string SaleUuid,
    decimal Quantity,
    double TotalPrice);

public sealed record YeniKasaFisOdemeSatiriDto(
    int Id,
    string SaleUuid,
    string PaymentMethodCode,
    string PaymentMethodName,
    string Category,
    int? PaymentMethodId,
    int? PavoMediator,
    int? PavoType,
    double Amount,
    bool IsIncludedInTotals);
