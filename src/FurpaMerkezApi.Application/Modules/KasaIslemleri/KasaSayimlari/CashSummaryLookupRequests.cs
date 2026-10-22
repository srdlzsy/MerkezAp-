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
    string? CashRegisterNo = null,
    int? BranchNo = null);

public sealed record BankPaymentTypeRequest(
    string? CashRegisterNo,
    int? BranchNo = null,
    int? CashNo = null);

public sealed record ZReportValueRequest(
    int WarehouseNo,
    string? DocumentSerie,
    int ZReportNo,
    int CashNo);
