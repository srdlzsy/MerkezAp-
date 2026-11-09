namespace FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Approval;

public interface IListPendingApprovalCompanyOrdersUseCase
{
    Task<IReadOnlyCollection<PendingApprovalCompanyOrderItemDto>> ExecuteAsync(
        PendingApprovalCompanyOrdersRequest request,
        CancellationToken cancellationToken);
}

public interface IApproveIssuedCompanyOrdersUseCase
{
    Task<BatchApproveCompanyOrdersResultDto> ExecuteAsync(
        BatchApproveCompanyOrdersRequest request,
        CancellationToken cancellationToken);
}
