namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;

public sealed record CashierPairRequest(
    int CashierCode,
    int ManagerCode);

public sealed record CashierSearchRequest(
    string Filter);

public sealed record CashRegistryRequest(
    int BranchNo);

public sealed record CashRegisterLookupRequest(
    int? CashNo,
    string? CashRegisterNo = null);

public sealed record BankPaymentTypeRequest(
    string CashRegisterNo);

public sealed record ZReportValueRequest(
    int WarehouseNo,
    string DocumentSerie,
    int ZReportNo,
    int CashNo);
