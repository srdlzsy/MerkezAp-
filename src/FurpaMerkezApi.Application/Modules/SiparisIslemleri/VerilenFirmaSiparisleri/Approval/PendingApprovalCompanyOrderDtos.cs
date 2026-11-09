namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Approval;

public sealed record PendingApprovalCompanyOrdersRequest(
    string? UserDefPerCode,
    string? CustomerCode,
    int? WarehouseNo,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int Take);

public sealed record PendingApprovalCompanyOrderItemDto(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime OrderDate,
    int BranchNo,
    string BranchName,
    string CustomerCode,
    string CustomerName,
    string CustomerTitle,
    double TotalAmount,
    int LineCount,
    bool CanBeCalled,
    string? PurchasingPersonCode,
    string? PurchasingPersonName);

public sealed record ApproveCompanyOrderCommand(
    string DocumentSerie,
    int DocumentOrderNo,
    int WarehouseNo);

public sealed record BatchApproveCompanyOrdersRequest(
    IReadOnlyCollection<ApproveCompanyOrderCommand> Orders);

public sealed record BatchApproveCompanyOrdersResultDto(
    int TotalRequested,
    int ApprovedCount,
    IReadOnlyCollection<string> ApprovedDocumentKeys,
    IReadOnlyCollection<string> FailedDocumentKeys);
